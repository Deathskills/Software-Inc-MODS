# Realistic Population growth

Authors: Deathskills + Claude

A Software Inc. code mod that replaces the game's fixed market population (vanilla: 145,613,576 for every year) with a curve that follows real-world growth.

## What it does

- **1980–1995:** population stays at the vanilla 145.6M.
- **1995–2025:** each year adds the real-world growth of the largest single platform ecosystem (MS-DOS/Windows → Facebook/Meta), at a fixed scale.
- **2025–2050:** constant 1.53 %/year growth (the real-world rate that would reach 10.3B in 2084).
- **2050 onwards:** flat at 1.4B. This also keeps the game below its ~1.43B 32-bit overflow limit.
- The target is calculated from the in-game date once per in-game day, interpolated within the year, and enforced every frame. New games and loaded saves pick up the right value automatically.
- The vanilla category multipliers are untouched: Computer OS × 0.95, Console OS × 0.75, Phone OS × 1.0.

| Year | In-game population |
|---|---|
| 1980–1995 | 145.6M |
| 2000 | 210.7M |
| 2010 | 361.9M |
| 2020 | 803.0M |
| 2025 | 992.1M |
| 2050+ | 1.40B |

The full yearly table with sources is in `population-data-review.md` and `population-data.csv`.

## Compatibility

- No Harmony and no patched methods. The only thing the mod writes is the public static field `MarketSimulation.Population`.
- It does not change software types, OS multipliers, servers, distribution platforms, `FanPopulation` or `FollowerPopulation`. Other mods that read `Population` simply see the new value.
- Disabling the mod restores the vanilla value.
- If another mod writes `MarketSimulation.Population`, this mod restores its target every frame.

### MegaMod Core

This mod overrides other population mods, including MegaMod Core's "Population follows the real year" option. Fans and followers are left to MegaMod if its option is on. Turn MegaMod's population option off for a clean result. The first detected overwrite in a session is logged once.

## Install

Copy the `Realistic Population growth` folder (with `RealisticPopulation.cs`) into `Software Inc/DLLMods/`. The game compiles it on start; accept the code-mod verification prompt and enable it in the Mods window.
