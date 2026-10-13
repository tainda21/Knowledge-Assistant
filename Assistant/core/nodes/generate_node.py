"""Provider-neutral prompt assembly for answer generation."""

from core.state import AgentState, ChatMessage


def generate_response(state: AgentState) -> dict:
    """Build the chat transcript from history, approved excerpts, and the query."""
    contents: list[ChatMessage] = list(state.get("chat_history", []))
    documents = state.get("documents", [])
    graded_documents = state.get("graded_documents", [])

    if graded_documents:
        excerpts = "\n\n".join(
            f"Source: {chunk['source']} (excerpt {chunk['chunk_index'] + 1})\n{chunk['content']}"
            for chunk in graded_documents
        )
        contents.insert(
            0,
            {
                "role": "user",
                "content": (
                    "Use these approved document excerpts as grounding when they answer the query. "
                    "Do not invent facts not supported by them.\n\n"
                    f"{excerpts}"
                ),
            },
        )
    elif documents:
        contents.insert(
            0,
            {
                "role": "user",
                "content": (
                    "Documents were supplied for this request, but none contains relevant context. "
                    "Tell the user that the supplied documents do not answer their question instead "
                    "of implying that they do."
                ),
            },
        )

    contents.append({"role": "user", "content": state["query"]})
    return {"contents": contents}
