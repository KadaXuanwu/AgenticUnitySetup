# __GAME__

<One-paragraph pitch.>

## Getting started

1. Install the Unity version named in `Unity/ProjectSettings/ProjectVersion.txt`.
2. **Install [Git LFS](https://git-lfs.com) before cloning** (`git lfs install` once per machine). Without it every
   texture, model, sound and DLL arrives as a ~130-byte stub; `git lfs pull` repairs a bad clone.
3. Open `Unity/` in Unity Hub. Asset Store packages come back by themselves from your download cache; a missing one
   logs a warning with its store link (AI/PACKAGES.md).
4. To change sounds, install the FMOD Studio version named in AI/PACKAGES.md and open
   `Audio/__GAME__/__GAME__.fspro`. To play, you do not need it: the built banks are in git.

### Claude Code on a fresh clone

`CLAUDE.md`, `AI/` and `.claude/settings.json` are committed, so the working agreement and the knowledge base come
with the repo. The one thing that does not travel is the Unity MCP client registration: after the project imports,
open **Window > MCP for Unity** and let it configure your client. Without it everything still works, minus driving
the editor headlessly.

```bash
dotnet build Unity/Unity.slnx -v q --nologo
```

compiles the project without the editor (.NET SDK). The rest of the verification ladder is in
[AI/AUTOMATION.md](AI/AUTOMATION.md).
