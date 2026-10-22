# Release checklist

What must be done, checked or remembered by a public release. Not general work (that is [TODO.md](TODO.md)). Add an
item the moment something is deferred to release; tick it only when it is true of the release build.

## Build

- [ ] Remove `com.coplaydev.unity-mcp`: its runtime assembly ships in every build (PACKAGES.md). Agents lose the
  editor connection, so only on the release branch or build machine.
- [ ] Asset Store packages restored on the build machine from `Unity/AssetStore.json`; they are not in git
  (PACKAGES.md).
- [ ] Developer tools (debug overlays, cheat keys) out of player builds.
- [ ] Scripting backend and code stripping checked: FMOD and every Asset Store package survive stripping (`link.xml`).
- [ ] Shader variant count and build size checked in a player build.
- [ ] Time scale and other test settings back at their defaults in `ProjectSettings/` (AUTOMATION.md).

## Performance

- [ ] Frame time measured on the minimum-spec PC in a development build.
