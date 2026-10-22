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

1. Make sure that the repo root has `Unity/ProjectSettings/ProjectVersion.txt`. If not, stop and tell me. Read the
   editor version from it. The kit is made for Unity 6.7 (`6000.7`). If the project uses a different version, tell me
   before you continue. Then expect differences: an API, a setting or a package version that is not there (for
   example the `[AutoStaticsCleanup]` attributes in `Unity.Scripting.LifecycleManagement`). Use what this version has,
   and correct the doc that described the 6.7 way.
2. If `Unity/.git` exists, Unity Hub made a second repo inside the project. Ask me, then delete it.
3. Make sure that `git lfs version` works. If not, stop and tell me to install Git LFS.
4. Write the steps below into `AI/TODO.md` under a `## Setup` group, as soon as `AI/` exists. Tick each step as it
   is done. I restart Claude Code during the setup, so the next session continues from that list.

## Steps

1. **Copy the template.** Copy everything in `template/` to the repo root, dot files too. If a file already exists,
   ours replaces it, except `Unity/Packages/manifest.json` and the rest of `Unity/ProjectSettings/`, which the template
   does not hold. Replace `__GAME__` with the game code name in every file's contents and in every folder and file
   name. Replace `__UNITY__` with the Unity version line from `ProjectVersion.txt` (for example `6.7`), and
   `__DATE__` with today's date (YYYY-MM-DD). Then search the repo for `__GAME__`, `__UNITY__` and `__DATE__`: nothing
   must remain. The template's `<…>` lines are text to fill in later (steps 6, 10 and 11).
2. **First commit.** Commit the root files (`.gitattributes` must be in git before the first binary). Do not commit
   `Unity/` yet.
3. **MCP for Unity.** Add `"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"`
   to `Unity/Packages/manifest.json`. Then stop and tell me to:
   1. Open `Unity/` in Unity Hub.
   2. Open **Window > MCP for Unity** and let it configure Claude Code.
   3. Restart Claude Code and tell you to continue.
4. **Move the template's files into ours.** The URP template puts its files at the `Assets/` root, where git ignores
   them. Over MCP (`execute_code`, `AssetDatabase.MoveAsset`, which keeps GUIDs), move `Assets/Settings` to
   `Assets/__GAME__/Settings`, `Assets/Scenes` to `Assets/__GAME__/Scenes` and `Assets/InputSystem_Actions.inputactions`
   to `Assets/__GAME__/`. Delete `Assets/TutorialInfo` and `Assets/Readme.asset`. After that, the `Assets/` root holds
   only `__GAME__` (and its `.meta`).
5. **Project settings.** Over MCP, set:
   - Enter Play Mode Options on, with no domain reload and no scene reload.
   - Active Input Handling: Input System Package (new) only.
   - Run In Background on (an unfocused editor does not tick play mode without it).
   - Asset Serialization: Force Text.
   - Company name and product name: ask me.
6. **Packages.** Remove `com.unity.ugui` (UI is UI Toolkit only), `com.unity.visualscripting`,
   `com.unity.collab-proxy` and `com.unity.multiplayer.center` if they are there and nothing uses them. Write in
   `AI/PACKAGES.md` what you removed, so that I can ask for one back.
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
    3. Tell you to continue.

    Then:
    1. Move `Audio/Scripts/names.js` from the template copy into `Audio/__GAME__/Scripts/`. FMOD Studio runs it on each
       bank build; it writes `Assets/__GAME__/Runtime/Audio/Fmod.cs`. Make the `Runtime/Audio` folder first: the
       script does not make folders.
    2. Point FMOD for Unity at the Studio project, with the source project path relative to `Unity/`. Use
       `FMODUnity.Settings.Instance` over `execute_code` (read its fields with `unity_reflect` first). If that does not
       work, tell me to run FMOD's setup wizard. FMOD turns off Unity's built-in audio; keep it off.
    3. Add `FMODUnity` to the `references` of `__GAME__.Runtime`.
    4. Build the banks headless with `fmodstudiocl.exe -build` (FMOD Studio closed). Verify that `Fmod.cs` exists and
       that rungs 1 to 3 pass.
    5. Fill in the FMOD section of `AI/PACKAGES.md` with the versions you found and the real paths.
    6. Commit: the `.fspro`, `Metadata/`, `Assets/`, `Scripts/` and `Build/` (banks, through LFS).
11. **The game.** Ask me for a one-paragraph pitch of the game. Write it into `CLAUDE.md` (the first section and
    "Where we are"). Put the first game terms from it into `AI/GLOSSARY.md`, and show me the list.
12. **Finish.** Delete the `## Setup` group from `AI/TODO.md`. Commit and push.

## Rules for the setup

- If a step does not work as written here (a menu moved, a package changed its name), find out why, do what the
  step means, and correct the doc that the step came from. Do not report a step as done if you did not verify it.
- Each choice that was expensive to make goes into `AI/DECISIONS.md`. The template already holds the decisions that
  come with the kit.
- At the end, tell me in a short list what is set up, what you could not verify, and what waits on me.
