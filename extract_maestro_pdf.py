import json
import os
import re
import sys
from typing import Dict, List, Optional, Tuple

try:
    from pypdf import PdfReader
    from pypdf.generic import Destination
except Exception:  # pragma: no cover - dependency bootstrap handled at runtime
    PdfReader = None  # type: ignore
    Destination = None  # type: ignore


INPUT_PDF_PATH = "Maestro Editor.pdf"
OUTPUT_TEXT_PATH = "maestro_editor_text.txt"
OUTPUT_OUTLINE_JSON_PATH = "maestro_editor_outline.json"


def fail_with_message(message: str) -> None:
    print(f"ERROR: {message}")
    sys.exit(1)


def extract_text_pages(pdf_path: str) -> List[Dict[str, object]]:
    """Extract text from each page while preserving page order.

    Returns a list of dicts: {"page_number": int (1-based), "text": str}
    """
    if PdfReader is None:
        fail_with_message(
            "Missing dependency 'pypdf'. Please install it before running this script."
        )

    if not os.path.exists(pdf_path):
        fail_with_message(f"PDF not found: {pdf_path}")

    reader = PdfReader(pdf_path)
    pages: List[Dict[str, object]] = []
    for index, page in enumerate(reader.pages):
        try:
            text = page.extract_text() or ""
        except Exception as exc:  # pragma: no cover - edge-case extraction failures
            text = f"[Extraction error on page {index + 1}: {exc}]\n"
        pages.append({"page_number": index + 1, "text": text})
    return pages


def write_text_output(pages: List[Dict[str, object]], output_path: str) -> None:
    """Write a single text file with clear page separators for searchability."""
    lines: List[str] = []
    for page in pages:
        page_number = int(page["page_number"])  # type: ignore[arg-type]
        lines.append("")
        lines.append(f"=== Page {page_number} ===")
        lines.append("")
        # Normalize Windows newlines for consistency
        page_text = str(page["text"]).replace("\r\n", "\n").replace("\r", "\n")
        lines.append(page_text)
    content = "\n".join(lines)
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(content)


def try_extract_pdf_bookmarks(pdf_path: str) -> List[Dict[str, object]]:
    """Attempt to extract PDF bookmarks (outline) using pypdf.

    Returns a flat list of {"title": str, "page_number": int, "level": int}.
    If no bookmarks are present or an error occurs, returns an empty list.
    """
    if PdfReader is None:
        return []

    try:
        reader = PdfReader(pdf_path)
    except Exception:
        return []

    outline_items: List[Dict[str, object]] = []

    def walk(nodes, level: int) -> None:
        if nodes is None:
            return
        for node in nodes:
            try:
                if isinstance(node, list):
                    walk(node, level + 1)
                else:
                    # node can be Destination or Bookmark (Destination-like)
                    title = getattr(node, "title", None) or getattr(node, "/Title", None)
                    dest = None
                    if hasattr(node, "destination"):
                        dest = getattr(node, "destination")
                    elif hasattr(node, "get"):
                        try:
                            dest = node.get("/A", {}).get("/D") or node.get("/Dest")
                        except Exception:  # pragma: no cover - defensive fallback
                            dest = None

                    page_number: Optional[int] = None
                    try:
                        if dest is not None:
                            # reader.get_destination_page_number works with Destination
                            page_number = reader.get_destination_page_number(dest) + 1  # type: ignore[arg-type]
                    except Exception:
                        page_number = None

                    if title is not None and page_number is not None:
                        outline_items.append(
                            {
                                "title": str(title).strip(),
                                "page_number": int(page_number),
                                "level": int(level),
                            }
                        )
            except Exception:  # pragma: no cover - tolerate malformed outlines
                continue

    try:
        # pypdf exposes outlines via .outline or .outlines depending on version
        nodes = getattr(reader, "outline", None)
        if nodes is None:
            nodes = getattr(reader, "outlines", None)
        walk(nodes, level=1)
    except Exception:
        return []

    return outline_items


def heuristic_outline_from_text(pages: List[Dict[str, object]]) -> List[Dict[str, object]]:
    """Infer a basic outline by detecting heading-like lines.

    Heuristics:
    - Lines starting with numeric section patterns: 1, 1.2, 2.3.4 etc.
    - Lines in UPPERCASE with length >= 6 that do not end with punctuation.
    """
    numeric_heading = re.compile(r"^\s*\d{1,2}(?:\.\d{1,3}){0,5}\s+\S")
    uppercase_heading = re.compile(r"^[A-Z0-9][A-Z0-9\s\-_/]{5,}(?:\([^)]*\))?$")

    outline: List[Dict[str, object]] = []
    for page in pages:
        page_number = int(page["page_number"])  # type: ignore[arg-type]
        text = str(page["text"]).replace("\r\n", "\n").replace("\r", "\n")
        for line in text.split("\n"):
            candidate = line.strip()
            if not candidate:
                continue

            is_heading = False
            level = 1
            if numeric_heading.match(candidate):
                is_heading = True
                # Estimate level by number of dots + 1
                level = candidate.split()[0].count(".") + 1
            elif uppercase_heading.match(candidate) and not candidate.endswith(":"):
                is_heading = True
                level = 1

            if is_heading:
                outline.append(
                    {
                        "title": candidate,
                        "page_number": page_number,
                        "level": level,
                    }
                )

    # Deduplicate consecutive identical headings (common in headers)
    deduped: List[Dict[str, object]] = []
    last_key: Optional[Tuple[str, int]] = None
    for item in outline:
        key = (item["title"], item["page_number"])  # type: ignore[index]
        if key == last_key:
            continue
        deduped.append(item)
        last_key = key
    return deduped


def build_outline(pdf_path: str, pages: List[Dict[str, object]]) -> List[Dict[str, object]]:
    """Build an outline using PDF bookmarks if available; otherwise fall back to heuristics."""
    bookmarks = try_extract_pdf_bookmarks(pdf_path)
    if bookmarks:
        return bookmarks
    return heuristic_outline_from_text(pages)


def main() -> None:
    if PdfReader is None:
        fail_with_message(
            "This script requires 'pypdf'. Install it with: pip install pypdf"
        )

    pages = extract_text_pages(INPUT_PDF_PATH)
    write_text_output(pages, OUTPUT_TEXT_PATH)

    outline = build_outline(INPUT_PDF_PATH, pages)
    with open(OUTPUT_OUTLINE_JSON_PATH, "w", encoding="utf-8") as f:
        json.dump({"outline": outline}, f, ensure_ascii=False, indent=2)

    print(
        f"Done. Wrote text to '{OUTPUT_TEXT_PATH}' and outline to '{OUTPUT_OUTLINE_JSON_PATH}'."
    )


if __name__ == "__main__":
    main()


