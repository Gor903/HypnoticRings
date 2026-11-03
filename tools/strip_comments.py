#!/usr/bin/env python3
"""Removes every comment from C# sources, leaving string literals alone.

A regular expression cannot do this. `"http://x"` contains a comment opener and
`@"a // b"` contains one inside a verbatim string; both survive a naive strip as
truncated code that still compiles and means something else. So this walks the
file character by character with exactly enough state to know whether it is
inside a string, a character literal, or code.

    tools/strip_comments.py --check   lists files that still carry comments
    tools/strip_comments.py           rewrites them
"""

import sys
from pathlib import Path

ROOTS = ["Assets/_Project/Scripts", "Assets/_Project/Editor", "Assets/_Project/Tests"]


def strip(text):
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]

        if c == '"':
            # Verbatim strings end at a lone quote; a doubled one is an escaped quote.
            if i and text[i - 1] == '@':
                out.append(c)
                i += 1
                while i < n:
                    if text[i] == '"':
                        if i + 1 < n and text[i + 1] == '"':
                            out.append('""')
                            i += 2
                            continue
                        out.append('"')
                        i += 1
                        break
                    out.append(text[i])
                    i += 1
                continue

            out.append(c)
            i += 1
            while i < n:
                if text[i] == '\\' and i + 1 < n:
                    out.append(text[i:i + 2])
                    i += 2
                    continue
                out.append(text[i])
                if text[i] == '"':
                    i += 1
                    break
                i += 1
            continue

        if c == "'":
            out.append(c)
            i += 1
            while i < n:
                if text[i] == '\\' and i + 1 < n:
                    out.append(text[i:i + 2])
                    i += 2
                    continue
                out.append(text[i])
                if text[i] == "'":
                    i += 1
                    break
                i += 1
            continue

        if c == '/' and i + 1 < n and text[i + 1] == '/':
            while i < n and text[i] != '\n':
                i += 1
            continue

        if c == '/' and i + 1 < n and text[i + 1] == '*':
            i += 2
            while i + 1 < n and not (text[i] == '*' and text[i + 1] == '/'):
                i += 1
            i += 2
            continue

        out.append(c)
        i += 1

    return ''.join(out)


def tidy(text):
    """Drops lines a removed comment left blank, and collapses the gaps it opened."""
    lines = [line.rstrip() for line in text.split('\n')]

    kept, blanks = [], 0
    for line in lines:
        if not line.strip():
            blanks += 1
            continue

        # A blank line that only exists because a comment stood above it reads as deliberate
        # spacing; two in a row never does. One hard against a brace is the gap a removed
        # doc comment left, and it reads as a mistake.
        opened = kept and kept[-1].rstrip().endswith(('{', '('))
        closes = line.strip().startswith(('}', ')'))
        if blanks and kept and not opened and not closes:
            kept.append('')

        blanks = 0
        kept.append(line)

    while kept and not kept[-1]:
        kept.pop()

    return '\n'.join(kept) + '\n'


def files():
    for root in ROOTS:
        for path in sorted(Path(root).rglob('*.cs')):
            yield path


def main():
    check = '--check' in sys.argv
    touched = 0

    for path in files():
        before = path.read_text(encoding='utf-8')
        after = tidy(strip(before))
        if after == before:
            continue

        touched += 1
        if check:
            print(f"  carries comments  {path}")
        else:
            path.write_text(after, encoding='utf-8')

    if check:
        if touched:
            print(f"\n{touched} file(s) still carry comments")
            return 1
        print("  ok    no comments in C# sources")
        return 0

    print(f"stripped {touched} file(s)")
    return 0


if __name__ == '__main__':
    sys.exit(main())
