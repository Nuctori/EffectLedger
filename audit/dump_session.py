"""Dump the tool calls and text of the latest interrupted iter10 session."""

import json

F = (
    r"C:\Users\Nuctori\.pi\agent\sessions\--D--Godot-Cosmos--"
    r"\2026-08-25T02-40-55-581Z_01a036ca-cd1d-781a-a472-d1221d85af3c.jsonl"
)


def load_lines(path: str) -> list:
    try:
        with open(path, encoding="utf-8", errors="replace") as fh:
            return fh.read().splitlines()
    except OSError as exc:
        print("read-failed:", path, exc)
        return []


def main() -> None:
    lines = load_lines(F)
    for i, ln in enumerate(lines):
        parsed = None
        try:
            parsed = json.loads(ln)
        except ValueError:
            continue
        if not parsed:
            continue
        m = parsed.get("message") or {}
        content = m.get("content")
        role = m.get("role") or "?"
        if isinstance(content, list):
            for b in content:
                btype = b.get("type")
                if btype == "text":
                    print(
                        i, role, "TEXT", str(b.get("text", ""))[:150].replace("\n", " ")
                    )
                elif btype == "toolCall":
                    print(i, role, "CALL", b.get("name"), str(b.get("arguments"))[:200])
                elif btype == "toolResult":
                    inner = b.get("output") or b.get("content")
                    print(i, "RESULT", str(inner)[:150].replace("\n", " "))
        elif isinstance(content, str):
            print(i, role, "STR", content[:120].replace("\n", " "))


if __name__ == "__main__":
    main()
