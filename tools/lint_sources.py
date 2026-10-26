#!/usr/bin/env python3
"""Hygiene checks against the code that actually ships.

A plain grep over .cs files cannot see preprocessor guards, so it reports editor-only
code as shipping. That produces exactly the wrong reaction: either the real finding is
drowned in noise, or someone widens the exclusion until the check stops working.

So this strips #if UNITY_EDITOR / UNITY_INCLUDE_TESTS regions first, then greps what
is left. Usage: lint_sources.py <file>...
"""
import re
import sys

EDITOR_SYMBOLS = ("UNITY_EDITOR", "UNITY_INCLUDE_TESTS")

RULES = [
    ("debug logging", re.compile(r"\bDebug\.Log(Warning|Error|Format)?\s*\(")),
    ("scaffolding marker", re.compile(r"(?<![A-Za-z])(TODO|FIXME|HACK|XXX)(?![A-Za-z])")),
    ("commented-out code", re.compile(r"^\s*//\s*(if|for|while|return|var|public|private|void)\s")),
]


def is_editor_condition(condition: str) -> bool:
    """True for a guard that keeps the block out of a player build."""
    condition = condition.strip()
    if "!" in condition:
        return False
    return any(symbol in condition for symbol in EDITOR_SYMBOLS)


def shipping_lines(text: str):
    """Yields (line_number, line) for lines that survive into a player build."""
    # Depth of nested #if blocks we are discarding; 0 means the code is shipping.
    suppressed_at = None
    depth = 0

    for number, line in enumerate(text.splitlines(), start=1):
        stripped = line.strip()

        if stripped.startswith("#if"):
            depth += 1
            if suppressed_at is None and is_editor_condition(stripped[3:]):
                suppressed_at = depth
            continue

        if stripped.startswith("#elif") or stripped.startswith("#else"):
            # The other branch of an editor-only guard does ship, so stop suppressing.
            if suppressed_at == depth:
                suppressed_at = None
            continue

        if stripped.startswith("#endif"):
            if suppressed_at == depth:
                suppressed_at = None
            depth = max(0, depth - 1)
            continue

        if suppressed_at is None:
            yield number, line


def main(paths) -> int:
    findings = []
    for path in paths:
        try:
            with open(path, encoding="utf-8") as handle:
                text = handle.read()
        except OSError as error:
            print(f"cannot read {path}: {error}", file=sys.stderr)
            return 2

        for number, line in shipping_lines(text):
            for label, pattern in RULES:
                if pattern.search(line):
                    findings.append((label, path, number, line.strip()))

    for label, path, number, line in findings:
        print(f"  {label}: {path}:{number}: {line[:110]}")

    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
