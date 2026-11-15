# ISP Business: status and history

Player-owned Internet Service Provider mod. Source in `ISP Business/src/`, user-facing docs in `ISP Business/README.md`, design spec in [../reference/isp-design-spec-v0.7.md](../reference/isp-design-spec-v0.7.md), verified game-code findings in [../reference/isp-phase1-findings.md](../reference/isp-phase1-findings.md).

## Current state (14 Nov 2026)

- **Version 0.6.0**, `Config.ModVersion = "0.6.0"`, `ProtocolVersion = 4`, `SaveVersion = 1`. Merged in PR #8.
- Installed on **both PCs** (4090 desktop and 3070 laptop). The two DLLs are not byte-identical (built on different machines) but come from the same source and protocol, so the two PCs can make agreements with each other. Laptop DLL sha256 starts `555b0c21`, 4090 build hash starts `A645EC80`.
- Laptop pre-0.6.0 files are backed up at `D:\SteamLibrary\steamapps\common\Software Inc\ModBackups\ISP Business pre-0.6.0\` (a copy is in the project files as `laptop-backup/ISP-Business-pre-0.6.0/`).
- The repo does not contain a built `ISPBusiness.dll`; build from source (see the root `CLAUDE.md`).

## Milestones delivered

| Version | PR | What |
|---|---|---|
| A | #4 | ISP network core: ISP pools on vanilla server groups, ISP capacity per server, ISP wattage and wear, transit, own-company internet through own ISP, multiplayer version handshake, ISP window. |
| B, 0.3.0 | #4 | Product ISP deals with AI companies, shown in the vanilla Deals window; ISP bandwidth in the furniture shop panel. |
| C, 0.4.0–0.4.1 | #4 | Company-wide ISP deals, ISP income under Deals, cleaner ISP window, never lose ISP deals on load. |
| D, 0.5.0–0.5.1 | #4 | Municipal ISP (one settlement per map type, offers only from the map's type). |
| 0.5.2–0.5.3 | #5 | Deals back in the Deals window after a load, monthly figures, town deals no longer end for using under 1 Mbps. |
| build fix | #7 | Reference `UnityEngine.TextRenderingModule` so the project builds. |
| 0.6.0 | #8 | Player-to-player ISP agreements (Players tab): standard and premium, reconnection fee, premium 95% cutoff, free endings on disconnect. |

## Design decisions made by Marc (keep these)

- **Multiplayer from the start** is a hard requirement.
- **Server failure:** exactly like vanilla. One broken server takes the whole pool down, and its load moves to the pool's vanilla fallback group if that group is an ISP pool with spare capacity. No partial-failure patch. (A standalone "Server Group Resilience" mod was built by mistake and deleted; it must not come back.)
- **Idle ISP capacity** uses little power (vanilla behaviour) and wears less: idle ISP servers wear 4x slower, rising to vanilla at 75% load and 1.25x at full load.
- **ISP wattage** (W per Mbps at full load): Small 6, Medium 4, Tower 2, Rack 0.25, Big Rack 0.053. **ISP capacity:** Small 50 Mbps, Medium 250, Tower 15 Gbps, Rack 50 Gbps, Big Rack 250 Gbps.
- **Player-to-player agreements:** option A, the mod's own network messages with "one side decides" rules. The customer's game owns billing and the premium cutoff, the provider's game owns capacity and reputation, and money only moves as vanilla broadcast transactions made by the payer. No vanilla player cloud. No "all access" prompt.
- Balance is a mix of municipal and corporate; slow early municipal scaling is accepted.

## Hard technical constraints (verified in vanilla code)

- The game's mod security check blocks `System.Reflection`, `System.Net`, `System.IO.File*`, `NetworkManager`, `NetworkMessaging` and more. Using them triggers the "all access" prompt (`GiveMeFreedom`). This mod must stay clean of them: use `MemoryStream` + `BitConverter`, not `BinaryWriter`; use `GameSettings.IsHost / IsNetworkMode / MyCompany.NetworkPlayerID`, not `NetworkManager`.
- Mod messages: `ParentMod.RegisterNetworkID(byte)`, `ParentMod.SendNetworkMessage(byte[], MessageTarget, targetID)`, receive via `ModMeta.ReceiveNetworkMessage(NetworkPlayer, MemoryStream)`. Main thread only, reliable and ordered, relayed by the host. Messages only reach players already in game, so state is resent after a join. Peers without the mod drop messages silently. Use `EveryoneButMe` to avoid self-delivery.
- Vanilla has no mod-list or mod-hash check between players; the lobby only carries a CodeMods on/off flag. The mod does its own version handshake. `*H.bin` in a mod folder is a local MD5 approval cache, so after a DLL swap the game asks once to approve the changed mod.
- **Never put a custom `Deal` subclass in `DealWindow.AllDeals`**: the save would fail to load once the mod is removed. ISP deals live only in the mod's own save data (text/primitives) and are shown through patches.
- Mod save data is local only and never sent to other players.
- Never patch `Server.GetISPCost` or `SoftwareProduct.GetLoadRequirement`, never write `ExternalHosting`, never make ISP offers `ServerDeal` objects (Server Hosting Business would reprice them). Use the `ISP_` key prefix in mod save data.
- Patched methods (prefix/postfix only, low priority): `TimeOfDay.UpdateHour`, `ServerGroup.RefreshUsage`, `GameSettings.ChangeServerName` (plus Deals-window display patches; see `src/Patches.cs` and `src/VanillaDeals.cs`).

## Open items

1. **Player-to-player ISP (0.6.0) is untested in a real multiplayer game.** Both players need 0.6.0. Agreements are not saved; after a reload everyone is back on the vanilla ISP.
2. **Spec items not built yet:** the Reliability tab (spec §13, §18) and an options screen for configuration (spec §22; no config files because `System.IO` is blocked, so settings would live in the options screen and per-save data, with hard-coded defaults in `Config.cs`).
3. **Draft PR #6 (Codex, "Improve Software Inc mod reliability and hosting offer consistency")** is still open and unmerged, based on the pre-0.6.0 state (`fd5938f`). Its ISP part (0.5.4) would need rebasing onto 0.6.0. Risks flagged in review on 13 Nov: AI customers start paying real money on the fallback path; new deals can end up with no pool and a misleading "you have no ISP pool" warning; some existing product deals may end after the update; its protocol number was not bumped. Decide with Marc whether to rebase, cherry-pick or close it.
4. **ISP + Server Hosting double pay:** ISP never checks for hosting deals, so one product can pay you for an ISP deal and a hosting deal on the same traffic. Hosting and ISP being separate was an explicit spec choice (§17), but the double pay was flagged as a balance risk and not yet decided.
5. With Server Hosting Business installed, product bandwidth is much larger, so ISP deals and capacity needs grow a lot too (by design, but check balance).
6. If a game update removes a patched method, a mod can be left half-patched. Check patch targets after each game update.
