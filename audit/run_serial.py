import subprocess, os
PI = r"C:\Users\Nuctori\AppData\Roaming\npm\pi.cmd"
if not os.path.exists(PI):
    PI = "pi.cmd"
PROMPTS = "D:/Godot/Cosmos/audit/prompts"
OUT = "D:/Godot/Cosmos/audit"
for n in range(13, 21):
    pid = f"{n:02d}"
    prompt = open(f"{PROMPTS}/iter{pid}.txt", encoding="utf-8").read()
    try:
        r = subprocess.run(
            [PI, "--no-extensions", "--no-skills", "--no-prompt-templates",
             "--provider", "opencode-go", "--model", "ox-alpha-free", "-p", prompt],
            capture_output=True, text=True, encoding="utf-8", errors="replace",
            timeout=1500)
        out, err, code = r.stdout, r.stderr, r.returncode
    except subprocess.TimeoutExpired:
        out, err, code = "", "TIMEOUT", -1
    done = os.path.exists(f"{OUT}/iter{pid}.md")
    print(f"iter{pid}: exit={code} file={'YES' if done else 'NO'} out={out.strip()[:50]!r} err={err.strip()[:80]!r}", flush=True)
