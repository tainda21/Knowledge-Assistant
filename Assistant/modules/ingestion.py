"""Document extraction, normalization, and chunking for persistent ingestion."""

from __future__ import annotations

import hashlib
from io import BytesIO
from pathlib import Path

CHUNK_SIZE = 1_000
CHUNK_OVERLAP = 150
SUPPORTED_EXTENSIONS = {".pdf", ".txt", ".md", ".markdown"}


def extract_text(source: bytes, filename: str, content_type: str | None = None) -> str:
    extension = Path(filename).suffix.lower()
    if extension not in SUPPORTED_EXTENSIONS:
        raise ValueError("Only PDF, TXT, Markdown, and .markdown files are supported.")
    if extension == ".pdf":
        try:
            from pypdf import PdfReader
            from pypdf.errors import PdfReadError
        except ImportError as error:
            raise RuntimeError("PDF ingestion requires the pypdf package.") from error
        try:
            reader = PdfReader(BytesIO(source))
            if reader.is_encrypted:
                raise ValueError("Encrypted PDFs are not supported.")
            return "\n".join(page.extract_text() or "" for page in reader.pages)
        except PdfReadError as error:
            raise ValueError("The PDF could not be read.") from error
    try:
        return source.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise ValueError("File content is not valid UTF-8.") from error


def clean_text(text: str) -> str:
    normalized_lines = [line.rstrip() for line in text.replace("\r\n", "\n").replace("\r", "\n").split("\n")]
    return "\n".join(normalized_lines).strip()


def normalize_text(text: str) -> str:
    return " ".join(text.split())


def content_hash(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def chunk_text(text: str) -> list[str]:
    normalized = normalize_text(text)
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
        chunks.append(normalized[start:end].strip())
        if end >= len(normalized):
            break
        next_start = max(end - CHUNK_OVERLAP, start + 1)
        space = normalized.find(" ", next_start, end)
        start = space + 1 if space != -1 else next_start
    return chunks
