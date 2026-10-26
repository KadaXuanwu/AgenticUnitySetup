# Agentic Unity Setup

A kit that starts a new Unity URP project with the working agreement and the tools that let a coding agent
(Claude Code) build, verify and document the game on its own, while a human designer sets direction and balance.

The kit does not fix a Unity version. It is made and tested for **Unity 6.7 (6000.7)**. On another Unity 6 version,
the agent checks each step and corrects the docs where Unity differs.

It does not set up agents that work in parallel (a work queue, workers, a merger). It sets up one agent and one
designer.

## What you get

- **Repo layout.** One git repo: the docs at the root, the Unity project in `Unity/`.
- **Asset management.** Git LFS for every binary type. Only `Assets/<Game>/` goes into git. Asset Store packages stay
  out of git and come back from the local download cache with one editor script.
- **Code style.** K&R C#, explicit types, access modifiers, in an `.editorconfig` that the IDE enforces.
- **Working agreement.** A `CLAUDE.md`: Simplified Technical English, one word for one meaning (a glossary), the
  designer and engineer roles, reports for a designer, git and doc rules.
- **Design Hub.** One editor window that shows every number the designer tunes, by topic.
- **Knowledge base.** `AI/` docs that the agent keeps: architecture, decisions, automation, packages, data, to-do,
  release. `Docs/DESIGN-DECISIONS.md` for every design decision.
- **Self-verification.** A headless compile, and MCP for Unity so the agent drives the editor: compile, console, tests,
  play mode. A list of the traps in that loop.
- **Audio.** FMOD for Unity, an FMOD Studio project in the repo, banks in LFS, and a Studio script that writes every
  event name as C# on each bank build.

## How to use it

1. Install:
   - a Unity 6 editor (6.7 is the version the kit is made for),
   - [Git LFS](https://git-lfs.com) (`git lfs install` once per machine),
   - [uv](https://docs.astral.sh/uv/) (Claude Code starts the Unity MCP server with `uvx`),
   - the .NET SDK 9 or later,
   - FMOD Studio at the minor version of the FMOD for Unity package you will install.
2. Make an empty git repo for the new game.
3. In Unity Hub, make a new project from the **Universal 3D** or the **Universal 2D** template. Make the `Unity`
   folder in that repo the project folder. Unity Hub adds the project name as a subfolder (`Unity/<Name>/`); the agent
   moves the project up into `Unity/` while the editor is closed. If Unity Hub offers to start git or version control
   for the project, say no.
4. Open Claude Code in the repo root.
5. Open [PROMPT.md](PROMPT.md), fill in the two lines at the top, and paste all of it as your first message.

The agent copies `template/` into the repo, puts your game's name in, moves the URP template's files into place,
installs the packages and verifies each step. It stops and asks you for each step that needs a human (listed in the
prompt).

## The placeholders

Every `__GAME__` in `template/`, in file contents and in folder and file names, is the game's code name: one word
in PascalCase, for example `StormHarbor`. It is also a valid C# identifier, so a template file that still has it
compiles. `__UNITY__` is the Unity version line (from `ProjectVersion.txt`), `__DATE__` is the setup date, and
`__RENDER__` names the template (`3D URP` or `2D URP (Universal 2D template)`). The agent replaces all four in step 1
of the prompt.
