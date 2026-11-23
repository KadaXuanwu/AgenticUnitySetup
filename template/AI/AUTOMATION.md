# Automation

The point: an agent verifies its own work instead of handing over a checklist of clicks. Given two ways to build
something, take the one a headless tool can read and write.

## The verification ladder

Climb only as far as the change requires. Rungs 1–3 run without the editor focused and without a human.

| # | Check | Command | Catches |
| - | ----- | ------- | ------- |
| 1 | Compile | `dotnet build Unity/Unity.slnx -v q --nologo` | syntax, types, missing asmdef references |
| 2 | Editor compile + console | `unity recompile` (each error with its file and line; exit code 6 when one is there), then `unity command console get -- --level error --tail 20` | the same through Unity's compiler, plus asset-import errors |
| 3 | EditMode tests | `unity command test run --detach`, then `unity job wait <job id>` | logic that needs no scene |
| 4 | Play it | `unity command editor.playmode start`, read state with `unity command script eval` | wiring, scene setup |

From the repo root, pass `--project-path Unity` to each `unity` command.

### Rung 1 caveats

- A PostToolUse hook in `.claude/settings.json` runs rung 1 after each edit of a `.cs` file
  (`Tools/compile-hook.py`). It is silent on a pass and shows the errors on a failure. For a file that no
  generated `.csproj` lists yet, it skips the build and says so.
- It compiles the `.csproj` files **Unity generates**, which are gitignored. After adding or moving a `.cs` or
  editing an `.asmdef`, a pass is a false negative until Unity regenerates them — so the real loop is edit →
  `unity recompile` → `console get` → tests.
- Unity emits a `.csproj` only for an assembly that **contains scripts**. A new empty asmdef is invisible to rung 1
  until its first `.cs` lands.
- After a package is added or removed, the csproj references stay stale (MSB3245 warnings) until Unity regenerates
  them: a `script eval` of `Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll(); return 0;`. A removed assembly's
  `.csproj` stays on disk, outside `Unity.slnx`; delete it.
- Building while Unity is mid-regeneration gives a phantom error. Let `unity recompile` return first.
- Read errors only. If the .NET SDK ships an older Roslyn than Unity's analyzers need (warning `CS9057`), the analyzers
  are skipped, so rung 1 misses analyzer diagnostics that rung 2 reports.

## Unity CLI command map

The editor is scriptable with the Unity CLI (`unity`, Unity's own tool) and does not need focus. The CLI reaches the
open editor through the `com.unity.pipeline` package in the project. Reach for these before describing manual steps.

| Need | Command |
| ---- | ------- |
| is the editor there and ready | `unity status` (`--until-ready` waits) |
| recompile after editing C# | `unity recompile` |
| compile errors / play-session logs | `unity command console get -- --level error --tail 20`; counts only: `unity command console status` |
| run tests | `unity command test run --detach` → `unity job wait <job id>`; some only: `-- --filter <part of a name>` |
| scenes, hierarchy, components | `unity command scene …`, `gameobject …`, `component …` |
| prefabs | `unity command prefab …` |
| assets | `unity command asset …` |
| a menu item | `unity command editor invoke_menu -- --path "<menu path>"` |
| enter/exit play mode | `unity command editor.playmode start` / `stop` |
| a picture of the game or of an editor window | `unity command capture game_view …` / `capture editor_window …` |
| anything with no command | `unity command script eval '<C#>'` (arbitrary editor C#; `return` gives the value back) |

- The commands come from the package: `unity command --detail compact` lists them, `unity command --query <word>`
  finds one, and `unity command <group> <name> --help` gives one command's parameters. Read them there, not from
  memory. Where Unity's guide names a command that this list does not have, the list is right.
- A command's own flags go after `--`; before it the CLI takes a flag of the same name for itself (`--timeout`).
  `--result-only` prints only the answer, as JSON.
- Unity's own guide for agents: `unity skill show`, with `--list` for its other files and `--path references/<file>`
  for one (`playmode-verification-loop.md` is the play check).
- No command checks a Unity API before you use it: reflect on the type in a `script eval`.
- UXML and USS are text: edit the files.

Editor work with no command → write an editor script, not a list of instructions.

### Gotchas

- **The console keeps the entries of earlier work.** `unity command console status` gives a `cursor`: note it before a
  step, and read with `unity command console get -- --level error --since <cursor>` after it. `unity recompile`
  answers with that compile's own errors. A test run right after a compile error runs the last assembly that built.
  A failed command also writes an error to the console. Never `console clear`.
- **Compile errors at editor start put Unity in Safe Mode**, and the package does not load: no `unity command`
  connects. `unity status` lists the errors. Fix them with rung 1, then `unity recompile`.
- **More than one Unity editor open**: a command that names no project takes any editor that answers. Name yours with
  `--project-path <repo>/Unity`, or with `UNITY_PROJECT_PATH` in the environment.
- **A dialog blocks the editor, and no command answers it** (CLI 1.0.1, package 1.0.0). `unity status` shows
  `starting`; with `--format json` it names the dialog under `blockedBy`. Every other command fails at once with exit
  code 18 and names the dialog and its buttons. Ask the designer to click it. Never open one:
  - **Git that changes the open scene's `.unity` file** (revert, checkout, reset) makes Unity ask "reload scene?".
    Change the open scene with `unity command script eval` (then `EditorSceneManager.SaveScene`) instead. A scene that
    is not open is fine to edit as text, so open another one (`unity command scene open -- --path <scene>`) before
    git touches the open one.
  - **Importing an unsigned `.unitypackage`** (most Asset Store packages) opens "Missing Signature", one for each
    package. Never import one unattended.
  - `EditorUtility.DisplayDialog` in code that a command runs, also on `EditorApplication.delayCall`.
- **`script eval` is a C# script:** statements, with a `return` for the value that comes back. It takes modern C#
  and methods with `in` parameters. More than a line goes in a file under `Unity/Temp`, passed with
  `-- --file Temp/<name>.cs`: the path is from the project folder. Bash keeps a `"` inside `'...'`; Windows
  PowerShell 5.1 drops it, so use a file there.
- **`script eval` can do anything, deletes included**, and no permission rule allows it: Claude Code asks for each
  call. Ask the designer before an eval deletes or moves assets.
- **A `script eval` waits 30 s.** Then the command ends with exit code 14, and the code may still run in the editor.
  For longer code, raise the wait: `unity command script eval '<C#>' --timeout 120`. After a timeout, run
  `unity status --until-ready` before you send anything else; never queue more commands behind a timeout. Keep each
  call short.
- **The editor answers nothing while it reloads** (after a compile, a package change, the start of play mode): "No
  Pipeline instance found". `unity status --until-ready` waits. After a package change the reload starts some seconds
  late, so wait a little first. Before `test run --detach`, wait until `unity status` shows `ready`: a run sent in
  the reload can start and print no job id. If it prints none, do not send it again; `unity command test status`
  says when it ends.
- **`unity recompile` can compile without the files git or a shell wrote** (after a rebase, for example). Check the
  time of the assembly's DLL in `Library/ScriptAssemblies/`. If it did not change, run a `script eval` of
  `AssetDatabase.ImportAsset(<path>, ImportAssetOptions.ForceUpdate)` for each changed file, then `unity recompile`.
- **An unfocused editor does not tick play mode by itself**, and a screenshot of a frozen game looks like a live one.
  Before `editor.playmode start`, run `unity command editor set_auto_tick`. After the start, run
  `unity status --until-ready`, then prove that the frames advance:
  `unity command job wait -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout 10`
  must answer `"met": true`. After `editor.playmode stop`, run `unity command editor set_auto_tick -- --enabled false`.
- **Test tweaks leak into project settings.** A `Time.timeScale` left changed when play stops is saved into
  `TimeManager.asset` — a shipped game at 8× speed. Reset it, then `git diff Unity/ProjectSettings/` before committing.
- **Read the console after the thing under test has happened** — speed it up with `Time.timeScale` and read again
  once it has.
- **Synthetic key presses need their own device.** Injecting into `Keyboard.current` is unreliable.
  `InputSystem.AddDevice<Keyboard>()`, queue state on *that*, remove it when done. Mouse and keyboard only count while
  the Game view has focus, which it has not for a command: press the gamepad bindings of an added `Gamepad` instead.
  Queue the event and stop: calling `InputSystem.Update()` by hand consumes the press in an update no `MonoBehaviour`
  sees. Read the result with the next command. Anything worth keeping belongs in a test instead.
- **Play mode can end without telling you**: reads then return frozen edit-mode values.
  `unity command editor status` gives `playMode`; if it reads `stopped`, start again.
- **The first `script eval` right after the start of play can run before `Awake`.** Check for null and call again.
- **To watch something that takes seconds**, set `Time.timeScale` low, wait in the shell, take the picture, then
  reset it. To act every frame, add a lambda to `EditorApplication.update` in a `script eval` that removes itself
  when done.
- **A picture of the game:** `unity command capture game_view -- --output-path Temp/Screenshots/<name>.png`. The path
  is from the project folder, so nothing lands in `Assets/`. It needs no focus.
- **A picture of an editor window** (the Design Hub): `unity command editor invoke_menu -- --path "__GAME__/Design Hub"`
  opens it, and `unity command capture editor_window -- --window "Design Hub" --output-path …` takes it, with no focus.
  Switch the page in one `script eval` and capture in the next command, or the layout is half done.
- **GPU ms per render pass** (no profiler window): in play mode, one `script eval` sets `Camera.main.targetTexture` to
  a 1920×1080 `RenderTexture`, enables `Sampler.Get("<URP pass name>").GetRecorder()` (`Sampler.GetNames` lists them)
  and adds an `EditorApplication.update` lambda that sums `gpuElapsedNanoseconds`, then parks the result in
  `AppDomain.CurrentDomain.SetData`; read it back a few seconds later. Interleave configs: the GPU's clocks drift between
  runs.

## Does it survive a clone?

Every file under `Assets/__GAME__/` has to reach a fresh clone; nothing else under `Assets/` is committed. Three ways
that fails silently:

**Something of ours sits outside `Assets/__GAME__/`.** It is ignored. `Editor/AssetStoreRestore` warns about any
`Assets/` root entry that is neither ours nor a listed Asset Store root, once per session.

**A gitignore rule swallows an asset.** After adding a new kind of asset, from the repo root:

```bash
git status --porcelain --ignored Unity/Assets/__GAME__ | grep '^!!'
```

Nothing printed means everything travels.

**Git LFS is missing on the cloning machine.** The root `.gitattributes` routes textures, audio, models, fonts,
archives and DLLs through LFS by extension. Check a new binary type with `git check-attr filter -- <path>` — it must
say `lfs`. Without the LFS client the clone succeeds and every binary is a ~130-byte stub. GitHub's free tier is 1 GB
stored and 1 GB/month bandwidth — that ceiling arrives before any repo-size limit when art lands in bulk.

How packages reach a clone is [PACKAGES.md](PACKAGES.md).

## Status

| Capability | State |
| ---------- | ----- |
| Headless compile | **Implemented** — rung 1, run by a hook after each C# edit |
| Editor compile + console with the Unity CLI | **Implemented** |
| EditMode tests with the Unity CLI | **Implemented** — `__GAME__.Tests.EditMode` |
| Doc rules as tests | **Implemented** — Design Hub placement, `AssetStore.json` entries |

Move a row as it lands. A row that has been Planned for months is a decision to delete it.
