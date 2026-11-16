# MegaMod compatibility (15 Nov 2026)

Marc subscribed to two Workshop mods by OmagaGod and asked for our three mods to work with them. This is the one exception to project rule 2 (other installed mods stay out of analysis): these two were named by Marc.

| Mod | Workshop id | Kind | Version read |
|---|---|---|---|
| MegaMod Data Beta 1.8 | 3813625905 | Data mod (software types, company types, hardware designs, name generators) | 4.8.0 |
| MegaMod Core Beta 1.8 | 3813626791 | Code mod, source `.cs` files, no Harmony | 4.10.0 |

Full per-feature tables: [megamod-server-features.md](megamod-server-features.md).

## Findings

- **No server furniture** in either mod. "Rack Cabinet", "Tower", "Router" in `HardwareDesign/` are case designs for hardware products. Server capacity, wattage and the hosting-deal price basis stay vanilla.
- **MegaMod Core has no Harmony patches**, so it cannot collide with our patches. It works through `TimeOfDay` events and `Update()`.
- **Population:** `MegaPopulation.cs` writes `MarketSimulation.Population`, `FanPopulation` and `FollowerPopulation` monthly (single player only, option "Population follows the real year", on by default). Its market is capped at 600M; ours goes to 1.4B.
- **Deal manager:** `MegaDeals.Accept` auto-accepts hosting deals and registers them on the non-cloud server group with the most spare hosting capacity. It cannot see ISP load, so it can pick an ISP pool, which then leaves ISP mode.
- **Server load:** 125 of MegaMod Data's 163 software types have `Server` features. Game is the extreme case (MMO 0.07 and MMORPG 0.07 against vanilla MMO 0.002).
- MegaMod Data only applies to new games. So does Server Hosting Business.

## What we changed

| Mod | Change |
|---|---|
| Realistic Population growth | Re-asserts its population every frame, so it always wins over MegaMod Core. Fans and followers now scale with the population at the vanilla ratio (Marc, 15 Nov) and are enforced the same way. |
| Server Hosting Business | Game classification uses term sets instead of the two vanilla names (below). Vanilla results are unchanged. |
| ISP Business 0.6.1 | When MegaMod Core is active and hosting lands on an ISP pool, the items are moved to the hosting group with the most spare capacity and the pool stays in ISP mode. Without MegaMod Core, or with no hosting group to move to, the old rule applies (the pool leaves ISP mode). |

### Game terms in Server Hosting Business

- **MMO** (any one makes the game an MMO): MMO, MMORPG, Persistent Servers, Persistent world servers, PvPvE servers.
- **Multiplayer** (any one, when no MMO term is present): Multiplayer, Online Multiplayer, Dedicated Server.
- **Online play covered by the curve** (dropped from the kept load, never a trigger): Online Lobby, Live Chat, Reconnection at Match, Automatic Balancing, Lag Compensation, Crossplay, LAN Matchmaking, NAT Traversal, Encrypted Multiplayer, DDoS Protection, Session Persistence, Server Browser, Matchmaking with MMR, Anti-Cheat Multiplayer, Cross-Region Play, Voice Chat, Spectator Mode, PvP, Battle Royale Mode, Ranked Matchmaking, Online tables, Role queue, Asynchronous messages. Added by Marc's decision on 15 Nov: Cloud Saving, Live Service Platform, Global Ranking, Clan & Guild System, Guild system, Multi Panel, Multiplatform, Remote Play, Remote Play Together.
- **Still kept at MegaMod's value:** the general services that single-player games can also have, about 0.006 in total: Cloud Sync, Trading System, Anti-Toxicity (0.001 each), Workshop Support, Telemetry (0.0005 each), Hotfix System, Crash Reporting, Microtransactions, Anti-cheat (0.0003 each), Cloud saves, Battle pass, Account protection, Seasonal Ranking, AI upscaling (0.0002 each), Ranking Rewards.

OS (Computer, Console, Phone), Antivirus (Network spec) and Office Software (Network spec, "Cloud backup") already matched by name. By Marc's decision on 15 Nov the 8 extra OS device types reuse the existing curves: Server, Supercomputer = Computer; Smart TV, VR Headset, Emulator = Console; Tablet, Watch, Car = Phone. Their own feature loads stay on top, as for the original three.

## Not changed: candidates for a compatibility option

These keep MegaMod's raw `Server` values and vanilla hosting pricing. Listed so Marc can pick what a later option should cover.

**The 8 extra OS device types** (now on our curves, see above; listed for reference) (features with a server load that each can use; 0.0002 each unless noted):

| Device | Online features |
|---|---|
| Tablet | Auto backup, Remote management, Certificate store, Theme store, Cloud saves, Store integration, Cloud clipboard, Remote wipe, Cloud photo library, AI copilot, Streaming to TV, Settings sync, Microphone surveillance, Push notifications, App store, Background sync, Account sync, Usage telemetry (0.0004), Cloud backup, Find my device, Phone link, Voice assistant (0.0003), Smart suggestions, Auto-update |
| Watch | Find my phone, Watch face store, Microphone surveillance, Push notifications, App store, Background sync, Account sync, Usage telemetry (0.0004), Cloud backup, Find my device, Phone link, Voice assistant (0.0003), Smart suggestions, Auto-update |
| Car | Backup camera, Over-the-air updates, In-car app store, Microphone surveillance, Push notifications, App store, Usage telemetry (0.0004), Cloud backup, Phone link, Voice assistant (0.0003), Smart suggestions, Auto-update |
| Server | Backup signing, UEFI firmware updates, Driver store, Media streaming server, Remote desktop, Auto backup, Remote management, Certificate store, Backup agent, Remote management console, Cloud migration tools, Cloud server backup, Auto-update |
| Smart TV | Smart apps, Streaming app suite, Voice remote, Cloud gaming app, Microphone surveillance, Push notifications, App store, Usage telemetry (0.0004), Voice assistant (0.0003), Smart suggestions, Auto-update |
| VR Headset | App store, Auto-update |
| Emulator | Netplay, Auto-update |
| Supercomputer | Auto-update |

Console also gained: Streaming apps, Online multiplayer, Digital store, Remote play, Cloud gaming, Cross-play, Platform subscription (0.0006). These add to our Console curve today.

**Anti-Piracy** (no vanilla equivalent; 25 features, sum 0.39): Cloud License 0.1, Microsegmentation 0.1, Advanced Telemetry 0.08, Private Cloud 0.05, Continuous Verification 0.03, MITM Detection 0.01, Smart Protection 0.008, Telemetry 0.006, Secure Communication 0.002, Detection System 0.001, Session Linking 0.001, mTLS 0.0007, Single Control/IP 0.0003, Communication Logs/Server 0.00014, User Clause, Challenge Authentication, Temporary Token (0.0001 each), and the SSL/TLS versions and Central Clock (0.00001 each).

**Game Engine** (30 features, sum 0.13): Built-in Anti-cheat 0.09, Dedicated Server 0.01, Dedicated Server Support 0.003, Server-side synchronization 0.003, MMO, PvP, PvE, Ranked, Open World Multiplayer, Cross-Platform Play, Matchmaking System, Project staging (0.002 each), Anti-Cheat System, Voice Chat Integration, Client/Server Architecture, Proximity Server (0.001 each), Entry Prediction 0.0004, Packet Compression 0.00035, Multiplayer 0.0003, Cloud collaboration, Generative asset tools, Neural NPC dialogue, Platform & monetization (0.0003 each), and smaller ones.

## Hosting pay, in plain terms

A hosting deal pays a price per Mbps the product uses. Our mod sets that price (ISP cost + $7 power + profit) and it does not change with MegaMod. What changes is the number of Mbps:

- Income from a deal = price per Mbps × Mbps used. You also need servers for those Mbps.
- A vanilla MMO with 100,000 users in 2020 uses about 565 Mbps. The same game built from MegaMod's features could use up to about 58,700 Mbps, so the same deal would pay about 100 times more and need more than a Big Rack.
- With the term sets that goes back to our curve (about 900 Mbps in 2020), plus whatever small general services the game has.

So profit per Mbps is the same; deals are just much bigger wherever MegaMod's raw values are kept.

## MegaMod's "Internet Service Provider" product type (designed 15 Nov, Marc chose all three)

MegaMod Data adds a normal software type named exactly "Internet Service Provider" (unlocked 1989; categories Dial up service, Online service portal, Broadband service 1999). It is the retail side: a product with subscribers and monthly fees, almost no server load, and no link to network capacity. AI company types "Internet Providers" and "Hosting Companies" release it. ISP Business is the network side. The integration keys only on the type name, adds no Harmony patches and no protocol or save-format break.

| | Rule |
|---|---|
| A | Each of the player's own ISP products becomes an ISP process (`sub:<product id>`). Demand = subscribers x the municipal per-user traffic curve (`Municipal.UsageMBPerUser`, no adoption factor). Traffic carried by the player's pools pays wholesale transit; traffic not carried pays the vanilla retail ISP rate. Both are added to the monthly Internet bill. |
| B | For AI ISP products, `Deals.DemandOf` adds the same subscriber traffic, so they ask for ordinary product ISP deals sized by their subscribers. No new deal kind. |
| C | Server Hosting Business does not send its extra hosting offers for ISP-type products while ISP Business is active. Vanilla's own release-time hosting offer is left alone. |

Scale check: one subscriber is about 0.00006 Mbps in 1995 and about 0.46 Mbps in 2020, so 100,000 subscribers need about 6 Mbps in 1995 and about 46,000 Mbps in 2020.

Not done: poor capacity does not hurt the product's sales (that would need patching sales code). Municipal ISP is kept as is.

## Multiplayer (noted, not addressed)

Priority is single player. For later:

- MegaMod Core switches population, events, rivals, cheats and its deal manager off in multiplayer. Ours stay on, so in multiplayer our population mod is the only one writing the market size.
- Every player needs the same MegaMod Data version, or software types differ between games. Vanilla does not check mods.
- Server Hosting Business only prices and offers on the host (`IsClient()` guard); the term sets do not change that.
- The ISP pool guard only reacts to MegaMod's deal manager, which is off in multiplayer, so it does nothing there.
- Untested: MegaMod Core's "active users never grow past copies sold" and technology-upgrade helpers change `Userbase`, which feeds both hosting and ISP demand.

## Open

1. Done 15 Nov: fans and followers scale with our population and override MegaMod's.
2. Done 15 Nov: Cloud Saving and the other online game features moved under our curves.
3. Done 15 Nov for the 8 OS device types. Anti-Piracy and Game Engine are still on MegaMod's raw values.
4. Nothing here has been tested in game.
