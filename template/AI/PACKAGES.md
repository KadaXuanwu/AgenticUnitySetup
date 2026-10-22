# Packages

What we build on that is not ours: Unity packages, Asset Store packages and FMOD — how each reaches a clone, and what
of each we can and cannot use.

## Unity packages

Packages are not committed. `Packages/manifest.json` and `packages-lock.json` are, and a clone restores from them. A
git-URL package (`com.coplaydev.unity-mcp`) is pinned by commit hash in the lock file; if a silent jump ever bites,
pin the URL to a tag with `#<tag>`.

Removed from the URP template at setup, because nothing uses them: <filled in at setup>. Add one back when a feature
needs it, and say so here.

`com.coplaydev.unity-mcp` ships a runtime assembly (helpers its editor tools use, never called in the game) because it
has no editor-only constraint. Remove the package before a public release (RELEASE.md).

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
`AssetDatabase.ImportPackage(<cache file>, false)` per package. Each asks "Missing Signature" in a dialog: Import
Anyway is a human click.

**Adding a package:** import it, then add its entry to `AssetStore.json`. The cache file's version is in its gzip
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
  owns it. Version: <filled in at setup>. If installing from My Assets copies it into `Packages/` as an embedded
  package, delete that folder and let Unity re-resolve. Adding or updating any package can log an exception from FMOD's
  package event handler; it is FMOD's, harmless, and the package lands.
- **FMOD Studio** must match the integration's minor version. Its command-line tool (`fmodstudiocl.exe`, beside
  FMOD Studio) runs a JavaScript file on a project (`-script`) and builds banks (`-build`) headlessly, with FMOD Studio
  closed (it locks the project).
- **The project** is `Audio/__GAME__/__GAME__.fspro`. Committed: the `.fspro`, `Metadata/` (XML), `Assets/` (the audio
  files events use, LFS), `Scripts/` and `Build/` (the banks, LFS, so a clone plays without FMOD Studio). Ignored:
  `.cache/`, `.unsaved/`, `.user/`.
- **Names are generated.** Every bank build, from FMOD Studio or headless, runs `Scripts/names.js`, which writes
  `Runtime/Audio/Fmod.cs`: `Fmod.Events.<Folder>.<Event>`, `Fmod.Snapshots`, `Fmod.Buses`, `Fmod.Vcas`,
  `Fmod.Parameters`. Code never spells a path; renaming an event the game plays breaks the build.
- **Settings** live in `Unity/Assets/Resources/FMOD/FMODStudioSettings.asset`, whitelisted in `Unity/.gitignore`;
  `FMODStudioCache.asset` beside it and `Assets/Plugins/FMOD/` are rebuilt. FMOD's setup turns off Unity's built-in
  audio, on purpose.
