"""Inspect recent interrupted pi CLI sessions to see how far iter10 got."""

import glob
import json
import os

base = r"C:\Users\Nuctori\.pi\agent\sessions\--D--Godot-Cosmos--"


def load_text(path: str) -> str:
    try:
        with open(path, encoding="utf-8", errors="replace") as fh:
            return fh.read()
    except OSError as exc:
        print("read-failed:", path, exc)
        return ""


def safe_role(entry: dict) -> str:
    message = entry.get("message") or {}
    role = message.get("role") or entry.get("type")
    return str(role) if role else "?"


def main() -> None:
    files = sorted(
        glob.glob(os.path.join(base, "2026-08-25T*.jsonl")),
        key=os.path.getmtime,
        reverse=True,
    )
    print("candidates:", len(files))
    for f in files[:8]:
        text = load_text(f)
        lines = text.splitlines()
        roles = []
        for ln in lines:
            if not ln.strip().startswith("{"):
                continue
            parsed = None
            try:
                parsed = json.loads(ln)
            except ValueError:
                parsed = None
            if parsed:
                roles.append(safe_role(parsed))
        has_write = '"name":"write"' in text or '"name": "write"' in text
        print(
            os.path.basename(f),
            "| entries:",
            len(lines),
            "| iter10:",
            "iter10" in text,
            "| write-call:",
            has_write,
            "| tail roles:",
            roles[-4:],
        )


if __name__ == "__main__":
    main()
