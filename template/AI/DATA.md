# Data

Every tunable number sits in a ScriptableObject under `Content/Data` and shows in the Design Hub; anything technical
sits in `Content/Tech` and never shows ([CLAUDE.md](../CLAUDE.md), Roles).

## Configs

`GameConfig` (`Content/Data/GameConfig`, type `Runtime/GameConfig`) is the root balance config. Its fields are grouped
by `[Header]`; each header is one topic. Wiring between assets sits above its first header, where the hub does not
show it. A topic that is a whole set of assets (one per enemy, one per level) gets its own config type, with its assets
in `Content/Data`.

Technical numbers (world size, budgets, layers) go in a config under `Content/Tech`, made with the first such number.

A number the game reads at load takes effect at the next Play. Say so in the field's tooltip if that is not clear.

## Design Hub

The Design Hub (`__GAME__ > Design Hub`, `Editor/DesignHub`) is organised by topic, not by asset: its `Pages` list puts
`GameConfig` sections (by `[Header]`), whole config assets (by type) and balance sheet sections on pages under groups
(Balance, Feel, …). A feature that adds a header, sheet section or config type places it on a page in the same pass —
until then it shows under Other (Unsorted, or the asset by name), never hidden.

The balance sheet is `DesignHub.SheetSections`: by section name, the lines a config adds up to, each a label and a
value worked out from the config. New balance numbers get a sheet line when they combine into something a designer
would otherwise compute by hand (time to kill, gold per minute). The sheet follows every edit live.
