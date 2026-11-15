# Reference documentation

Everything produced in the claude.ai project "Software Inc Modding" (12–14 Nov 2026), exported here so Claude Code can use it. All mechanics are from the vanilla game (Beta 1.8.x, Unity 2018.4.36f1), read from the decompiled `Assembly-CSharp.dll` and built-in data. Where a reference and the mod source disagree, the source wins.

| File | What it is | Origin |
|---|---|---|
| [servers-and-monetization.md](servers-and-monetization.md) | Base-game servers, overload, monetization, AI servers, market caps, durability and repairs, electricity generation and the grid-only baseline, the ISP, all deal types, the legal system, and modding notes. | Claude Doc "Software Inc. Servers and Monetization Reference" |
| [isp-design-spec-v0.7.md](isp-design-spec-v0.7.md) | Marc's complete design and technical spec for the player-owned ISP mod. Some numbers were changed by later decisions; see `../mods/isp-business.md`. | Posted by Marc, 13 Nov |
| [isp-phase1-findings.md](isp-phase1-findings.md) | ISP mod phase 1: constraints, conflicts with Server Hosting Business and vanilla, verified game-code findings (§8), mod networking API and multiplayer options (§9), decisions. | Project file `isp-mod/phase1-preliminary-findings.md` |
| [hosting-power-options.md](hosting-power-options.md) | Hosting profit and payback with a fixed $4 / $6 / $7 power price vs vanilla; wind turbine payback. Led to the $7 choice. | Artifact "Hosting Power Options" |
| [hosting-rates-and-income-model.md](hosting-rates-and-income-model.md) | Server Hosting Business bandwidth curves, profit per Mbps, agreed rules and the offer-count model. | Artifacts "Server Hosting Rates" and "Hosting Income Comparison" |
| [artifacts/](artifacts/) | The original interactive HTML pages of the three artifacts above. Open in a browser. | Project artifacts |

Also in the repo, next to their mods:

- `Server Hosting Business/hosting-rates.html`: the later "power at cost" version of the rates page.
- `Realistic Population growth/population-data-review.md` and `population-data.csv`: yearly population table with sources.

The electricity grid-only baseline that was pending (`software-inc/pending-doc-electricity-baseline.md` in the project files) is already part of `servers-and-monetization.md` ("Grid-only baseline (no generation)").
