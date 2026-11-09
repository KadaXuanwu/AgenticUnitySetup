# Packages

What we build on that is not ours: Unity packages, Asset Store packages and FMOD — how each reaches a clone, and what
of each we can and cannot use.

## Unity packages

Packages are not committed. `Packages/manifest.json` and `packages-lock.json` are, and a clone restores from them. A
git-URL package (`com.coplaydev.unity-mcp`) is pinned by commit hash in the lock file, so every clone gets the same
commit until someone updates it.

Removed from the URP template at setup, because nothing uses them: <filled in at setup>. Add one back when a feature
needs it, and say so here.

`com.coplaydev.unity-mcp` ships a runtime assembly (helpers its editor tools use, never called in the game) because it
has no editor-only constraint. Remove the package before a public release (RELEASE.md).

MCP for Unity has no version number in any file. The package URL in `manifest.json` ends in `#main`, which is the
latest stable release (`beta` is the pre-release branch). Claude Code starts the server from the root `.mcp.json`
through `Tools/unity-mcp.py`: it reads the version of the installed package (`Library/PackageCache`) and starts
`mcpforunityserver` at that version, so the package and the server always match. Before Unity has resolved the
package (a fresh clone), it starts the latest server.

To update: Package Manager > MCP for Unity > Update (or delete its entry from `packages-lock.json`), let Unity
resolve, then restart Claude Code. Read the release notes for changed tool names and fix AUTOMATION.md in the same
pass. **Window > MCP for Unity** can write the server into Claude Desktop's config instead of Claude Code's; Claude
Code reads only `.mcp.json`.

## Asset Store packages

Classic `.unitypackage` assets are not committed: they are large, the store serves them again, and a licence forbids
redistributing them. `Unity/AssetStore.json` records each one: store `Id`, `Title`, `Publisher`, `Version`, its file
in the local download cache (`Cache`, relative to `%APPDATA%/Unity/Asset Store-5.x`, or `ASSETSTORE_CACHE_PATH`), the
`Root` folder it imports to, and a `Probe` file that only exists once it is imported. `Editor/AssetStoreRestore` runs
once per editor session, and on `__GAME__ > Asset Store > Restore Missing`:

- probe missing and the package in the cache → imports it silently; a cached version other than the pinned one
  imports anyway with a warning — the store only serves the latest version.
- not in the cache → warns with the store link. Download or import it from Package Manager > My Assets (needs the
  owner's Unity login; there is no API for it), then run Restore Missing.

After any import of a listed package, however started, it runs `git checkout -- <Root>`: the import has just
overwritten our patches with the vendor's originals, and this puts them back. If a package also writes files outside
its Root (some ship the URP template's settings), `git checkout` ours after the import and write that down here.

**A fresh clone can't restore itself if our code needs a vendor package to compile.** While any assembly fails, Unity
loads no project code, so `AssetStoreRestore` never runs. Import over MCP instead: `execute_code`
`UnityEditor.AssetPackage.Package.Import(<cache file>, false)` per package (the call `AssetStoreRestore` uses). Each asks "Missing Signature" in a dialog: Import
Anyway is a human click.

**Adding a package:** import it, then add its entry to `AssetStore.json`. `AssetStoreTests` fails on an entry with a
missing field, a `Root` that is not a folder at the `Assets/` root, or a `Probe` outside the `Root`. The cache file's version is in its gzip
header: `python -c "import sys;b=open(sys.argv[1],'rb').read(4096);print(b[16:12+b[10]+b[11]*256])" <file>`. UPM-style
Asset Store packages go into `manifest.json` by themselves and need no entry.

**Patching a vendor file** (URP conversion, import settings in a `.meta`): edit it in place, then `git add -f <file>`.
Once tracked, git follows it despite the ignore rule, and a diff shows exactly what we changed. Do not force-add a
vendor folder's `.meta` — only the changed files. Files that are ours to evolve belong copied into `Assets/__GAME__/`,
not patched. An untracked edit in a vendor folder is invisible to git and is lost on the next clone.

### What we use of each

| Package | Root | We use | Not available to us |
| ------- | ---- | ------ | ------------------- |

## FMOD

- **FMOD for Unity** is an Asset Store UPM package: `manifest.json` names it, and Unity fetches it for an account that
  owns it. Version: <filled in at setup: the package name, its version, the FMOD version>. If installing from My Assets copies it into `Packages/` as an embedded
  package, delete that folder and let Unity re-resolve. Adding or updating any package can log an exception from FMOD's
  package event handler; it is FMOD's, harmless, and the package lands.
- **FMOD Studio** must match the integration's minor version: <filled in at setup>. Its command-line tool runs a
  JavaScript file on a project (`-script`) and builds banks (`-build`) headlessly, with FMOD Studio closed (it locks
  the project):

  ```powershell
  & "<filled in at setup: path to fmodstudiocl.exe>" -build Audio\__GAME__\__GAME__.fspro
  ```

- **FMOD for Unity points at the project** relative to `Unity/`: source project `../Audio/__GAME__/__GAME__.fspro`,
  banks `../Audio/__GAME__/Build`. To set it without the setup wizard, use `execute_code` with a `SerializedObject` on
  `FMODUnity.Settings.Instance`. Set `sourceProjectPath`, `sourceBankPath`, `HasSourceProject` and `HasPlatforms`.
  Then call `FMODUnity.EventManager.RefreshBanks()`, and check with `FMODUnity.EditorUtils.ValidateSource`. This is
  what the wizard's "Locate Studio Project" does. It needs built banks: build them first.
- **The project** is `Audio/__GAME__/__GAME__.fspro`. Committed: the `.fspro`, `Metadata/` (XML), `Assets/` (the audio
  files events use, LFS), `Scripts/` and `Build/` (the banks, LFS, so a clone plays without FMOD Studio). Ignored:
  `.cache/`, `.unsaved/`, `.user/`.
- **Names are generated.** Every bank build, from FMOD Studio or headless, runs `Scripts/names.js`, which writes
  `Runtime/Audio/Fmod.cs`: `Fmod.Events.<Folder>.<Event>`, `Fmod.Snapshots`, `Fmod.Buses`, `Fmod.Vcas`,
  `Fmod.Parameters`. Code never spells a path; renaming an event the game plays breaks the build. If two FMOD names
  give the same C# name ("Hit 1" and "Hit-1"), the later one gets a number (`Hit1_2`), and the top of `Fmod.cs` lists
  each such collision.
- **Settings** live in `Unity/Assets/Resources/FMOD/FMODStudioSettings.asset`, whitelisted in `Unity/.gitignore`;
  `FMODStudioCache.asset` beside it is rebuilt. FMOD 2.04 and later is a UPM package and puts nothing in
  `Assets/Plugins/`. Unity's built-in audio is off (`m_DisableAudio` on `ProjectSettings/AudioManager.asset`), on
  purpose. The setup wizard turns it off; setup without the wizard must turn it off over `execute_code`.
