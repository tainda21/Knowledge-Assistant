"""Deterministic retrieval over documents included in an individual chat request."""

from __future__ import annotations

import re

from core.state import AgentState, DocumentChunk
from modules.vector_store import search

CHUNK_SIZE = 1_200
CHUNK_OVERLAP = 200
MAX_RETRIEVED_CHUNKS = 5


def _tokens(text: str) -> set[str]:
    return {token for token in re.findall(r"[a-z0-9]+", text.lower()) if len(token) > 1}


def _split_content(content: str) -> list[str]:
    """Split content into bounded, overlapping excerpts without cutting words where possible."""
    normalized = " ".join(content.split())
    if not normalized:
        return []

    chunks: list[str] = []
    start = 0
    while start < len(normalized):
        end = min(start + CHUNK_SIZE, len(normalized))
        if end < len(normalized):
            boundary = normalized.rfind(" ", start, end)
            if boundary > start:
                end = boundary
        chunk = normalized[start:end].strip()
        if chunk:
            chunks.append(chunk)
        if end >= len(normalized):
            break
        start = max(end - CHUNK_OVERLAP, start + 1)
    return chunks


def _relevance_score(query_terms: set[str], content: str) -> float:
    if not query_terms:
        return 0.0
    return len(query_terms & _tokens(content)) / len(query_terms)


def retrieve_documents(state: AgentState) -> dict:
    """Search managed vectors, then add deterministic temporary-attachment matches."""
    query_terms = _tokens(state["query"])
    chunks: list[DocumentChunk] = []
    try:
        chunks.extend(search(state["query"], MAX_RETRIEVED_CHUNKS))
    except RuntimeError:
        # The chat path remains available while Chroma is unavailable or has not been initialized.
        pass
    for document in state.get("documents", []):
        for chunk_index, content in enumerate(_split_content(document["content"])):
            chunks.append(
                {
                    "source": document["name"],
                    "content": content,
                    "chunk_index": chunk_index,
                    "relevance_score": _relevance_score(query_terms, content),
                }
            )

    ranked = sorted(
        chunks,
        key=lambda chunk: (-chunk["relevance_score"], chunk["source"], chunk["chunk_index"]),
    )
    return {"retrieved_documents": ranked[:MAX_RETRIEVED_CHUNKS]}
