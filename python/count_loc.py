#!/usr/bin/env python3
"""Count C# source lines for each directory below a repository root.

By default, each row includes all ``.cs`` files in that directory and its
descendants. Pass ``--direct`` to count only files located directly in each
directory. Build-output and source-control directories are skipped.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from pathlib import Path
import sys
from typing import Iterable


EXCLUDED_DIRECTORIES = frozenset(
    {
        ".git",
        ".vs",
        "artifacts",
        "bin",
        "obj",
    }
)


@dataclass(frozen=True)
class LineCounts:
    files: int = 0
    code: int = 0
    comments: int = 0
    blank: int = 0

    @property
    def total(self) -> int:
        return self.code + self.comments + self.blank

    def __add__(self, other: LineCounts) -> LineCounts:
        return LineCounts(
            self.files + other.files,
            self.code + other.code,
            self.comments + other.comments,
            self.blank + other.blank,
        )


@dataclass
class LexerState:
    block_comment: bool = False
    verbatim_string: bool = False
    raw_string_quotes: int = 0


def parse_arguments(arguments: Iterable[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Count C# code, comment, blank, and physical lines by directory."
    )
    parser.add_argument(
        "root",
        nargs="?",
        default=".",
        help="root directory to scan (default: current directory)",
    )
    parser.add_argument(
        "--direct",
        action="store_true",
        help="count only files directly in each directory instead of recursive totals",
    )
    return parser.parse_args(arguments)


def is_excluded(path: Path, root: Path) -> bool:
    return any(part.casefold() in EXCLUDED_DIRECTORIES for part in path.relative_to(root).parts)


def find_csharp_files(root: Path) -> list[Path]:
    return sorted(
        (
            path
            for path in root.rglob("*.cs")
            if path.is_file() and not is_excluded(path, root)
        ),
        key=lambda path: path.relative_to(root).as_posix().casefold(),
    )


def count_file(path: Path) -> LineCounts:
    state = LexerState()
    code = 0
    comments = 0
    blank = 0

    with path.open("r", encoding="utf-8-sig", newline=None) as source:
        for line in source:
            classification = classify_line(line, state)
            if classification == "code":
                code += 1
            elif classification == "comment":
                comments += 1
            else:
                blank += 1

    return LineCounts(files=1, code=code, comments=comments, blank=blank)


def classify_line(line: str, state: LexerState) -> str:
    """Classify one C# physical line while retaining multiline lexical state."""

    index = 0
    has_code = state.verbatim_string or state.raw_string_quotes > 0
    has_comment = state.block_comment

    while index < len(line):
        if state.block_comment:
            end = line.find("*/", index)
            if end < 0:
                break
            state.block_comment = False
            index = end + 2
            continue

        if state.raw_string_quotes:
            has_code = True
            delimiter = '"' * state.raw_string_quotes
            end = line.find(delimiter, index)
            if end < 0:
                break
            state.raw_string_quotes = 0
            index = end + len(delimiter)
            continue

        if state.verbatim_string:
            has_code = True
            if line[index] != '"':
                index += 1
                continue
            if index + 1 < len(line) and line[index + 1] == '"':
                index += 2
                continue
            state.verbatim_string = False
            index += 1
            continue

        character = line[index]
        if character.isspace():
            index += 1
            continue

        if line.startswith("//", index):
            has_comment = True
            break

        if line.startswith("/*", index):
            has_comment = True
            state.block_comment = True
            index += 2
            continue

        if line.startswith('@"', index):
            has_code = True
            state.verbatim_string = True
            index += 2
            continue

        if line.startswith('@$"', index):
            has_code = True
            state.verbatim_string = True
            index += 3
            continue

        if character == '"':
            quote_count = 1
            while index + quote_count < len(line) and line[index + quote_count] == '"':
                quote_count += 1
            has_code = True
            if quote_count >= 3:
                state.raw_string_quotes = quote_count
                index += quote_count
            else:
                index = skip_regular_literal(line, index + 1, '"')
            continue

        if character == "'":
            has_code = True
            index = skip_regular_literal(line, index + 1, "'")
            continue

        has_code = True
        index += 1

    if has_code:
        return "code"
    if has_comment:
        return "comment"
    return "blank"


def skip_regular_literal(line: str, index: int, delimiter: str) -> int:
    while index < len(line):
        if line[index] == "\\":
            index += 2
        elif line[index] == delimiter:
            return index + 1
        else:
            index += 1
    return index


def aggregate(root: Path, files: Iterable[Path], direct: bool) -> dict[Path, LineCounts]:
    totals: dict[Path, LineCounts] = {}
    for path in files:
        counts = count_file(path)
        directory = path.parent
        while True:
            totals[directory] = totals.get(directory, LineCounts()) + counts
            if direct or directory == root:
                break
            directory = directory.parent
    return totals


def display_path(path: Path, root: Path) -> str:
    relative = path.relative_to(root).as_posix()
    return "." if relative == "." else relative


def print_table(root: Path, totals: dict[Path, LineCounts]) -> None:
    rows = sorted(totals.items(), key=lambda item: display_path(item[0], root).casefold())
    if not rows:
        print("No C# files found.")
        return

    labels = [display_path(path, root) for path, _ in rows]
    directory_width = max(len("Directory"), *(len(label) for label in labels))
    number_widths = {
        heading: max(len(heading), *(len(f"{getattr(counts, attribute):,}") for _, counts in rows))
        for heading, attribute in (
            ("Files", "files"),
            ("Code", "code"),
            ("Comments", "comments"),
            ("Blank", "blank"),
            ("Total", "total"),
        )
    }

    ordered_headings = tuple(number_widths)
    headings = "  ".join(
        [
            f"{'Directory':<{directory_width}}",
            *(f"{heading:>{number_widths[heading]}}" for heading in ordered_headings),
        ]
    )
    print(headings)
    print(
        "  ".join(
            [
                "-" * directory_width,
                *("-" * number_widths[heading] for heading in ordered_headings),
            ]
        )
    )
    for label, (_, counts) in zip(labels, rows):
        print(
            "  ".join(
                [
                    f"{label:<{directory_width}}",
                    f"{counts.files:>{number_widths['Files']},}",
                    f"{counts.code:>{number_widths['Code']},}",
                    f"{counts.comments:>{number_widths['Comments']},}",
                    f"{counts.blank:>{number_widths['Blank']},}",
                    f"{counts.total:>{number_widths['Total']},}",
                ]
            )
        )


def main(arguments: Iterable[str]) -> int:
    options = parse_arguments(arguments)
    root = Path(options.root).resolve()
    if not root.is_dir():
        print(f"error: root directory does not exist: {root}", file=sys.stderr)
        return 2

    try:
        files = find_csharp_files(root)
        totals = aggregate(root, files, options.direct)
    except (OSError, UnicodeError) as error:
        print(f"error: unable to count C# source: {error}", file=sys.stderr)
        return 1

    print_table(root, totals)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
