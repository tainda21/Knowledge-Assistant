import logging
from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient
from main import app

client = TestClient(app)
AUTH_HEADERS = {"X-Internal-Key": "test-key"}


@pytest.fixture(autouse=True)
def set_internal_key(monkeypatch):
    monkeypatch.setenv("ASSISTANT_INTERNAL_KEY", "test-key")


def test_internal_endpoints_require_valid_internal_key():
    # Missing header
    res1 = client.post("/api/assistant/documents/index", json={"document_id": "1", "name": "n", "source_file_name": "f.txt", "content": "hello"})
    assert res1.status_code == 401

    # Wrong key
    res2 = client.post(
        "/api/assistant/documents/index",
        json={"document_id": "1", "name": "n", "source_file_name": "f.txt", "content": "hello"},
        headers={"X-Internal-Key": "wrong-key"},
    )
    assert res2.status_code == 401

    # Delete with missing header
    res3 = client.delete("/api/assistant/documents/1")
    assert res3.status_code == 401


def test_index_rejects_unsupported_extension():
    response = client.post(
        "/api/assistant/documents/index",
        json={"document_id": "1", "name": "Test", "source_file_name": "doc.docx", "content": "text"},
        headers=AUTH_HEADERS,
    )
    assert response.status_code == 422
    assert "supported" in response.json()["detail"]


def test_index_rejects_invalid_base64():
    response = client.post(
        "/api/assistant/documents/index",
        json={"document_id": "1", "name": "Test", "source_file_name": "doc.txt", "source_base64": "invalid-base-64!!!"},
        headers=AUTH_HEADERS,
    )
    assert response.status_code == 422


def test_index_rejects_empty_content():
    response = client.post(
        "/api/assistant/documents/index",
        json={"document_id": "1", "name": "Test", "source_file_name": "doc.txt", "content": "   \n\t  "},
        headers=AUTH_HEADERS,
    )
    assert response.status_code == 422
    assert "No readable text" in response.json()["detail"]


def test_index_dependency_failure_maps_to_503(monkeypatch):
    def failing_replace(*args, **kwargs):
        raise RuntimeError("Chroma connection refused")

    monkeypatch.setattr("main.replace_document", failing_replace)

    response = client.post(
        "/api/assistant/documents/index",
        json={"document_id": "1", "name": "Test", "source_file_name": "doc.txt", "content": "valid content"},
        headers=AUTH_HEADERS,
    )
    assert response.status_code == 503
    assert "downstream" in response.json()["detail"].lower()


def test_delete_dependency_failure_maps_to_503(monkeypatch):
    def failing_delete(*args, **kwargs):
        raise RuntimeError("Chroma I/O error")

    monkeypatch.setattr("main.delete_document", failing_delete)

    response = client.delete("/api/assistant/documents/1", headers=AUTH_HEADERS)
    assert response.status_code == 503
    assert "downstream" in response.json()["detail"].lower()


def test_chat_stream_logs_only_metadata_not_content(caplog):
    caplog.set_level(logging.INFO)
    sensitive_query = "SUPER_SECRET_QUERY_TEXT_12345"

    with patch("main.chat_graph.invoke", return_value={"contents": []}), patch("main.stream_text", return_value=[]):
        response = client.post(
            "/api/assistant/chat/stream",
            json={"query": sensitive_query, "chat_history": [], "documents": []},
        )
        assert response.status_code == 200

    # Ensure the sensitive text is NOT in log records
    for record in caplog.records:
        assert sensitive_query not in record.message
        if "Chat request:" in record.message:
            assert f"query_len={len(sensitive_query)}" in record.message


def test_chat_stream_failure_yields_sse_error():
    with patch("main.chat_graph.invoke", side_effect=RuntimeError("Retrieval database down")):
        response = client.post(
            "/api/assistant/chat/stream",
            json={"query": "test query", "chat_history": [], "documents": []},
        )
        assert response.status_code == 200
        content = response.text
        assert "event: error" in content
