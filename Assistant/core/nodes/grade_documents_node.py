"""Document relevance grading for the transient retrieval workflow."""

from core.state import AgentState

RELEVANCE_THRESHOLD = 0.2


def grade_documents(state: AgentState) -> dict:
    """Keep only retrieved excerpts that meet the minimum lexical relevance score."""
    graded_documents = [
        chunk
        for chunk in state.get("retrieved_documents", [])
        if chunk["relevance_score"] >= RELEVANCE_THRESHOLD
    ]
    return {
        "graded_documents": graded_documents,
        "has_document_context": bool(graded_documents),
    }
