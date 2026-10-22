# Decisions

Structural choices that were expensive to make, so they are not argued again. Five lines at most per entry, newest
last. A reversal says what it supersedes. Design and player-facing decisions go in
[Docs/DESIGN-DECISIONS.md](../Docs/DESIGN-DECISIONS.md) instead.

## __DATE__ — One repo, Unity project in a subfolder

**Why:** design docs, agent docs and code change together. **Costs:** git and `dotnet build` paths run from the root.
`.gitattributes` must be at the root (its LFS macros are ignored anywhere else).

## __DATE__ — UI Toolkit only

**Why:** UXML and USS are text an agent authors and verifies end to end; a uGUI canvas is not. **Costs:**
TextMeshPro and uGUI-based Asset Store UI won't work without adding `com.unity.ugui` back.

## __DATE__ — Agent knowledge lives in committed docs

`CLAUDE.md`, `AI/` and `.claude/settings.json` are in git; nothing lives in machine-local memory. **Why:** a fresh
clone on another device has to be the whole picture. **Costs:** docs have to be pruned as they go stale, hence the
five-line cap.

## __DATE__ — Asset Store packages stay out of git

Only `Assets/__GAME__/` is committed; vendor packages are listed in `Unity/AssetStore.json` and re-imported from the
Asset Store cache by an editor bootstrap, and patched vendor files are force-added. **Why:** size, licences forbid
redistribution, the store serves them again. **Costs:** a new machine downloads each package once; the store serves
only the latest version, so an old pin can't be reproduced unless its `.unitypackage` is backed up.

## __DATE__ — Every tunable number in a ScriptableObject, shown in one Design Hub

**Why:** the designer balances without reading code, and finds every number by topic in one window. **Costs:** each
feature places its numbers on a hub page in the same pass.

## __DATE__ — MCP for Unity is the editor bridge

**Why:** the agent compiles, tests and plays the game without a human click, with the editor unfocused. **Costs:** a
runtime assembly in builds until the package is removed for release; modal dialogs hang it (AUTOMATION.md).

## __DATE__ — FMOD for audio

**Why:** FMOD Studio shapes every sound (variety, space, mix); the game only says when and where. Generated names make
a renamed event a compile error. **Costs:** FMOD Studio must match the integration's version; audio numbers live in
FMOD, not in the Design Hub.
