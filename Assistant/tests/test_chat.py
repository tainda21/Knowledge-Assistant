from core.graph import chat_graph
from core.nodes.generate_node import generate_response
from core.nodes.grade_documents_node import grade_documents
from core.nodes.retrieve_node import CHUNK_SIZE, retrieve_documents
from modules.gemini import get_genai_client


def test_generation_preserves_history_and_ends_with_query():
    result = generate_response({"query": "How are you?", "chat_history": [{"role": "assistant", "content": "Hi"}]})

    assert result["contents"] == [
        {"role": "assistant", "content": "Hi"},
        {"role": "user", "content": "How are you?"},
    ]


def test_retrieve_ranks_matching_chunks_and_splits_oversized_content():
    result = retrieve_documents(
        {
            "query": "return policy hardware",
            "documents": [
                {"name": "irrelevant.txt", "content": "Quarterly marketing plans."},
                {"name": "policy.txt", "content": "Hardware return policy details. " + ("x " * CHUNK_SIZE)},
            ],
        }
    )

    assert result["retrieved_documents"][0]["source"] == "policy.txt"
    assert result["retrieved_documents"][0]["relevance_score"] > 0
    assert all(len(chunk["content"]) <= CHUNK_SIZE for chunk in result["retrieved_documents"])


def test_retrieve_ignores_empty_content():
    result = retrieve_documents({"query": "policy", "documents": [{"name": "empty.txt", "content": "   "}]})

    assert result["retrieved_documents"] == []


def test_grading_excludes_low_relevance_chunks_and_handles_empty_results():
    result = grade_documents(
        {
            "retrieved_documents": [
                {"source": "keep.txt", "content": "return policy", "chunk_index": 0, "relevance_score": 0.5},
                {"source": "drop.txt", "content": "marketing", "chunk_index": 0, "relevance_score": 0.0},
            ]
        }
    )

    assert [chunk["source"] for chunk in result["graded_documents"]] == ["keep.txt"]
    assert result["has_document_context"] is True
    assert grade_documents({"retrieved_documents": []}) == {"graded_documents": [], "has_document_context": False}


def test_generation_includes_approved_excerpts_and_omits_rejected_ones():
    result = generate_response(
        {
            "query": "What is the policy?",
            "chat_history": [{"role": "assistant", "content": "Hello"}],
            "documents": [{"name": "policy.txt", "content": "allowed"}],
            "graded_documents": [
                {"source": "policy.txt", "content": "Returns are accepted within 30 days.", "chunk_index": 0, "relevance_score": 1.0}
            ],
        }
    )

    assert result["contents"][0]["content"].find("Source: policy.txt") >= 0
    assert result["contents"][1] == {"role": "assistant", "content": "Hello"}
    assert result["contents"][-1] == {"role": "user", "content": "What is the policy?"}


def test_generation_explains_when_supplied_documents_are_not_relevant():
    result = generate_response(
        {"query": "What is the policy?", "documents": [{"name": "other.txt", "content": "marketing"}], "graded_documents": []}
    )

    assert "do not answer" in result["contents"][0]["content"]


def test_compiled_graph_runs_all_document_nodes():
    result = chat_graph.invoke(
        {
            "query": "return policy",
            "documents": [{"name": "policy.txt", "content": "The return policy allows returns."}],
        }
    )

    assert result["has_document_context"] is True
    assert result["graded_documents"][0]["source"] == "policy.txt"
    assert result["contents"][-1] == {"role": "user", "content": "return policy"}


def test_missing_api_key_has_clear_error(monkeypatch):
    monkeypatch.delenv("GEMINI_API_KEY", raising=False)
    get_genai_client.cache_clear()

    try:
        get_genai_client()
    except RuntimeError as error:
        assert str(error) == "GEMINI_API_KEY is not configured"
    else:
        raise AssertionError("Expected missing API key to be rejected")
