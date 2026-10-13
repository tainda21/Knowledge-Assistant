import binascii
import json
import logging
import os
import secrets
from base64 import b64decode
from collections.abc import Iterator
from pathlib import Path

from dotenv import load_dotenv
from fastapi import Depends, FastAPI, Header, HTTPException
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, Field

from core.graph import chat_graph
from modules.gemini import stream_text
from modules.ingestion import chunk_text, clean_text, content_hash, extract_text
from modules.vector_store import delete_document, replace_document

MAX_MESSAGE_LENGTH = 8_000
MAX_SOURCE_BYTES = 20 * 1024 * 1024

load_dotenv(Path(__file__).resolve().parent / ".env")

logging.basicConfig(level=os.getenv("LOG_LEVEL", "INFO").upper())
logger = logging.getLogger(__name__)


def require_internal_key(x_internal_key: str | None = Header(default=None)) -> None:
    expected = os.getenv("ASSISTANT_INTERNAL_KEY")
    if not expected or not x_internal_key or not secrets.compare_digest(x_internal_key, expected):
        raise HTTPException(status_code=401, detail="Invalid internal key.")


class MessageInput(BaseModel):
    role: str = Field(pattern="^(user|assistant)$")
    content: str = Field(min_length=1, max_length=MAX_MESSAGE_LENGTH)


class DocumentInput(BaseModel):
    name: str = Field(min_length=1, max_length=255)
    content: str = Field(min_length=1, max_length=100_000)


class ChatRequest(BaseModel):
    query: str = Field(min_length=1, max_length=MAX_MESSAGE_LENGTH)
    chat_history: list[MessageInput] = Field(default_factory=list)
    documents: list[DocumentInput] = Field(default_factory=list, max_length=10)


class IndexDocumentRequest(BaseModel):
    document_id: str = Field(min_length=1)
    name: str = Field(min_length=1, max_length=255)
    source_file_name: str = Field(min_length=1, max_length=255)
    content_type: str | None = Field(default=None, max_length=128)
    content: str | None = Field(default=None, max_length=MAX_SOURCE_BYTES)
    source_base64: str | None = Field(default=None, max_length=(MAX_SOURCE_BYTES * 4) // 3 + 4)


def sse_event(event: str, data: dict) -> str:
    return f"event: {event}\ndata: {json.dumps(data)}\n\n"


app = FastAPI(title="Knowledge Assistant")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/api/assistant/documents/index", dependencies=[Depends(require_internal_key)])
def index_document(request: IndexDocumentRequest) -> dict:
    try:
        source = (
            b64decode(request.source_base64, validate=True)
            if request.source_base64
            else (request.content or "").encode("utf-8")
        )
        extracted_text = clean_text(extract_text(source, request.source_file_name, request.content_type))
        if not extracted_text:
            raise HTTPException(status_code=422, detail="No readable text could be extracted from this document.")
    except HTTPException:
        raise
    except (ValueError, binascii.Error) as error:
        raise HTTPException(status_code=422, detail=str(error)) from error

    digest = content_hash(extracted_text)
    chunks = chunk_text(extracted_text)
    try:
        replace_document(request.document_id, request.name, digest, chunks)
    except Exception as error:
        logger.exception("Document replacement failed")
        raise HTTPException(status_code=503, detail="Downstream vector store or embedding service failure.") from error

    return {"extracted_text": extracted_text, "content_hash": digest, "chunk_count": len(chunks)}


@app.delete("/api/assistant/documents/{document_id}", dependencies=[Depends(require_internal_key)])
def remove_document(document_id: str) -> dict[str, bool]:
    try:
        delete_document(document_id)
    except Exception as error:
        logger.exception("Document deletion failed")
        raise HTTPException(status_code=503, detail="Downstream vector store failure.") from error
    return {"deleted": True}


@app.post("/api/assistant/chat/stream")
def chat_stream(request: ChatRequest) -> StreamingResponse:
    logger.info(
        "Chat request: query_len=%d history=%d attachments=%d",
        len(request.query), len(request.chat_history), len(request.documents),
    )

    query = request.query.strip()
    if not query:
        raise HTTPException(status_code=422, detail="Query cannot be blank")

    def event_stream() -> Iterator[str]:
        try:
            state = chat_graph.invoke(
                {
                    "query": query,
                    "chat_history": [message.model_dump() for message in request.chat_history],
                    "documents": [document.model_dump() for document in request.documents],
                }
            )
            for token in stream_text(state["contents"]):
                yield sse_event("token", {"text": token})
            yield sse_event("complete", {})
        except RuntimeError as error:
            logger.exception("Chat stream failed with runtime error")
            yield sse_event("error", {"message": str(error)})
        except Exception:
            logger.exception("Chat stream failed")
            yield sse_event("error", {"message": "The AI service could not generate a response."})

    return StreamingResponse(event_stream(), media_type="text/event-stream", headers={"Cache-Control": "no-cache"})
