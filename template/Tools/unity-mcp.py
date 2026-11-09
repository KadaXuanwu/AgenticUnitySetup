"""Starts the MCP for Unity server for Claude Code (.mcp.json), at the release of the Unity package in this project.

The Unity package tracks the latest stable release (`#main` in Unity/Packages/manifest.json), and the server has to be
the same release. This reads the version of the installed package and starts that server with uvx, so no file holds a
version number. Before Unity has resolved the package (a fresh clone), it starts the latest stable server.
"""
import glob
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKAGE = "com.coplaydev.unity-mcp"
SERVER = "mcpforunityserver"


def package_version():
    unity = os.path.join(ROOT, "Unity")
    candidates = [os.path.join(unity, "Packages", PACKAGE, "package.json")]
    cached = glob.glob(os.path.join(unity, "Library", "PackageCache", PACKAGE + "@*", "package.json"))
    candidates += sorted(cached, key=os.path.getmtime, reverse=True)
    for path in candidates:
        try:
            with open(path, encoding="utf-8") as f:
                return json.load(f)["version"]
        except (OSError, ValueError, KeyError):
            continue
    return None


def main():
    version = package_version()
    if version is None:
        source = ["--from", SERVER]
    elif "-" in version:
        # A pre-release package (the beta branch): the package itself asks for the newest pre-release server.
        source = ["--prerelease", "explicit", "--from", SERVER + ">=0.0.0a0"]
    else:
        source = ["--from", SERVER + "==" + version]
    try:
        return subprocess.call(["uvx"] + source + ["mcp-for-unity", "--transport", "stdio"])
    except OSError:
        print("uvx is not on the PATH. Install uv: https://docs.astral.sh/uv/", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
