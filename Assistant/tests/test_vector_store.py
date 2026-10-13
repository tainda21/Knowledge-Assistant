from unittest.mock import MagicMock, patch

import pytest
from modules import vector_store


@pytest.fixture(autouse=True)
def isolated_chroma(tmp_path, monkeypatch):
    monkeypatch.setenv("CHROMA_PERSIST_DIR", str(tmp_path / "chroma"))
    vector_store._collection.cache_clear()
    yield
    vector_store._collection.cache_clear()


def fake_embeddings(texts, task_type=None):
    return [[0.1 * (i + 1), 0.2 * (i + 1)] for i in range(len(texts))]


def test_embed_batches_inputs_and_validates_count(monkeypatch):
    mock_client = MagicMock()
    mock_response = MagicMock()
    # Batch size is 50. Provide 105 texts.
    texts = [f"text {i}" for i in range(105)]

    def side_effect(model, contents, **kwargs):
        resp = MagicMock()
        resp.embeddings = [MagicMock(values=[0.1, 0.2]) for _ in contents]
        return resp

    mock_client.models.embed_content.side_effect = side_effect
    monkeypatch.setattr("modules.vector_store.get_genai_client", lambda: mock_client)

    vectors = vector_store._embed(texts, task_type="RETRIEVAL_DOCUMENT")
    assert len(vectors) == 105
    # Should be 3 batches: 50, 50, 5
    assert mock_client.models.embed_content.call_count == 3


def test_embed_raises_when_count_mismatches(monkeypatch):
    mock_client = MagicMock()
    resp = MagicMock()
    resp.embeddings = [MagicMock(values=[0.1, 0.2])]  # returns 1 vector for 2 inputs
    mock_client.models.embed_content.return_value = resp
    monkeypatch.setattr("modules.vector_store.get_genai_client", lambda: mock_client)

    with pytest.raises(RuntimeError, match="Embedding service returned 1 vectors for 2 inputs"):
        vector_store._embed(["text 1", "text 2"])


def test_replace_document_embeds_before_delete_on_failure(monkeypatch):
    monkeypatch.setattr("modules.vector_store._embed", fake_embeddings)

    # Initial index
    vector_store.replace_document("doc-1", "doc.txt", "hash-1", ["Initial chunk"])
    col = vector_store._collection()
    initial_records = col.get(where={"document_id": "doc-1"})
    assert len(initial_records["ids"]) == 1

    # Simulate embed failure during replacement
    def failing_embed(texts, task_type=None):
        raise RuntimeError("Gemini embed failed")

    monkeypatch.setattr("modules.vector_store._embed", failing_embed)
    with pytest.raises(RuntimeError, match="Gemini embed failed"):
        vector_store.replace_document("doc-1", "doc.txt", "hash-2", ["Updated chunk"])

    # Old vectors must still exist because delete happens after embedding and upsert
    records_after_failure = col.get(where={"document_id": "doc-1"})
    assert len(records_after_failure["ids"]) == 1
    assert records_after_failure["documents"][0] == "Initial chunk"


def test_replace_document_upserts_and_removes_stale_vectors(monkeypatch):
    monkeypatch.setattr("modules.vector_store._embed", fake_embeddings)

    # Initial index
    vector_store.replace_document("doc-1", "doc.txt", "hash-1", ["Chunk 1", "Chunk 2"])
    col = vector_store._collection()
    assert col.count() == 2

    # Re-indexing same content with same hash should overwrite IDs and not duplicate
    vector_store.replace_document("doc-1", "doc.txt", "hash-1", ["Chunk 1", "Chunk 2"])
    assert col.count() == 2

    # Update with new content and new hash
    vector_store.replace_document("doc-1", "doc.txt", "hash-2", ["New single chunk"])
    assert col.count() == 1
    updated_records = col.get(where={"document_id": "doc-1"})
    assert updated_records["documents"] == ["New single chunk"]


def test_delete_document_and_search_excludes_deleted(monkeypatch):
    monkeypatch.setattr("modules.vector_store._embed", fake_embeddings)

    vector_store.replace_document("doc-1", "doc.txt", "hash-1", ["Searchable text"])
    assert len(vector_store.search("Searchable")) == 1

    vector_store.delete_document("doc-1")
    assert len(vector_store.search("Searchable")) == 0
    assert vector_store._collection().count() == 0


def test_collection_client_is_cached():
    c1 = vector_store._collection()
    c2 = vector_store._collection()
    assert c1 is c2
