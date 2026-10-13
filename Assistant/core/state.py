from typing import TypedDict


class ChatMessage(TypedDict):
    role: str
    content: str


class Document(TypedDict):
    name: str
    content: str


class DocumentChunk(TypedDict):
    """A retrievable excerpt derived from a document supplied with a chat request."""

    source: str
    content: str
    chunk_index: int
    relevance_score: float


class AgentState(TypedDict, total=False):
    query: str
    chat_history: list[ChatMessage]
    documents: list[Document]
    retrieved_documents: list[DocumentChunk]
    graded_documents: list[DocumentChunk]
    has_document_context: bool
    contents: list[ChatMessage]
    answer: str
