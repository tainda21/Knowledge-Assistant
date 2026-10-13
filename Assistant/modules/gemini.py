"""Gemini request module.

This module owns provider-specific client configuration, request translation,
and token streaming so the FastAPI entrypoint only handles HTTP/SSE concerns.
"""

from __future__ import annotations

import os
from collections.abc import Iterator
from functools import lru_cache

from google import genai

from core.prompts import SYSTEM_INSTRUCTION


@lru_cache
def get_genai_client():
    """Create and cache the configured Gemini client for this process."""
    api_key = os.getenv("GEMINI_API_KEY")
    if not api_key:
        raise RuntimeError("GEMINI_API_KEY is not configured")
    return genai.Client(
        api_key=api_key,
        http_options={"api_version": os.getenv("GEMINI_API_VERSION", "v1beta")},
    )


def to_gemini_contents(messages: list[dict[str, str]]) -> list[dict]:
    """Translate provider-neutral chat messages to Gemini's request shape."""
    return [
        {
            "role": "model" if message["role"] == "assistant" else "user",
            "parts": [{"text": message["content"]}],
        }
        for message in messages
    ]


def stream_text(messages: list[dict[str, str]]) -> Iterator[str]:
    """Yield generated text chunks for a provider-neutral transcript."""
    response_stream = get_genai_client().models.generate_content_stream(
        model=os.getenv("GEMINI_MODEL", "gemini-3.6-flash"),
        contents=to_gemini_contents(messages),
        config={"system_instruction": SYSTEM_INSTRUCTION},
    )
    for response in response_stream:
        if response.text:
            yield response.text
