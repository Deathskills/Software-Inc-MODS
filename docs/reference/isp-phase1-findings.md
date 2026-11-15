# Player-Owned ISP Mod: Phase 1 preliminary findings

Spec: Design & Technical Specification v0.7. Status: **preliminary**. Written 13 Nov 2026.

The assembly inspection itself (spec §23 Phase 1) has not happened yet, because the link to Marc's PC failed to connect in this thread. Everything below comes from three sources, and each point says which:

- **[SHB]** the Server Hosting Business mod source (repo + project files, fixed-$7 power version).
- **[Earlier]** vanilla code facts already read from Marc's install in the "Explain server mechanics" and "Rework server deals" threads.
- **[To verify]** my expectation, to be checked against `Assembly-CSharp.dll` before any code is written.

---

## 1. Hard constraints that shape the whole design

### 1.1 The game's mod security check  [SHB, verified by two load-error fixes]
The game rejects a mod DLL that references `System.Reflection`, `System.IO` or `SINetworking` (or shows an "all access" prompt). SHB had to drop both to load. Consequences for the spec:

| Spec item | Problem | Proposed alternative |
|---|---|---|
| §22 "configuration files" | No `System.IO`, so the mod can't read its own config file. | Option A: settings on the mod's options screen, stored per save in the save data. Option B: accept the "all access" prompt (more install friction). Option C: hard-coded defaults in one `Config` class, rebuilt per release. **Recommend A + C.** [To verify whether the game offers a sanctioned settings store for mods.] |
| §15, §16 multiplayer agreements, freeze, reconnection | No `SINetworking`, so the mod can't send its own network messages. Mod state on the host will not reach clients unless the game syncs mod save data itself. | See §4 below. |

### 1.2 The game decides when to save deals, not the mod  [To verify]
Vanilla deals live in `DealWindow.AllDeals` and are written into the save. If the ISP mod puts its own `Deal` subclass into that list, a save made with the mod may fail to load (or crash) once the mod is removed or fails to load, because the game can't recreate an unknown type. **Proposal:** ISP agreements live only in the mod's own save data. Offers are shown through the vanilla Deals window only if I can confirm the game skips or survives unknown deal types; otherwise they get their own list in the ISP window.

### 1.3 Server groups fail as a whole  [Earlier]
Vanilla: wired servers form one group, and **if one server breaks the whole group goes down** unless a fallback group is set. Spec §3.4 says pool capacity is the sum of available linked servers, so one failure only removes that server's share. These conflict.
- Option A (vanilla-faithful): an ISP pool *is* a vanilla server group, and it fails as a group. Redundancy = several pools plus vanilla fallback. Simple, no patching of group logic.
- Option B (spec-faithful): the mod sums per-server capacity itself and ignores the group's up/down state. Needs a patch or a parallel calculation.
- **Recommend A**, because it reuses vanilla wiring, fallback and UI, and it matches "reuse vanilla" in §25.
- **Decided by Marc (13 Nov):** vanilla whole-group failure, plus the vanilla fallback group. When an ISP pool goes down, its load moves to its fallback group if that group has spare capacity. No patch to group logic. Partial failure is not planned.

---

## 2. Conflicts with the Server Hosting Business mod (SHB)

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| S1 | SHB reprices **every incoming `ServerDeal`** it hasn't priced yet (hourly `RepriceDeals`) and extends `ServerDeal.StillValid`. If ISP offers were `ServerDeal` objects, SHB would overwrite their price and lifetime. | High | ISP offers must never be `ServerDeal` instances. |
| S2 | SHB prices hosting from `Server.GetISPCost()`. If the ISP mod patched `GetISPCost` to model internal ISP savings (§5.6), every SHB hosting price would drop. | High | Never patch `GetISPCost`. Apply internal savings at the billing call site instead (patch target to verify). |
| S3 | SHB postfixes `SoftwareProduct.GetLoadRequirement` and raises OS / antivirus / office / game loads a lot. ISP product demand (§6.2) that reads this method inherits SHB's curves, so ISP demand with SHB on is far larger than without it. | Medium (balance) | Don't patch `GetLoadRequirement`; only read it (× the era factor, same as SHB). Balance with SHB on, and state in the README that the two mods multiply each other. |
| S4 | Both mods send offers at the month boundary and both call `DealWindow.FindReceptionTime()`. SHB has no monthly cap, so in big games it can use up the receptionist's time and starve ISP offers, or the reverse depending on which mod updates first (order not controllable). | Medium | ISP offers on a different day of the month, plus a fixed share of reception slots reserved for ISP. |
| S5 | SHB skips products whose `ExternalHosting` points to a live deal. If ISP deals touched `ExternalHosting` / `ExternalHostingActive`, hosting offers would stop for that product. | High | ISP mod never writes those fields. Hosting and ISP stay separate as §17 requires. |
| S6 | Deal IDs: SHB keys its saved state by deal ID. An ISP mod inventing its own IDs could collide. | Low | Use the game's own ID generator, or a separate ID space in ISP save data only. |
| S7 | Both mods use `ModBehaviour.Serialize`. | Low | Distinct key prefix (`ISP_`), never read or clear `SHB_` keys. |

Harmony between the two: different Harmony IDs, both postfix-only on different methods, both `UnpatchAll(ownId)` only. No ordering dependency if the ISP mod follows S2/S3.

## 3. Conflicts with vanilla features

| # | Risk | Mitigation |
|---|---|---|
| V1 | **ISP servers with no products have ~0 load**, so vanilla charges almost no electricity and almost no wear (both scale with load). Spec §3.5 wants vanilla power and wear. | Feed the pool's delivered ISP load into the vanilla group load. [To verify which field/method.] |
| V2 | If V1 is done by adding load to the vanilla group, **vanilla also bills $18 × map factor per Mbps ISP** on that load, on top of the mod's transit. That is the double charge §20 warns about. | Patch the vanilla ISP billing to skip ISP-flagged groups (one prefix/postfix at the billing site). |
| V3 | Players can still assign products, source control or their store to an ISP group through the vanilla server window, which breaks hosting/ISP exclusivity (§3.1). | Either a postfix that refuses the assignment, or move items off ISP groups every hour with a notification. |
| V4 | Hosting deal pay is based on `ObjectDatabase.CheapestServer()` (cheapest unlocked server under 4 W/Mbps). The spec's fallback plan (ISP furniture variants with very low W/Mbps) would cut **all vanilla hosting deal pay** for everyone. | Don't add furniture. If ever needed, give variants a wattage that keeps them out of `CheapestServer`. |
| V5 | Vanilla "wattage" is not literal watts: the hourly power cost is wattage × 0.03 × price, scaled by load. The spec's W/Mbps table (§3.3) has to be mapped onto that formula. The spec's 250 Gbps Big Rack at 0.053 W/Mbps implies ~13,000 W, against vanilla's Wattage 50 (wiki example). | Reconcile once the furniture data is read. Likely: the mod sets its own ISP electricity per Mbps and the vanilla wattage stays as is. |
| V6 | Known vanilla bug: hosting deal **power** pay is divided by DaysPerMonth while the power bill isn't. | ISP billing never reuses deal pay code. It bills from elapsed in-game minutes ÷ minutes per month, which gives the same monthly totals for every DaysPerMonth (spec §4.4). |
| V7 | Frame skipping at high game speed: hour-change events can be skipped. | Settle from elapsed time between saved checkpoints, not "once per hour" events. This also makes reload idempotent (§20). |
| V8 | Lawsuits (§14): the vanilla legal system appears to be built around products and IP. A breach by a "municipality" has no company or product to sue from. [To verify.] | If it can't be represented safely: a mod-side breach process (notice → fine + reputation hit, once per breach), using vanilla notifications and transactions. |
| V9 | Municipalities aren't companies. A vanilla deal or lawsuit needs a client company. A fake company would show up in the market, stocks and AI logic. | Never create fake companies. Municipal deals live only in the ISP window. |
| V10 | Business stars: the spec's table has 0–6 stars, SHB assumes 0–5. | Verify the maximum and reuse vanilla's own multiplier roll. |

## 4. Multiplayer (spec §15–16): likely the biggest gap

- No `SINetworking`, so no custom messages. Unless the game syncs `ModBehaviour` save data or deals to clients itself, **clients won't see ISP state** for their own company, and no client-side UI could show it. [To verify.]
- **Freezing a whole offline company (§16.1)** means changing the game's company simulation for every system, not just ISP. That's very invasive and would conflict with any mod touching company simulation.
- Vanilla already has a multiplayer **cloud service** where a player sells server capacity to other players at a markup on ISP cost [Earlier]. That's the obvious reuse point for player-to-player ISP, if it can be extended.
- Every player must run the same mod version, or the host and client simulations differ.

**Superseded (13 Nov): Marc wants multiplayer support from the start.** Original proposal: build single-player first. Multiplayer goes in last as a host-authoritative layer, with player-to-player ISP built on the vanilla cloud-service flow where possible. The offline freeze would cover only the ISP ledger (no ISP revenue, expenses, breach months or offers while offline), not the whole company.

## 5. Crash and Harmony risks

| # | Risk | Mitigation |
|---|---|---|
| C1 | Save data arriving before `GameSettings.Instance` exists (load order). | Same pending-apply pattern SHB uses. Billing only starts after rebuild (§19.3). |
| C2 | Exceptions inside patches on hot paths (server tick, every frame) spam the log and drop FPS. | Try/catch in every patch, log once per kind, self-disable that feature after repeated failures. |
| C3 | Stale furniture references after move/sell/load. | Store stable IDs only, resolve every refresh, drop missing ones (§19.4). [Verify furniture keeps a stable ID across save/load; moving may recreate the object.] |
| C4 | Other Harmony mods patching the same server/deal billing methods, especially with skipping prefixes (`return false`). | Postfixes only, unique ID, `Priority.Low`, no transpilers. At start, log any other owner patching our targets (via Harmony's own API, no `System.Reflection` in our code). |
| C5 | Game updates rename or reshape methods. | Check every target exists before patching. If one is missing, turn off only that feature and say so on the options screen. |
| C6 | Money precision: municipal revenue reaches hundreds of millions a month late game (see §6), while settlements run in small hourly steps. | Settle in larger steps (daily) with double-precision running totals. |
| C7 | Removing the mod mid-save. | Nothing ISP-specific stored in vanilla objects. Note in the README that ISP groups go back to normal hosting and running agreements are lost. |
| C8 | Lifetime: the announced server overhaul (save-breaking, servers become products) will almost certainly break the infrastructure part of this mod. | Out of scope per your rule. Noted once, so it isn't a surprise later. |

## 6. Spec numbers worth a second look

Municipal bandwidth from §11.4 (full settlement coverage):

| Year | Settlement / population | Avg Mbps | Bandwidth pay/month at $5.31 |
|---|---|---:|---:|
| 1980 | first 100-person deal | 0.000015 | ~$0 |
| 1990 | whole City, 129,926 | 0.43 | ~$2 |
| 2000 | whole City, 337,617 | 137 | ~$730 |
| 2020 | whole City, 2.28M | 738,800 | ~$3.9M |
| 2050 | City at cap, 40M | 58M | ~$308M (232 Big Racks) |
| 2080 | any at cap, 40M | 91M | ~$485M (365 Big Racks) |

- Before about 1997 municipal bandwidth is effectively zero, so the **coverage payment is the entire municipal business** for 15+ years. Its formula (§12.2, open question 21) is the key balancing number.
- Between 2000 and 2020 demand grows about 5,400×. With SHB on as well, late-game server income may dwarf the rest of the economy.
- Q22 (peak vs average): average Mbps understates peak by roughly 2–3× in real networks. Suggest a configurable peak factor, default 1.0 to keep your economics.
- Corporate check: $11.45 × 1.05 − $1.81 electricity − $0.50 transit = $9.71 ✓ (matches §5.5).

## 7. Proposed delivery plan

1. **Phase 1 finish (needs your PC link):** read `Assembly-CSharp.dll` for server groups, power/ISP billing, deals, reputation, lawsuits, save, multiplayer sync. Update this document to verified/not verified.
2. **Milestone A, single-player core:** ISP pools on vanilla server groups, capacity, power + transit, internal ISP savings, versioned save data, a simple ISP window. Testable on its own.
3. **Milestone B:** product ISP deals + reliability/Performance/reputation.
4. **Milestone C:** company-wide deals + dev bandwidth.
5. **Milestone D:** municipal system + breach handling.
6. **Milestone E:** multiplayer, scoped by what §4 verification allows.

Each milestone ends with a PR and your in-game test before the next one starts.

---

## 8. Verified against the game code (13 Nov, Unity 2018.4.36f1, DLL mod build 24)

| # | Finding | Effect on the design |
|---|---|---|
| 1 | Hourly `TimeOfDay.UpdateHour` adds load of `UsesISP` items × `Server.GetISPCost()` (18 × ISPCostFactor) / 24 / DaysPerMonth to `GameSettings.ServerCost`. `TimeOfDay.UpdateMonth` pays it as the "Internet" bill. Fallback chains are resolved in the same hourly pass. | ISP pools contain no `UsesISP` items, so vanilla never bills them for internet. The V2 double charge doesn't happen as long as the mod doesn't put ISP load into vanilla items. |
| 2 | One broken server drops the whole group to zero power. | Matches the agreed choice (whole-group failure + fallback). |
| 3 | `DealWindow.AllDeals` is saved with the game's reflection serializer. A custom `Deal` subclass makes the save fail to load once the mod is removed. Over the network an unknown deal type is ignored. | Confirmed: ISP deals stay out of `AllDeals`. Only primitives in mod save data. |
| 4 | Max 6 business stars, `BusinessStars = floor(rep × 6)`. `ChangeBusinessRep(deltaStars)`. | Spec's 0–6 table is right. SHB's 0–5 star scaling should be checked (separate fix). |
| 5 | `GameSettings.LaunchSuit(new GameSettings.Lawsuit(company, subject, money, difficulty))` starts a lawsuit. Player chooses Court (LegalWork task) or Settle. | Native lawsuits are possible. To check: what `company` must be when the plaintiff is a municipality. |
| 6 | Mod save data is only in local saves and is never sent to multiplayer clients. `Writeable.SaveDLCData` can store data on individual furniture. | Persistence in multiplayer needs care (see below). |
| 7 | Multiplayer: each player simulates their own servers and deals. Money stays in sync because every transaction is broadcast, plus a host correction. A disconnected player's building and bills stop. Vanilla cloud service charges ISP × (1 + markup) per hour. `GameSettings.IsNetworkMode` / `IsHost`. | Each player's ISP business can run on that player's own client with vanilla transactions. Offline freeze (§16) comes for free. Player-to-player agreements are the only part that needs cross-player data. |
| 8 | Security blocks `System.Reflection`, `System.Net`, `System.IO.File*`, `NetworkManager`, `NetworkMessaging` and more. A public static `GiveMeFreedom` field on `ModMeta` skips the check after a one-time "all access" prompt. | Custom network messages and config files need the all-access prompt. |
| 9 | AI companies have no server or ISP costs. `SoftwareAlpha` load = team size × DevTime / 24, not billed for ISP. | Dev bandwidth is computed only inside the ISP demand aggregator, as the spec says. |
| 10 | `Writeable.DID` is stable across save/load. Server groups are saved by name. | Pools are keyed by group name + server DIDs. |
| 11 | `Company.BuyOut` handles AI bankruptcy/buyouts and cancels hosting deals without penalty. `MarketSimulation.OnCompanyClosed` fires on removal. | Hook for §7.6 dissolution handling. |
| 12 | Servers (cost / wattage / Mbps / unlock): Small $2,000 / 800 / 50 / start; Medium $3,000 / 2,000 / 250 / 1985; Tower $4,000 / 4,000 / 1,000 / 1990; Rack $8,000 / 20,000 / 5,000 / 2000; Big Rack $25,000 / 200,000 / 50,000 / 2005. Unlock years from a pattern match. | Vanilla W/Mbps: 16, 8, 4, 4, 4. See the wattage options. |

### Wattage options for ISP mode (spec §3.3)

Spec capacities: 50 / 250 / 15,000 / 50,000 / 250,000 Mbps.

| Option | Small | Medium | Tower | Rack | Big Rack | Notes |
|---|---:|---:|---:|---:|---:|---|
| A. Vanilla wattage at spec capacity | 16 | 8 | 0.27 | 0.40 | 0.80 | No power patch. Tower is the most efficient, and Big Rack is worse than Rack. |
| B. 20% of vanilla wattage | 3.2 | 1.6 | 0.053 | 0.08 | 0.16 | Closest to the old 20% goal. Same odd ordering. |
| C. Spec W/Mbps exactly (recommended) | 4 | 2 | 1 | 0.25 | 0.053 | Needs a patch on the power cost of ISP groups only. As a share of vanilla wattage at full load: 25%, 25%, 375%, 62%, 6.6%. |

All values in W/Mbps at full load. Power still scales with utilization, so idle capacity costs little, as in vanilla.

### Decided by Marc (13 Nov): ISP wattage
| | Small | Medium | Tower | Rack | Big Rack |
|---|---:|---:|---:|---:|---:|
| W/Mbps at full load | 6 | 4 | 2 | 0.25 | 0.053 |
| Rural electricity per Mbps per month | $2.72 | $1.81 | $0.91 | $0.113 | $0.024 |
| Full-load watts (spec capacity) | 300 | 1,000 | 30,000 | 12,500 | 13,250 |

Tower at 2 W keeps the same step down as Small → Medium. Group failure and fallback: exactly as vanilla. The "Server Group Resilience" mod is to be deleted.

## 9. Multiplayer agreements: verified options (13 Nov)

**Vanilla player cloud** (ServerWindow.ApplyCloudChanges, Company.CloudService, TimeOfDay.UpdateHour):
- The customer's load on the cloud group comes off its own ISP bill. The customer's machine pays `ISP × (1 + markup) / 24 / DaysPerMonth` per hour as a broadcast transaction pair (customer Bills −, provider Contracts +).
- The provider carries that load as a NetworkServerItem on its real servers and pays its own ISP on it.
- Markup slider: 0–200% in 10% steps, default 50%. **It can't go below 0, so a cloud can never be cheaper than vanilla ISP.**
- If the provider goes offline, the customer keeps paying at the last advertised capacity, and the host charges the provider ISP. The provider nets the markup while offline (conflicts with spec §16).
- The customer is billed for the full requested load even when overloaded.

**Mod network messages** (sanctioned API, no all-access prompt):
- Every machine calls `ParentMod.RegisterNetworkID(byte)` with the same id. Ids are first-come, so pick an uncommon one.
- Send with `ParentMod.SendNetworkMessage(byte[], MessageTarget, targetID)`. Targets: Everyone, EveryoneButMe, EveryoneExcept, Specifically, Host. Main thread only. Relayed by the host. Reliable and ordered (Steam reliable / LAN TCP). 8 KB flush chunks.
- Receive by overriding `ModMeta.ReceiveNetworkMessage(NetworkPlayer, MemoryStream)`. Self-delivery quirk: use EveryoneButMe.
- Only reaches players already in game, so resend state after a join. Peers without the mod drop messages silently.
- Allowed types only: no BinaryWriter (use MemoryStream + BitConverter), no NetworkManager (use GameSettings.IsHost / IsNetworkMode / MyCompany.NetworkPlayerID).
- Vanilla has no mod-list check (only the host's AllowCodeMods flag), so the mod needs its own version handshake.

**Recommendation:** the mod's own agreements over mod messages, with single-owner rules. The customer's machine owns billing and the premium cutoff. The provider's machine owns capacity and reputation. Messages carry only what the owner decided: provider → customer capacity + price + tier, customer → provider usage, and leave/fallback events. Money moves only as vanilla broadcast transactions made by the payer. Plus a version handshake on join and a state resend after a join.
