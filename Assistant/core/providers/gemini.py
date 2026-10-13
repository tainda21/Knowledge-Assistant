"""Backward-compatible import location for the Gemini module."""

from modules.gemini import get_genai_client, stream_text, to_gemini_contents

__all__ = ["get_genai_client", "stream_text", "to_gemini_contents"]
