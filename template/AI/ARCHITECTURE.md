# Architecture

Unity __UNITY__, __RENDER__, UI Toolkit, Input System. The *why* behind each choice is in [DECISIONS.md](DECISIONS.md).

This file is the single source of truth for where things go. Nothing else repeats it.

## Repo

```
__GAME__/                    git root
├── CLAUDE.md                working agreement for agents
├── README.md                getting started on a fresh clone
├── .claude/settings.json    shared Claude Code permissions; turns on the server in .mcp.json
├── .mcp.json                the Unity MCP server for Claude Code
├── .editorconfig            C# style, enforced by the IDE
├── .gitattributes           LFS by file type; must sit at the root
├── AI/                      agent knowledge base: this file, AUTOMATION, DECISIONS, …
├── Audio/__GAME__/          the FMOD Studio project; Scripts/names.js writes Fmod.cs on each bank build
├── Docs/                    design, written by the designer; DESIGN-DECISIONS.md, kept by the agent
└── Unity/                   the Unity project; open this folder in the Hub
```

Unity's `.gitignore` lives in `Unity/` and applies from there. `.gitattributes` has to sit at the root — git ignores
`[attr]` macros anywhere else, which silently disables LFS.

## Assets

Everything of ours lives in `Assets/__GAME__/`, and git commits nothing else under `Assets/` (`Unity/.gitignore`
whitelists that one folder, and FMOD's settings file). Asset Store packages land at the root beside it, wherever their
publisher put them, and are restored from `Unity/AssetStore.json` ([PACKAGES.md](PACKAGES.md)). Never put our own work
inside a vendor folder; copy the file into ours first.

```
Assets/__GAME__/
├── Scenes/                  the game's scenes
├── Settings/                URP assets and volume profiles
├── InputSystem_Actions      the project-wide input actions
├── Runtime/                 [asmdef __GAME__.Runtime]  the game: gameplay, rendering, input, audio, UI
│   └── Audio/               Fmod.cs (generated names), our audio code
├── Editor/                  [asmdef __GAME__.Editor]  the Design Hub, Asset Store restore, editor tools
├── Tests/EditMode/          [asmdef __GAME__.Tests.EditMode]  tests with no play mode
└── Content/
    ├── Data/                the designer's: every tunable ScriptableObject (DATA.md)
    ├── Tech/                the engineer's: world setup, budgets, wiring between assets; never in the hub
    ├── UI/                  UXML, USS, PanelSettings
    ├── Materials/
    ├── Models/
    └── Prefabs/
```

A folder goes in this tree when its first file lands, not before.

## Assemblies

Dependency runs one way: `Editor → Runtime`, `Tests → both`. `Runtime` never references `Editor`.

If game logic later moves out of Unity (a fixed-tick simulation that a server, a replay or a headless test can run),
it gets its own assembly, `__GAME__.Sim`, with `noEngineReferences: true`, so the compiler keeps `UnityEngine` out.
`Runtime` then reads it and talks back only through commands. Record that choice in DECISIONS.md when it is made.

## Conventions

- **UI Toolkit only** (UXML + USS). No uGUI — text an agent can author end to end beats a canvas hierarchy it
  cannot. If a package needs `com.unity.ugui`, install it for that package alone and say so in PACKAGES.md.
- **New Input System only.** Active Input Handling is the Input System Package, not Both; no `UnityEngine.Input`.
- **Menus stand in full.** Every screen and control the final game has is in the UXML; what is not built yet carries
  `enabled="false"` and shows greyed. Building a feature means enabling and wiring its control.
- Namespaces `__GAME__.<Assembly>` — `__GAME__.Runtime`, `__GAME__.Editor`.
- Balance numbers live in a `ScriptableObject`, never as a literal in a MonoBehaviour (DATA.md). `[Tooltip]` every
  field whose name does not answer the question.
- Prefabs instantiated at runtime carry a `<Prefabname>Refs` component on the root that resolves its own fields in
  `OnValidate` (`GetComponent` / `GetComponentInChildren` / find-child-by-name), so adding a reference costs no
  dragging. Skip it for prefabs spawned in bulk.
- New external dependency → add it to the asmdef `references`, not only a `using`.
- Play mode skips domain and scene reload, and a script reload serializes private fields: a lazily built array or list
  reads empty, not null, after one. Mark such caches `[NonSerialized]`.
- So statics outlive a play session. Every type with static fields says what happens to them (Unity's analyzer warns
  otherwise): `[AutoStaticsCleanup]` (the type `partial`) for state a play leaves behind — audio handles, flags set
  each frame — and `[NoAutoStaticsCleanup]` for read-only tables and caches, or state a component sets and clears
  itself.
- Serialization is ForceText: scenes and assets are diffable YAML. Leave it.
- Data in assets, not values typed into scene objects; UXML, not prefab hierarchies, where either works: an agent
  reads and writes text.
