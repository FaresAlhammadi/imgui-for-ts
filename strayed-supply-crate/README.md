# Strayed Supply Crate — a Gorilla Tag × Strayed mashup

You play **Gorilla Tag** as usual. When the level starts, an open Strayed supply crate stands in front
of you holding real **Strayed** gear — the Rock, Torch, Stone Hatchet and Wooden Bat — built from the
meshes and textures in *your own* Strayed install, in a crate wearing Strayed's own wooden crate texture.
Grip near an item to pick it up, release to throw it; the torch gives a flickering light.

- Host game (primary): Gorilla Tag (Steam, Unity 6000.2.9, Mono) — a BepInEx 5 plugin.
- Companion: Strayed (Steam), read-only. At start the plugin opens `Strayed_Data` with AssetsTools.NET on
  a background thread, decodes the item meshes and hands the textures to the GPU in Unity's own
  compressed format. Nothing from either game is copied or shipped.
- Solo. The crate exists only on your machine, and only while you are offline or in a private room:
  in a public lobby it is hidden (Another Axiom's Fan Content & Mod Policy keeps gameplay mods out of
  public lobbies).

Earlier hosts: Animal Company (dropped — it runs under EasyAntiCheat) and People Playground (dropped —
Studio Minus removed C# mod support after the September 2026 Workshop malware incidents; 1.27.18 cannot
compile any mod).

## Status

| Part | State |
|---|---|
| Sheets (`sheets/`) + preflight (`tools/preflight.py`) | clean: items, crate, hooks, systems all filled and verified |
| Strayed reader (`src/StrayedSupply.Core`) | builds; 9 unit tests pass; all four items + the crate texture read from a real install in ~2 s |
| Gorilla Tag plugin (`src/StrayedSupply.Plugin`) | builds against the installed game with 0 warnings; **ran in Gorilla Tag**: "Strayed supply crate spawned … with 4 items", no errors |
| Grab / throw, torch light, public-lobby gate | built; not yet exercised with a headset |

## Items (traced through Strayed's own item definitions and prefabs)

| Item | Strayed mesh | Strayed texture | Strayed dmg | Length |
|---|---|---|---|---|
| Rock | `Rock` | `Rock#2268` | 12 | 0.25 m |
| Torch | `SM_Torch_LOD0` | `T_Torch_BC` | 0 (not in the wiki chart; a light here) | 0.6 m |
| Stone Hatchet | `SM_StoneHatchet_LOD0` | `T_StoneHatchet_BC` | 15 | 0.45 m |
| Wooden Bat | `LOD0_Bat#4040` | `RGB_2` | 18 | 0.85 m |

Crate boards: `WoodenCrate02`. Strayed authors item meshes at mixed scales, so `MeshFit` rotates each one
upright and scales it to the sheet's length. Strayed reuses some asset names, so the reader refuses an
ambiguous name and takes `name#pathId`; `src/StrayedIndexTool` prints those keys. A Strayed update can
change pathIds — re-run the index tool after one. Damage numbers are informational: nobody gets hurt.

## Building

1. Get `classdata.tpk` (Unity type database, MIT): the LZ4 build linked from the
   [AssetRipper/Tpk README](https://github.com/AssetRipper/Tpk#type-tree-tpk). Strayed's files carry no
   type trees, so the reader needs it. It ships next to the plugin.
2. `python3 tools/preflight.py && python3 tools/gen.py`
3. `dotnet test src/StrayedSupply.Core.Tests`
4. `dotnet build src/StrayedSupply.Plugin -c Release -p:GtDir="<Gorilla Tag folder>" -p:ClassDataTpk="<path>\classdata.tpk" -p:Deploy=true`
   (copies the plugin into `<Gorilla Tag>/BepInEx/plugins/StrayedSupplyCrate`; BepInEx 5 must be installed).

The plugin finds Strayed by itself (config `StrayedFolder`, then Melty's `STRAYED_SUPPLY_STRAYED_DIR`,
then every Steam library). `tools/strayed_names.py` lists asset names without .NET.

## Credits

- Gorilla Tag © Another Axiom; Strayed © Crustacean Interactive. Players use their own copies. This is
  an unofficial fan mod, not affiliated with or endorsed by either studio.
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) (MIT) — reads Strayed's asset files.
- [BepInEx](https://github.com/BepInEx/BepInEx) (MIT) — plugin loader, provided by Melty for Gorilla Tag.
- [AssetRipper Tpk](https://github.com/AssetRipper/Tpk) `classdata.tpk` (MIT) — Unity type database, shipped with the plugin.
- Item damage numbers: [strayed.wiki.gg/wiki/Tools](https://strayed.wiki.gg/wiki/Tools).

License: MIT (this mod's code). Remixing allowed.
