# Automation

The point: an agent verifies its own work instead of handing over a checklist of clicks. Given two ways to build
something, take the one a headless tool can read and write.

## The verification ladder

Climb only as far as the change requires. Rungs 1–3 run without the editor focused and without a human.

| # | Check | Command | Catches |
| - | ----- | ------- | ------- |
| 1 | Compile | `dotnet build Unity/Unity.slnx -v q --nologo` | syntax, types, missing asmdef references |
| 2 | Editor compile + console | `refresh_unity(compile="request")` then `read_console(types=["error"])` | the same through Unity's compiler, plus asset-import errors |
| 3 | EditMode tests | `run_tests(mode="EditMode")` then `get_test_job` | logic that needs no scene |
| 4 | Play it | `manage_editor(action="play")`, read state with `execute_code` | wiring, scene setup |

### Rung 1 caveats

- It compiles the `.csproj` files **Unity generates**, which are gitignored. After adding or moving a `.cs` or
  editing an `.asmdef`, a pass is a false negative until Unity regenerates them — so the real loop is edit →
  `refresh_unity` → `read_console` → tests.
- Unity emits a `.csproj` only for an assembly that **contains scripts**. A new empty asmdef is invisible to rung 1
  until its first `.cs` lands.
- After a package is added or removed, the csproj references stay stale (MSB3245 warnings) until Unity regenerates
  them: `execute_code` `Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll()`. A removed assembly's `.csproj` stays on
  disk, outside `Unity.slnx`; delete it.
- Building while Unity is mid-regeneration gives a phantom error. Let `refresh_unity` return first.
- Read errors only. If the .NET SDK ships an older Roslyn than Unity's analyzers need (warning `CS9057`), the analyzers
  are skipped, so rung 1 misses analyzer diagnostics that rung 2 reports.

## Unity MCP tool map

The editor is scriptable over MCP for Unity (`com.coplaydev.unity-mcp`) and does not need focus. Reach for these
before describing manual steps.

| Need | Tool |
| ---- | ---- |
| recompile after editing C# | `refresh_unity(compile="request", mode="force")` |
| compile errors / play-session logs | `read_console(action="get", types=["error"])` |
| run tests | `run_tests` → `get_test_job` |
| scenes, hierarchy, components | `manage_scene`, `manage_gameobject`, `manage_components` |
| prefabs | `manage_prefabs` |
| assets and ScriptableObjects | `manage_asset`, `manage_scriptable_object` |
| UXML / USS | `manage_ui` |
| enter/exit play mode | `manage_editor(action="play"/"stop")` |
| check a Unity API before using it | `unity_reflect`, `unity_docs` |
| anything with no tool | `execute_code` (arbitrary editor C#) |
| several of the above at once | `batch_execute` |

Editor work with no tool → write an editor script, not a list of instructions.

### Gotchas

- **The console keeps errors from an earlier compile.** `read_console(action="clear")` before a refresh whose result
  matters, or a fixed error reads as still there. A test run right after a compile error runs the last assembly that
  built.
- **More than one Unity editor open** → MCP errors until `set_active_instance` pins this one
  (`mcpforunity://instances` lists them; pin by the project path).
- **The first MCP call after a package resolve or domain reload fails** with a socket error. Repeat the call once.
- **Editing the open scene's `.unity` file on disk**, git included (revert, checkout, reset), makes Unity ask "reload
  scene?" in a modal dialog, and every MCP call hangs until a human clicks it. Change the open scene through MCP, then
  `manage_scene` save. A scene that isn't open is fine to edit as text, so open another scene over MCP before git
  touches the open one.
- **Importing an unsigned `.unitypackage`** (most Asset Store packages) opens a "Missing Signature" modal, one per
  package, and MCP calls hang until a human clicks Import Anyway. Never import one unattended.
- `read_console` returns entries since the console was last cleared. **Clear on Play** wipes them — turn it off to
  read a play session's logs afterwards.
- **An unfocused editor does not tick play mode** unless Run In Background is on — `Time.frameCount` stays at 1 and
  everything looks frozen. The editor still stalls between MCP round trips, so compare state across three reads, not
  two.
- **Test tweaks leak into project settings.** A `Time.timeScale` left changed when play stops is saved into
  `TimeManager.asset` — a shipped game at 8× speed. Reset it, then `git diff Unity/ProjectSettings/` before committing.
- **Read the console after the thing under test has happened** — speed it up with `Time.timeScale` and read again
  once it has.
- **Synthetic key presses need their own device.** Injecting into `Keyboard.current` is unreliable.
  `InputSystem.AddDevice<Keyboard>()`, queue state on *that*, remove it when done. Mouse and keyboard only count while
  the Game view has focus, which it hasn't over MCP: press the gamepad bindings of an added `Gamepad` instead. Queue
  the event and stop: calling `InputSystem.Update()` by hand consumes the press in an update no `MonoBehaviour` sees.
  Read the result on the next round trip. Anything worth keeping belongs in a test instead.
- **`execute_code` may compile with CodeDom (C# 6)**: write `UnityEngine.Object`, never bare `Object`, and declare
  lambdas as `System.Action<...>` locals. It cannot call methods with `in` parameters. It does not find extension
  methods: call `UQueryExtensions.Q<Label>(root, "name", (string)null)`, not `root.Q<Label>("name")`.
- **`execute_code` blocks deletes.** Its safety check refuses `AssetDatabase.DeleteAsset` and `MoveAssetToTrash`,
  and Claude Code's auto mode refuses `safety_checks=false`. To delete an asset, ask the designer, or get approval for
  that one call.
- **An `execute_code` that times out may have crashed or hung the editor.** Before sending anything else, read
  `mcpforunity://editor/state` and wait until it answers; never queue more calls behind a timeout. Keep each call
  short.
- **The bridge can vanish for a few seconds after a test run** (`No Unity Editor instances found`). Wait and re-read
  `mcpforunity://instances`. During a long test run `get_test_job` can answer "Unknown job_id": `set_active_instance`
  again, wait, and read `mcpforunity://editor/state` before starting another run.
- **Play mode can end without telling you**: reads then return frozen edit-mode values. If `Time.frameCount` stops
  advancing between reads, `stop` and `play` again.
- **The first `execute_code` right after `play` can run before `Awake`.** Check for null and call again.
- **Play time barely advances between round trips.** To watch something that takes seconds, set `Time.timeScale`
  low, wait in the shell, screenshot, then reset it. To act every frame, add a lambda to `EditorApplication.update` in
  `execute_code` that removes itself when done.
- **`manage_camera(action="screenshot")` only writes inside the project** — pass `output_folder="Temp/Screenshots"`
  so nothing lands in `Assets/`.
- **Screenshotting an editor window** (the Design Hub): reading screen pixels captures whatever app sits on top. Grab
  the window itself: reflect `EditorWindow.m_Parent`, call its `GrabPixels(RenderTexture, Rect)`, `ReadPixels`, and flip
  rows — the result is upside down. Open or switch the page in one `execute_code` call and grab in the next.
- **GPU ms per render pass** (no profiler window): in play mode, one `execute_code` sets `Camera.main.targetTexture` to
  a 1920×1080 `RenderTexture`, enables `Sampler.Get("<URP pass name>").GetRecorder()` (`Sampler.GetNames` lists them)
  and adds an `EditorApplication.update` lambda that sums `gpuElapsedNanoseconds`, then parks the result in
  `AppDomain.CurrentDomain.SetData`; read it back a few seconds later. Interleave configs: the GPU's clocks drift between
  runs.

## Unity's own agent tooling

Unity ships its own CLI (`unity`), an MCP server over it, and agent plugins. MCP for Unity stays the editor bridge
until Unity's tools have: play mode that ticks with the editor unfocused, an `eval` past C# 6, and survival across a
recompile. Check again on each Unity update, beside MCP for Unity on a branch.

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
| Headless compile | **Implemented** — rung 1 |
| Editor compile + console over MCP | **Implemented** |
| EditMode tests over MCP | **Implemented** — `__GAME__.Tests.EditMode` |

Move a row as it lands. A row that has been Planned for months is a decision to delete it.
