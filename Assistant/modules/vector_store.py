"""ChromaDB persistence and Gemini embedding helpers for managed documents."""

from __future__ import annotations

import os
from functools import lru_cache
from pathlib import Path
from typing import Any

from modules.gemini import get_genai_client

COLLECTION_NAME = "knowledge_chunks"
EMBED_BATCH_SIZE = 50


@lru_cache(maxsize=1)
def _collection():
    try:
        import chromadb
    except ImportError as error:
        raise RuntimeError("Managed document search requires the chromadb package.") from error
    persist_dir = Path(os.getenv("CHROMA_PERSIST_DIR", "./data/chroma_db"))
    persist_dir.mkdir(parents=True, exist_ok=True)
    return chromadb.PersistentClient(path=str(persist_dir)).get_or_create_collection(
        COLLECTION_NAME,
        metadata={"hnsw:space": "cosine"},
    )


def _embed(texts: list[str], task_type: str | None = None) -> list[list[float]]:
    if not texts:
        return []
    client = get_genai_client()
    model = os.getenv("EMBEDDING_MODEL", "gemini-embedding-001")
    vectors: list[list[float]] = []
    for start in range(0, len(texts), EMBED_BATCH_SIZE):
        batch = texts[start:start + EMBED_BATCH_SIZE]
        kwargs = {}
        if task_type:
            from google.genai import types
            kwargs["config"] = types.EmbedContentConfig(task_type=task_type)
        try:
            response = client.models.embed_content(model=model, contents=batch, **kwargs)
        except Exception as error:
            raise RuntimeError(f"Embedding generation failed for model '{model}': {error}") from error
        embeddings = getattr(response, "embeddings", None) or []
        if len(embeddings) != len(batch):
            raise RuntimeError(
                f"Embedding service returned {len(embeddings)} vectors for {len(batch)} inputs."
            )
        vectors.extend(list(item.values) for item in embeddings)
    return vectors


def replace_document(document_id: str, source_name: str, content_hash: str, chunks: list[str]) -> None:
    collection = _collection()
    if not chunks:
        collection.delete(where={"document_id": document_id})
        return
    embeddings = _embed(chunks, task_type="RETRIEVAL_DOCUMENT")
    collection.upsert(
        ids=[f"{document_id}:{content_hash}:{index}" for index in range(len(chunks))],
        documents=chunks,
        embeddings=embeddings,
        metadatas=[
            {"document_id": document_id, "source": source_name, "chunk_index": index, "content_hash": content_hash}
            for index in range(len(chunks))
        ],
    )
    collection.delete(where={"$and": [
        {"document_id": document_id},
        {"content_hash": {"$ne": content_hash}},
    ]})


def delete_document(document_id: str) -> None:
    _collection().delete(where={"document_id": document_id})


def search(query: str, limit: int = 5) -> list[dict[str, Any]]:
    result = _collection().query(query_embeddings=_embed([query], task_type="RETRIEVAL_QUERY"), n_results=limit, include=["documents", "metadatas", "distances"])
    documents = (result.get("documents") or [[]])[0]
    metadata = (result.get("metadatas") or [[]])[0]
    distances = (result.get("distances") or [[]])[0]
    return [
        {
            "source": item_metadata["source"],
            "content": item_document,
            "chunk_index": int(item_metadata["chunk_index"]),
            "relevance_score": max(0.0, 1.0 - float(distance)),
        }
        for item_document, item_metadata, distance in zip(documents, metadata, distances, strict=True)
    ]
