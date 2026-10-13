# Strayed Supply Crate — an Animal Company × Strayed mashup

You play **Animal Company** as usual. When a level loads, a Strayed supply crate appears in
front of you holding real **Strayed** gear — the Rock, Torch, Stone Hatchet and Wooden Bat — built
from the meshes and textures in *your own* Strayed install. Grab them and take them into the run;
the torch lights the way.

- Host game (primary): Animal Company (Steam, Unity IL2CPP) — the mod is a BepInEx 6 IL2CPP plugin.
- Companion: Strayed (Steam) — read-only; the mod opens `Strayed_Data` at start with AssetsTools.NET
  and decodes the item meshes/textures in memory. Nothing from either game is copied or shipped.
- Solo. Use it in a private/solo session; the crate and items exist only on your machine.

## Status

| Part | State |
|---|---|
| Design sheets (`sheets/`) + preflight (`tools/preflight.py`) | done |
| Strayed reader (`src/StrayedSupply.Core`) | builds; unit-tested on synthetic mesh data |
| Index tool (`src/StrayedIndexTool`) | builds; must be run on a PC with Strayed to fill the items sheet |
| Animal Company plugin (`src/StrayedSupply.Plugin`) | **source only, untested** — needs Animal Company + BepInEx interop assemblies to compile |
| Damage to monsters (`hooks.monster_hit`) | open — needs Animal Company's monster class from the real build |

Run `python3 tools/preflight.py` to see every open cell. Build only once it is clean.

## Building (on the PC with both games)

1. Run Animal Company once with BepInEx 6 IL2CPP installed so `BepInEx/interop` is generated.
2. `dotnet run --project src/StrayedIndexTool -- "<Strayed folder>" classdata.tpk rock torch hatchet bat`
   and copy the real names into `sheets/items.json`; fill `hooks.json` from the interop assemblies.
3. `python3 tools/preflight.py && python3 tools/gen.py`
4. `dotnet build src/StrayedSupply.Plugin -c Release -p:AcDir="<Animal Company folder>"`

## Credits

- Animal Company © Wooster Games; Strayed © Crustacean Interactive. Players use their own copies.
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) (MIT) — reads Strayed's asset files.
- [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1) and Il2CppInterop (LGPL-3.0) — plugin loader.
- [AssetRipper Tpk](https://github.com/AssetRipper/Tpk) classdata (MIT) — Unity type database.
- Item damage numbers: [strayed.wiki.gg/wiki/Tools](https://strayed.wiki.gg/wiki/Tools).

License: MIT (this mod's code). Remixing allowed.
