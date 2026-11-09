"""PostToolUse hook: after Claude edits a C# file, run rung 1 of AI/AUTOMATION.md (the headless compile).

Claude Code starts it from .claude/settings.json with `uv run`. A clean build prints nothing. A failed build, or a file
that no generated .csproj lists yet, exits with 2: Claude Code then shows the message to Claude.
"""
import glob
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY = os.path.join(ROOT, "Unity")
SOLUTION = os.path.join(UNITY, "Unity.slnx")
ERROR = re.compile(r": error [A-Z]+\d+:")


def edited_file():
    try:
        event = json.load(sys.stdin)
    except ValueError:
        return None
    return (event.get("tool_input") or {}).get("file_path")


# Unity lists each script in its .csproj, so a new or moved file compiles only after Unity regenerates them.
def listed(path):
    try:
        relative = os.path.relpath(os.path.abspath(path), UNITY)
    except ValueError:
        return True
    if relative.startswith(".."):
        return True
    needle = ('include="' + relative + '"').replace("/", "\\").lower()
    for project in glob.glob(os.path.join(UNITY, "*.csproj")):
        with open(project, encoding="utf-8", errors="replace") as f:
            if needle in f.read().replace("/", "\\").lower():
                return True
    return False


def main():
    path = edited_file()
    if not path or not path.lower().endswith(".cs"):
        return 0
    if not os.path.exists(SOLUTION):
        return 0
    if not listed(path):
        print("Rung 1 skipped: no generated .csproj lists " + os.path.basename(path) + " yet. "
              "Run refresh_unity, then read_console (AI/AUTOMATION.md, rung 1 caveats).", file=sys.stderr)
        return 2
    try:
        build = subprocess.run(
            ["dotnet", "build", SOLUTION, "-v", "q", "--nologo", "-tl:off", "-clp:ErrorsOnly"],
            cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    except OSError:
        print("Rung 1 skipped: dotnet is not on the PATH. Install the .NET SDK 9 or later.", file=sys.stderr)
        return 2
    if build.returncode == 0:
        return 0
    errors = []
    for line in (build.stdout + build.stderr).splitlines():
        line = line.strip()
        if ERROR.search(line) and line not in errors:
            errors.append(line)
    report = "\n".join(errors[:30]) or (build.stdout + build.stderr).strip()[-3000:]
    print("Rung 1 failed (dotnet build):\n" + report, file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
