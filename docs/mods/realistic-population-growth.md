# Realistic Population growth: status and history

Replaces the fixed vanilla market population (145,613,576) with a real-world growth curve. Source is a single `.cs` file the game compiles itself: `Realistic Population growth/RealisticPopulation.cs`. Data and sources: `population-data.csv` and `population-data-review.md` in the same folder.

## Current state (14 Nov 2026)

- Merged in PR #1 (12 Nov). No Harmony, writes only `MarketSimulation.Population`, re-applied once per in-game day. Confirmed working in game by Marc (3 ms cached load).
- Curve: flat 145.6M until 1995, real-world largest-platform growth 1995–2025 (992M in 2025), 1.53%/year to 1.4B in 2050, flat after (stays under the ~1.43B 32-bit overflow).
- **Both PCs run the merged repo version** (`c141e090…a004`; 4090 updated 15 Nov). The 4090 ran the unmerged PR #6 version (`8aab1e2c…4062`) until then; it's backed up in `ModBackups\Realistic Population growth pre-repo-merged\`. PR #6's version remembers the previous population so turning the mod off restores whatever value was there instead of always the vanilla constant.

## MegaMod compatibility (15 Nov 2026, branch `claude/megamod-compat`, installed on the 4090, not merged)

- The population is now enforced every frame, so it wins over MegaMod Core's "Population follows the real year".
- New: `FanPopulation` and `FollowerPopulation` scale with the population at the vanilla ratio (Marc's decision), up to about 87.5M and 707,000 at the 1.4B cap, and are enforced the same way. Disabling the mod restores all three vanilla values. Untested in game.

## Install

Copy the folder (with `RealisticPopulation.cs`, `meta.tyd`, `README.md`) into `DLLMods\`. The game compiles it on start and asks once to verify the code mod. Changing the `.cs` makes the game ask again.

## Open items

- Decide whether PR #6's restore-on-disable change should be merged.
