Game code name: <one word, PascalCase, for example StormHarbor>
Kit folder: <full path to the AgenticUnitySetup folder on this machine>

---

You start a new Unity URP game in this repo. I am the game designer; you are the senior engineer. The kit folder
above holds a `template/` folder with the working agreement, the docs, the git and editor configs, and the editor
code. Copy it into this repo and make it work here. Do not refer to the kit folder in anything you write: the repo must
stand alone.

Read `template/CLAUDE.md` first, all of it. From now on its rules apply to you, including its response style. Then
read every file in `template/AI/`.

## Before you start

1. Make sure that the repo root has `Unity/ProjectSettings/ProjectVersion.txt`. If it does not, look for
   `Unity/<Name>/ProjectSettings/`: Unity Hub adds the project name as a subfolder. If you find it and the editor is
   closed, move everything in `Unity/<Name>/` up into `Unity/` and delete the generated `.slnx` and `.csproj` files
   (Unity makes them again). If the editor is open, or you find no project, stop and tell me.
2. Read the editor version from `ProjectVersion.txt`. The kit is made for Unity 6.7 (`6000.7`). If the project uses a
   different version, tell me before you continue. Then expect differences: an API, a setting or a package version
   that is not there (for example the `[AutoStaticsCleanup]` attributes in `Unity.Scripting.LifecycleManagement`). Use
   what this version has, and correct the doc that described the 6.7 way.
3. Find out which URP template made the project. If `Unity/Packages/manifest.json` has `com.unity.2d.*` packages and
   `Assets/Settings` has a `Renderer2D` asset, it is **Universal 2D**; else it is **Universal 3D**. Steps 1, 4 and 6
   differ between the two.
4. If `Unity/.git` exists, Unity Hub made a second repo inside the project. Ask me, then delete it.
5. Make sure that `git lfs version` and `uvx --version` work. If not, stop and tell me to install Git LFS or
   [uv](https://docs.astral.sh/uv/) (the Unity MCP server runs through `uvx`).
6. Make sure that `git config user.name` and `git config user.email` give values. If not, ask me which identity to
   use, and where (this repo or global).
7. Write the steps below into `AI/TODO.md` under a `## Setup` group, as soon as `AI/` exists. Tick each step as it
   is done. I restart Claude Code during the setup, so the next session continues from that list.

## Steps

1. **Copy the template.** Copy everything in `template/` to the repo root, dot files too. If a file already exists,
   ours replaces it, except `Unity/Packages/manifest.json` and the rest of `Unity/ProjectSettings/`, which the template
   does not hold. Replace these placeholders in every file's contents, and `__GAME__` also in every folder and file
   name:
   - `__GAME__`: the game code name.
   - `__UNITY__`: the Unity version line from `ProjectVersion.txt` (for example `6.7`).
   - `__DATE__`: today's date (YYYY-MM-DD).
   - `__RENDER__`: `3D URP` for Universal 3D, or `2D URP (Universal 2D template)` for Universal 2D.

   For Universal 2D, also change `Models/` to `Sprites/` in the `Content/` tree of `AI/ARCHITECTURE.md`. Then search
   the repo for `__GAME__`, `__UNITY__`, `__DATE__` and `__RENDER__`: nothing must remain. The template's `<…>` lines
   are text to fill in later (steps 6, 10 and 11).
2. **First commit.** Commit the root files (`.gitattributes` must be in git before the first binary). Do not commit
   `Unity/` yet.
3. **MCP for Unity.** Add `"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.3.0"`
   to `Unity/Packages/manifest.json`. The template's `.mcp.json` starts the server for Claude Code, pinned to the same
   version (`mcpforunityserver==10.3.0`), and `.claude/settings.json` turns it on. If a newer release tag exists, use
   it in both places. Then stop and tell me to:
   1. Open `Unity/` in Unity Hub (**Add > Add project from disk** if Hub still lists the old subfolder).
   2. Restart Claude Code. If it asks to approve `unityMCP`, approve it.
   3. Tell you to continue.

   Then read `mcpforunity://instances`: it must list this project. Do not let **Window > MCP for Unity** configure a
   client: it can write the server into Claude Desktop's config, which Claude Code does not read.
4. **Move the template's files into ours.** The URP template puts its files at the `Assets/` root, where git ignores
   them. Over MCP (`execute_code`, `AssetDatabase.MoveAsset`, which keeps GUIDs), move `Assets/Settings` to
   `Assets/__GAME__/Settings` and `Assets/Scenes` to `Assets/__GAME__/Scenes`. Move `InputSystem_Actions.inputactions`
   to `Assets/__GAME__/`: Universal 3D puts it at the `Assets/` root, Universal 2D in `Assets/Settings/`. Then the
   template's own content must go: `Assets/TutorialInfo` and `Assets/Readme.asset` (Universal 3D), or `Assets/Welcome`
   (Universal 2D). `execute_code` blocks deletes (AUTOMATION.md), so ask me to delete them in the Project window, or to
   approve one `execute_code` call with `safety_checks=false` and `MoveAssetToTrash`. After that, the `Assets/` root
   holds only `__GAME__` (and its `.meta`).
5. **Project settings.** Over MCP, set:
   - Enter Play Mode Options on, with no domain reload and no scene reload.
   - Active Input Handling: Input System Package (new) only.
   - Run In Background on (an unfocused editor does not tick play mode without it).
   - Asset Serialization: Force Text.
   - Company name and product name: ask me.
6. **Packages.** Remove `com.unity.ugui` (UI is UI Toolkit only), `com.unity.visualscripting`,
   `com.unity.collab-proxy` and `com.unity.multiplayer.center` if they are there and nothing uses them. Remove a
   package after the packages that depend on it (`com.unity.visualscripting` depends on `com.unity.ugui`). Universal
   2D also has `com.unity.learn.iet-framework`, which its `Welcome` page uses: remove it after step 4 deleted that
   folder, and `git rm` its leftover `ProjectSettings/Packages/com.unity.learn.iet-framework/`. Then regenerate the
   csproj files (AUTOMATION.md, rung 1 caveats). Write in `AI/PACKAGES.md` what you removed, so that I can ask for one
   back.
7. **Our code.** The template holds three assemblies under `Assets/__GAME__/`: `Runtime`, `Editor` and
   `Tests/EditMode`. Make the `GameConfig` asset at `Assets/__GAME__/Content/Data/GameConfig.asset` over MCP.
   Open **__GAME__ > Design Hub** over MCP and read the console: it must open with no errors.
8. **Verify.** Climb the ladder in `AI/AUTOMATION.md`, rungs 1 to 3. All must pass. Then run the clone checks in
   `AI/AUTOMATION.md` ("Does it survive a clone?").
9. **Commit `Unity/`.** Before the commit, look at `git status` and `git diff --stat`: nothing from `Library/`,
   `Temp/`, `Logs/` or `UserSettings/` goes in, and no binary goes in without LFS.
10. **FMOD.** Stop and tell me to:
    1. Install FMOD for Unity from Package Manager > My Assets.
    2. Make an FMOD Studio project at `Audio/__GAME__/__GAME__.fspro` (File > New, then Save As).
    3. Close FMOD Studio and tell you to continue.

    Then:
    1. Move `Audio/Scripts/names.js` from the template copy into `Audio/__GAME__/Scripts/`. FMOD Studio runs it on each
       bank build; it writes `Assets/__GAME__/Runtime/Audio/Fmod.cs`. Make the `Runtime/Audio` folder first: the
       script does not make folders.
    2. Build the banks headless with `fmodstudiocl.exe -build` (beside FMOD Studio; FMOD Studio closed). Verify that
       `Build/` holds the banks and that `Fmod.cs` exists. Build before the next step: FMOD for Unity rejects a source
       project with no built banks.
    3. Point FMOD for Unity at the Studio project, with paths relative to `Unity/`. The setup wizard does this, and so
       does `execute_code` (read the fields with `unity_reflect` first). The exact calls are in the FMOD section of
       `AI/PACKAGES.md`. `FMODUnity.EditorUtils.ValidateSource` must report valid.
    4. Turn off Unity's built-in audio (`m_DisableAudio` on `ProjectSettings/AudioManager.asset`). The wizard does
       this; setup without the wizard must do it.
    5. Add `FMODUnity` to the `references` of `__GAME__.Runtime`. Verify that rungs 1 to 3 pass.
    6. Fill in the FMOD section of `AI/PACKAGES.md` with the versions you found and the real paths.
    7. Commit: the `.fspro`, `Metadata/`, `Assets/`, `Scripts/` and `Build/` (banks, through LFS), and
       `Assets/Resources/FMOD/FMODStudioSettings.asset`.
11. **The game.** If `Docs/` holds a design document, take the pitch from it. If not, ask me for a one-paragraph pitch
    of the game. Write it into `CLAUDE.md` (the first section and "Where we are") and `README.md`. Put the first game
    terms into `AI/GLOSSARY.md`, and show me the list. If a design or plan document in `Docs/` differs from the
    template's docs (folders, code style, config names), list each difference in `AI/TODO.md` for me to decide.
12. **Finish.** Delete the `## Setup` group from `AI/TODO.md`. Commit and push.

## Rules for the setup

- If a step does not work as written here (a menu moved, a package changed its name), find out why, do what the
  step means, and correct the doc that the step came from. Do not report a step as done if you did not verify it.
- Each choice that was expensive to make goes into `AI/DECISIONS.md`. The template already holds the decisions that
  come with the kit.
- At the end, tell me in a short list what is set up, what you could not verify, and what waits on me.
