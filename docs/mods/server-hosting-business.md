# Server Hosting Business: status and history

Turns server hosting into a real business line. Source in `Server Hosting Business/src/`, user docs in `Server Hosting Business/README.md`, rate model in [../reference/hosting-rates-and-income-model.md](../reference/hosting-rates-and-income-model.md), power price study in [../reference/hosting-power-options.md](../reference/hosting-power-options.md).

## Current state (14 Nov 2026)

- Repo (default branch) has the merged state from PRs #2 and #3, including a prebuilt `ServerHostingBusiness.dll` (sha256 `be75ec0c…885e`).
- **4090:** runs the repo DLL since 15 Nov. Its previous PR #6 build (`3efcc675…f137`, built 13 Nov from the unmerged PR #6 source) is in `ModBackups\Server Hosting Business pre-repo-PR3\`.
- **Laptop:** `f52c78fb…29d9` (checked 14 Nov), which matches no known build. Install the repo DLL there to get both PCs in step.
- Needs 0HarmonyLoader. Meant for new games, not existing saves.

## MegaMod compatibility (15 Nov 2026, PR #11 merged, installed on the 4090)

- Games are classified by term sets (MMO, multiplayer triggers, covered online-play features) so MegaMod Data's game features follow the mod's curves; vanilla results unchanged.
- MegaMod's 8 extra OS device types reuse the existing curves: Server, Supercomputer = Computer; Smart TV, VR Headset, Emulator = Console; Tablet, Watch, Car = Phone.
- Anti-Piracy and Game Engine still use MegaMod's raw values. Repo DLL rebuilt: sha256 `507b2227…0e3d`. Details in [../reference/megamod-compatibility.md](../reference/megamod-compatibility.md). Untested in game.

- 15 Nov 2026, branch `claude/megamod-isp-products` (installed on the 4090): no extra hosting offers for products of type "Internet Service Provider" while ISP Business is active (those pay through ISP Business). Vanilla's release-time offer is untouched. Repo DLL rebuilt: sha256 `296b9982…f725`.

## History

| PR | What |
|---|---|
| #2 | Mod added. Era-based bandwidth curves for OS, Antivirus and Office with Network, new Multiplayer and MMO curves. Extra hosting offers for unhosted AI products over 1 Mbps (no monthly cap, offer threshold lowered to 1 Mbps), AI-owned subsidiaries can ask for hosting. Load-error fixes (dropped `System.Reflection` / `System.IO` use to pass the security check). |
| #3 | Hosting power priced as a fixed $7 per Mbps per month (Marc's choice among $4/$6/$7; an intermediate version priced power from the player's own servers). |

Pay per Mbps per month = ISP cost (vanilla `Server.GetISPCost`) + $7 power + profit (Multiplayer $2.00, MMO $6.00, OS/Antivirus/Office $8.55), profit scaled 80%–120% by business stars. Price locked when the offer is made.

Expected deal-window rates at 4 stars after PR #3 (city map, per Mb per hour): OS / Antivirus / Office about $1.40, MMO about $1.29, Multiplayer about $1.11. At 3 stars an OS offer should show about $1.38. Marc had not yet confirmed a live offer when the thread went quiet.

## Open items

1. **Confirm an in-game offer** matches the expected rates above.
2. **Optional:** small display-only patch so "Maximum bandwidth usage" in the deal window counts the mod's bandwidth instead of showing 0. Offered, not requested.
3. **Draft PR #6** also changes this mod: offers are priced as they arrive (vanilla hosting offers too), repricing is off in multiplayer, and open mod offers for products under 24 months old can expire on load. Unmerged; its build ran on the 4090 until 15 Nov (backed up, see above).
4. Business-star scaling uses 0–5 stars; vanilla max is 6 stars (`BusinessStars = floor(rep × 6)`). Worth a check.
5. **Double pay with ISP Business** on the same traffic, and much larger ISP demand when both are installed (see `isp-business.md`).
6. Interaction details with ISP Business (both postfix-only, different Harmony IDs, no overlapping patches) are listed in [../reference/isp-phase1-findings.md](../reference/isp-phase1-findings.md) §2.
