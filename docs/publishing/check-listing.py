#!/usr/bin/env python3
"""Checks the submission pack against the store's limits.

Everything here is measured from the files themselves. Nothing is taken from
a build log, and no count is estimated.

Copy into the game as docs/publishing/check-listing.py, adjust the CONFIG block,
and run from the project root:  python3 docs/publishing/check-listing.py
"""

import re
import struct
import sys
import zlib
from pathlib import Path

# ---- CONFIG --------------------------------------------------------------

# Where the art lives, relative to this file.
ICON = "art/store-icon-512.png"
FEATURE = "art/feature-graphic-1024x500.png"
SCREENSHOT_GLOB = "art/screenshots/**/*.png"

# Privacy pages to check, relative to this file. One per locale.
PRIVACY_PAGES = ["privacy/index.html"]

# Markdown scanned for leftover {{TOKEN}} markers, relative to this file.
# The second entry reaches the docs folder this one sits inside, so a
# half-filled CONTRACT.md fails the same way a half-filled listing does.
DOC_GLOBS = ["*.md", "../*.md"]

# Words the full description must not repeat. Keyword-stuffing a genre term,
# especially one that is someone's trademark, is what gets a listing pulled —
# the Threes takedown was triggered by repeating "2048" through a listing.
# Example: {"2048": 1} allows at most one mention.
KEYWORD_CAPS = {}

# ---- end CONFIG ----------------------------------------------------------

PUB = Path(__file__).parent
PROBLEMS = []
NOTES = []


def problem(msg):
    PROBLEMS.append(msg)


def note(msg):
    NOTES.append(msg)


# ---- store text ----------------------------------------------------------

LIMITS = {"App name": 30, "Short description": 80, "Full description": 4000,
          "Release notes": 500}


def blocks(path):
    """Returns {heading: fenced block} for one markdown file."""
    text = path.read_text(encoding="utf-8")
    found = {}
    for match in re.finditer(r"^### (.+?)\n+```\n(.*?)\n```", text, re.S | re.M):
        found[match.group(1).strip()] = match.group(2)
    return found


def check_text():
    listing = PUB / "store-listing.md"
    if not listing.exists():
        problem(f"{listing} is missing")
        return {}

    found = blocks(listing)
    for field, limit in LIMITS.items():
        if field not in found:
            problem(f"store-listing.md has no '{field}' block")
            continue

        value = found[field]
        count = len(value)
        if count == 0:
            problem(f"{field} is empty")
        elif count > limit:
            problem(f"{field} is {count} characters, limit {limit} (over by {count - limit})")
        else:
            note(f"{field:18s} {count:>5d} / {limit} characters")

    full = found.get("Full description", "")
    for word, cap in KEYWORD_CAPS.items():
        hits = len(re.findall(re.escape(word), full))
        if hits > cap:
            problem(f"Full description mentions {word} {hits} times; keep it to at most {cap}")
        else:
            note(f"'{word}' in full description: {hits} (keyword-stuffing safe)")

    return found


# ---- images --------------------------------------------------------------


def png_size(path):
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    w, h = struct.unpack(">II", data[16:24])
    return w, h, data[24], data[25]   # width, height, bit depth, colour type


def decode_alpha(path):
    """Returns the minimum alpha value, or None when the file has no alpha."""
    data = path.read_bytes()
    pos, idat, w, h, depth, ctype = 8, b"", 0, 0, 0, 0
    while pos < len(data):
        length = struct.unpack(">I", data[pos:pos + 4])[0]
        kind = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + length]
        if kind == b"IHDR":
            w, h, depth, ctype = struct.unpack(">IIBB", chunk[:10])
        elif kind == b"IDAT":
            idat += chunk
        elif kind == b"IEND":
            break
        pos += 12 + length

    if ctype != 6 or depth != 8:
        return None

    raw = zlib.decompress(idat)
    bpp, stride = 4, w * 4
    prev = bytearray(stride)
    lowest = 255
    i = 0
    for _ in range(h):
        filt = raw[i]; i += 1
        line = bytearray(raw[i:i + stride]); i += stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if filt == 1:
                line[x] = (line[x] + a) & 255
            elif filt == 2:
                line[x] = (line[x] + b) & 255
            elif filt == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif filt == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 255
        lowest = min(lowest, min(line[3::4]))
        prev = line
    return lowest


def check_images():
    for rel, expected in ((ICON, (512, 512)), (FEATURE, (1024, 500))):
        path = PUB / rel
        if not path.exists():
            problem(f"{path} is missing")
            continue
        size = png_size(path)
        if size is None:
            problem(f"{path} is not a PNG")
            continue
        w, h = size[0], size[1]
        if (w, h) != expected:
            problem(f"{path.name} is {w}x{h}, expected {expected[0]}x{expected[1]}")
        else:
            note(f"{path.name:34s} {w}x{h}")

        # A colour type of 6 is not transparency; only pixel values decide.
        lowest = decode_alpha(path)
        if lowest is not None and lowest < 255:
            problem(f"{path.name} has real transparency (min alpha {lowest}); "
                    "the store rejects transparent icons")
        elif lowest is not None:
            note(f"{path.name:34s} alpha channel fully opaque — store-safe")

    shots = sorted(PUB.glob(SCREENSHOT_GLOB))
    if not 2 <= len(shots) <= 8:
        problem(f"{len(shots)} screenshots; the store takes 2 to 8 per locale")
    else:
        note(f"screenshots{'':23s} {len(shots)} files")

    for shot in shots:
        size = png_size(shot)
        if size is None:
            problem(f"{shot} is not a PNG")
            continue
        w, h = size[0], size[1]
        if min(w, h) < 320 or max(w, h) > 3840:
            problem(f"{shot.name} is {w}x{h}; each side must be 320..3840")
        if max(w, h) / min(w, h) > 2.0:
            problem(f"{shot.name} ratio {max(w, h) / min(w, h):.2f}:1 exceeds 2:1")


# ---- privacy policy ------------------------------------------------------


def check_privacy():
    for rel in PRIVACY_PAGES:
        page = PUB / rel
        if not page.exists():
            problem(f"{page} is missing")
            continue

        html = page.read_text(encoding="utf-8")

        # Any {{TOKEN}} left means the page still carries template text.
        leftover = set(re.findall(r"\{\{[A-Z_]+\}\}", html))
        if leftover:
            problem(f"{rel} still contains {', '.join(sorted(leftover))}")

        if not re.search(r"[\w.+-]+@[\w-]+\.[\w.]+", html):
            problem(f"{rel} carries no contact address; one is required")

        # Self-contained: a web font or CDN script turns a dead host into a
        # broken policy page, and the URL may not 404 after publication.
        if re.search(r'(src|href)\s*=\s*["\']https?://', html):
            problem(f"{rel} loads an external resource; it must be self-contained")
        else:
            note(f"{rel:34s} self-contained, no external resources")


def check_templates():
    """A half-filled template reads as a finished document. Catch it here."""
    seen = set()
    for pattern in DOC_GLOBS:
        for doc in sorted(PUB.glob(pattern)):
            if doc in seen:
                continue
            seen.add(doc)
            label = doc.relative_to(PUB) if PUB in doc.parents else doc.name
            leftover = set(re.findall(r"\{\{[A-Z_]+\}\}",
                                      doc.read_text(encoding="utf-8")))
            if leftover:
                problem(f"{label} still carries {len(leftover)} template token(s): "
                        f"{', '.join(sorted(leftover)[:4])}"
                        f"{' …' if len(leftover) > 4 else ''}")
            else:
                note(f"{str(label):34s} no template tokens left")


def main():
    check_text()
    check_images()
    check_privacy()
    check_templates()

    for line in NOTES:
        print(f"  ok   {line}")
    print()
    if PROBLEMS:
        for line in PROBLEMS:
            print(f"  FAIL {line}")
        print(f"\n{len(PROBLEMS)} problem(s).")
        return 1

    print("Submission pack passes every measurable check.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
