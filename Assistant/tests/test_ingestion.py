import pytest
from modules.ingestion import (
    CHUNK_OVERLAP,
    CHUNK_SIZE,
    chunk_text,
    clean_text,
    content_hash,
    extract_text,
    normalize_text,
)


def test_text_extraction_normalizes_and_hashes_content():
    extracted = extract_text(b"  Return\n\npolicy\t details  ", "policy.md", "text/markdown")

    assert normalize_text(extracted) == "Return policy details"
    assert content_hash("Return policy details") == content_hash("Return policy details")
    assert content_hash("Return policy details") != content_hash("Other policy")


def test_clean_text_preserves_line_breaks_and_structure():
    raw = "Line 1  \r\n\r\nLine 2 \t \n- Item 1\n- Item 2"
    cleaned = clean_text(raw)
    assert cleaned == "Line 1\n\nLine 2\n- Item 1\n- Item 2"


def test_utf8_bom_is_handled():
    source = b"\xef\xbb\xbfHello with BOM"
    extracted = extract_text(source, "file.txt")
    assert extracted == "Hello with BOM"


def test_invalid_utf8_raises_value_error():
    with pytest.raises(ValueError, match="valid UTF-8"):
        extract_text(b"\xff\xfe\x00\x00invalid", "file.txt")


def test_unsupported_documents_are_rejected():
    with pytest.raises(ValueError, match="supported"):
        extract_text(b"binary", "report.docx")


def test_content_type_does_not_override_txt_extension():
    extracted = extract_text(b"plain text", "file.txt", "application/pdf")
    assert extracted == "plain text"


def test_corrupt_pdf_raises_value_error():
    with pytest.raises(ValueError, match="could not be read"):
        extract_text(b"not a valid pdf", "corrupt.pdf")


def test_encrypted_pdf_raises_value_error(monkeypatch):
    class MockPdfReader:
        def __init__(self, _):
            self.is_encrypted = True

    monkeypatch.setattr("pypdf.PdfReader", MockPdfReader)
    with pytest.raises(ValueError, match="Encrypted"):
        extract_text(b"%PDF-mock", "secret.pdf")


def test_chunking_overlap_does_not_start_mid_word():
    text = " ".join(f"word{index}" for index in range(500))
    chunks = chunk_text(text)

    assert len(chunks) > 1
    assert all(len(chunk) <= CHUNK_SIZE for chunk in chunks)

    # First word of chunk 1 should be a complete word, not a fragment
    first_word_chunk1 = chunks[1].split()[0]
    assert first_word_chunk1.startswith("word")
    assert chunks[0].find(first_word_chunk1) != -1

    all_words = set(text.split())
    covered_words = set(" ".join(chunks).split())
    assert all_words == covered_words


def test_chunking_empty_or_whitespace():
    assert chunk_text("") == []
    assert chunk_text("   \n\t  ") == []
