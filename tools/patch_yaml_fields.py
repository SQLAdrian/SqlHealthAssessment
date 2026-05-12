#!/usr/bin/env python3
"""
patch_yaml_fields.py — Add missing editorial fields to corpus YAMLs.

Fields managed (inserted after `priority:` if absent):
  Tri_impact: Reliability, Performance, Risks
  bad: 0
  CheckNr:
  RedoYAMLCheck: 0
  UpdateCheckFileName: 0

Rules:
  - Never overwrite an existing value.
  - Insert missing fields as a block immediately after the `priority:` line.
  - If `priority:` is absent, insert after `category:`.
  - If neither, insert after the first line (after check_id/title).
  - Dry-run by default; pass --write to commit changes.
  - Prints a summary at the end.
"""

import argparse
import os
import re
import sys

CORPUS_DIR = r"c:\GitHub\LiveMonitor\research_output\LLM1_deepseek"

# Fields to ensure exist, in insertion order.
# value=None means emit the key with no value (bare key).
FIELDS = [
    ("Tri_impact", "Reliability, Performance, Risks"),
    ("bad", "0"),
    ("CheckNr", None),
    ("RedoYAMLCheck", "0"),
    ("UpdateCheckFileName", "0"),
]

# Regex: matches a top-level YAML key at the start of a line.
KEY_RE = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*):")


def field_present(lines: list[str], key: str) -> bool:
    """Return True if the key exists as a top-level YAML field."""
    prefix = f"{key}:"
    return any(line.startswith(prefix) for line in lines)


def render_field(key: str, value) -> str:
    if value is None or value == "":
        return f"{key}:\n"
    return f"{key}: {value}\n"


def find_insertion_point(lines: list[str]) -> int:
    """
    Return the index AFTER which missing fields should be inserted.
    Priority: after `priority:` > after `category:` > after line 0.
    """
    for anchor in ("priority", "category"):
        for i, line in enumerate(lines):
            if line.startswith(f"{anchor}:"):
                return i  # insert immediately after this index
    return 0  # fallback: after first line


def patch_file(path: str, dry_run: bool) -> tuple[int, int]:
    """
    Returns (fields_added, fields_already_present).
    """
    with open(path, "r", encoding="utf-8") as fh:
        lines = fh.readlines()

    missing = [(k, v) for k, v in FIELDS if not field_present(lines, k)]
    if not missing:
        return 0, len(FIELDS)

    insert_after = find_insertion_point(lines)
    new_lines_to_insert = [render_field(k, v) for k, v in missing]

    patched = lines[: insert_after + 1] + new_lines_to_insert + lines[insert_after + 1 :]

    if not dry_run:
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.writelines(patched)

    return len(missing), len(FIELDS) - len(missing)


def main():
    parser = argparse.ArgumentParser(description="Patch missing editorial fields into corpus YAMLs.")
    parser.add_argument("--write", action="store_true", help="Commit changes (default: dry-run).")
    parser.add_argument("--dir", default=CORPUS_DIR, help="Corpus directory to process.")
    args = parser.parse_args()

    dry_run = not args.write
    corpus_dir = args.dir

    if not os.path.isdir(corpus_dir):
        print(f"ERROR: Directory not found: {corpus_dir}", file=sys.stderr)
        sys.exit(1)

    yaml_files = sorted(f for f in os.listdir(corpus_dir) if f.endswith(".yaml"))
    if not yaml_files:
        print("No .yaml files found.", file=sys.stderr)
        sys.exit(1)

    total_files = len(yaml_files)
    patched_count = 0
    already_complete = 0
    total_fields_added = 0

    for fname in yaml_files:
        path = os.path.join(corpus_dir, fname)
        added, present = patch_file(path, dry_run=dry_run)
        if added > 0:
            patched_count += 1
            total_fields_added += added
            if not dry_run:
                print(f"  patched  {fname}  (+{added} fields)")
            else:
                print(f"  would patch  {fname}  (+{added} fields)")
        else:
            already_complete += 1

    mode = "DRY RUN — no files written" if dry_run else "WRITE MODE"
    print(f"\n{'='*60}")
    print(f"  {mode}")
    print(f"  Total files     : {total_files}")
    print(f"  Already complete: {already_complete}")
    print(f"  Files patched   : {patched_count}")
    print(f"  Fields added    : {total_fields_added}")
    if dry_run:
        print(f"\n  Re-run with --write to apply changes.")
    print(f"{'='*60}")


if __name__ == "__main__":
    main()
