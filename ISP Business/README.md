# ISP Business

A Software Inc. code mod that lets you run your own Internet Service Provider. It's built from the design spec v0.7 in milestones. This version has **Milestone A (the ISP network core)**, **Milestone B (product ISP deals with AI companies)** and **Milestone C (company-wide ISP deals)** and **Milestone D (municipal ISP)**.

## What it does

**ISP Network window**
- Open it from its button on the bottom bar. The counter on the button shows open ISP offers.
- Your ISP pools only in the same table as the vanilla Servers window: status, colour, name, mode, bandwidth, servers, load, processes, electricity, fallback, Select fallback and View (moves the camera to the servers). **Add server group** turns a hosting group into an ISP pool, and **Back to hosting** reverses it. The vanilla Servers window also gets an **ISP mode** button on each group.
- Below the table are the ISP processes on the selected pool: your company's internet and one per ISP deal. Use **Move selected processes** to move them to another pool, like vanilla.
- The **Deals window** button opens the vanilla Deals window, where ISP offers and deals live (see below).

**Furniture shop:** a server's Bandwidth line also shows its ISP bandwidth, e.g. "Bandwidth: 1 Gbps   ISP: 15 Gbps".

**ISP pools**
- Any of your server groups with nothing assigned to it can be switched to ISP mode. It's then an ISP pool.
- A group is either a hosting group or an ISP pool, never both. If you assign a product to an ISP pool, it goes back to hosting mode and you get a message.
- Failure and fallback work exactly like vanilla. If one server in a pool breaks, the whole pool is down, and its ISP processes follow the pool's fallback group. Only an ISP pool can carry ISP traffic, so if the fallback is a hosting group, or there's no fallback, the traffic isn't served.
- If a pool carries more than its capacity, every process on it gets the same share of what's available.

**ISP capacity and power**

| Server | Hosting (vanilla) | ISP mode | ISP power at full load |
|---|---:|---:|---:|
| Small Server | 50 Mbps | 50 Mbps | 6 W per Mbps |
| Medium Server | 250 Mbps | 250 Mbps | 4 W per Mbps |
| Tower Server | 1 Gbps | 15 Gbps | 2 W per Mbps |
| Server Rack | 5 Gbps | 50 Gbps | 0.25 W per Mbps |
| Big Server Rack | 50 Gbps | 250 Gbps | 0.053 W per Mbps |

- Power use scales with how busy the pool is, like vanilla. An idle pool draws almost nothing.
- Idle ISP servers wear 4× slower than vanilla. Wear rises with load to the vanilla rate at 75% and 1.25× at full load.
- Server furniture from other mods keeps its own bandwidth and wattage in ISP mode.

**Your own internet through your ISP**
- Your company's internet is an ISP process. Your first pool picks it up automatically, and you can move it.
- Each hour, the traffic on your vanilla "Internet" bill (released products, hosting deals and your store; projects in development don't use the internet) is carried by that pool, up to its capacity.
- Traffic your pools carry costs wholesale transit instead of the vanilla ISP rate. Transit starts at $0.50 per Mbps per month in 1980 and falls to $0.05 in 2050.
- Traffic above your capacity, or while your pools are down, is billed by the vanilla ISP as usual.
- There's no fake income: you only save money, and you still pay the electricity for your ISP servers.
- Costs are the same per month whatever days-per-month setting you play with.

**Municipal ISP (towns)**
- Eight settlements (3 rural, 3 towns, 2 cities) with their own population, separate from the game's: rural 2,500, town 10,000 and city 50,000 people in 1980, growing to 40 million by 2080, 2065 and 2050. Nothing vanilla is changed.
- Town offers arrive in the Deals window: a small first deal (100 / 200 / 300 residents), then expansions at 3, 6, 9 and 12 months (up to 2x, 4x, 8x, then the whole available population), and growth offers every 6 to 12 months after that. Expansions need 90%+ service there last month.
- Bandwidth: residents x internet adoption (5% in 1980, 70% in 2020, 100% in 2080) x data per user per month (1 MB in 1980 up to 740 GB in 2080) x 8 / 2,592,000 seconds. It's tiny in the 1980s, so towns also pay a coverage payment.
- Pay: delivered Mbps x $5.06 x the star multiplier, plus a coverage payment per town of $1,000 + $25 x the square root of the residents you cover there, a month. It's worked out once on all your coverage in that town, so many small deals don't stack, and it's only paid for the share of traffic you actually deliver. Paid under Deals as "ISP municipal".
- Breaches: under 90% service for 3 months in a row brings a breach notice and a reputation hit. Under 75% for 5 months, or a whole month with no service, and the town sues you through the vanilla lawsuit system (court or settle). One lawsuit per continuing breach.
- The **Towns** tab of the ISP window shows each settlement's population, your coverage, other players' coverage, what's still available, your bandwidth, coverage pay, last month's service and breach status, with a **Consolidate** button to merge your deals in a town.
- In the Deals window the Company column of a town deal shows the game's "Public Domain" company (towns aren't companies); the description names the town.
- Multiplayer: players share each town's population. Each player tells the others how many residents they cover.

**Company-wide ISP deals (from 1990)**
- Each month, up to 4 AI companies needing at least 1000 Mbps in total offer a company-wide deal, separate from the 10 product offers.
- It covers every product the company owns, now and later (it follows trades and buyouts), plus its development traffic, worked out with vanilla's own formula for projects in development (team size x DevTime / 24, times the era's bandwidth factor). Vanilla's internet billing isn't changed.
- Accepting it folds your product deals with that company into it, without penalty. Products another player already serves stay theirs and are left out, so nothing is billed twice.
- It ends without penalty when the company closes or a player buys it. Being bought by an AI company keeps it.

**ISP deals with AI companies**
- Each month, up to 10 AI products that need at least 1 Mbps offer you an ISP deal, ranked by value. You need a receptionist, and deals must be on in the difficulty settings.
- Offers show up in the vanilla **Deals window** under Incoming deals (description "ISP service for ...") at the receptionist's time, and accepted ones under Active deals. Accept, Reject and Cancel work as for vanilla deals. The details panel shows the price per Mbps, the bandwidth needed, the monthly income, and for active deals the delivered share, Performance, pool and earnings. The "Offer" column uses the same unit as hosting deals (income per Mb per hour, the monthly price / 24). The customer pays daily through vanilla's deal payments, so ISP income shows under **Deals** in the finance bills as "ISP" (bills show last month, so it appears after the first month change).
- ISP deals are kept out of the vanilla part of the save and stored in the mod's own data, so a save still loads after you remove the mod (the ISP deals are simply gone then).
- Price: $11.45 per Mbps per month × the vanilla business-star multiplier (1.05 at 0 stars up to 1.25 at 6 stars). It's locked when the offer is made.
- Offers stay open for 24 months. A declined product isn't offered again for 6 months.
- An accepted deal goes to the pool with the most spare capacity. You're paid every hour for the Mbps you actually deliver, and you pay transit on it.
- Its bandwidth follows the product's live users, including the bandwidth curves of Server Hosting Business if you use it.
- Performance starts at 1.0 and changes every month with the share you delivered: +0.06 at 100%, +0.03 from 98%, 0 from 96%, −0.02 from 95%, −0.08 from 90%, −0.20 below 90%. At 0 the customer cancels.
- Good months (98%+) raise business reputation and bad months (under 90%) lower it, scaled by deal size. Cancelling a deal yourself also costs reputation.
- A deal ends without penalty when the product leaves the market or needs under 1 Mbps for 6 months.
- Hosting and ISP are separate: a product can have a hosting deal and an ISP deal at the same time.

**Multiplayer**
- Each player runs their own ISP for their own company. No extra syncing is needed, because the game simulates each player's servers and bills on their own machine.
- The mod checks which players run it, and which version.
- When you accept an ISP deal, the other players are told, so they aren't offered that product and can't hold it at the same time. If two players accept at the same moment, the lower player number keeps it and the other deal is withdrawn without penalty.
- Agreements between players come in a later milestone.
- A player without the mod plays normally.

## Requirements and install

1. Install **0HarmonyLoader** (Harmony 2.3.3). This mod doesn't include or modify any Harmony files.
2. Copy this whole folder to `Software Inc\DLLMods\ISP Business\`. Keep `ISPBusiness.dll` as the only `.dll` in the folder, next to `icon.png` and `meta.tyd`.
3. Enable it in the mod list. It works in new and existing saves.

Without 0HarmonyLoader the mod stays off.

## Compatibility

- Patches three methods, with prefixes and postfixes only and low priority:
  - `TimeOfDay.UpdateHour`
  - `ServerGroup.RefreshUsage`
  - `GameSettings.ChangeServerName`
- **Server Hosting Business:** compatible. The two mods patch different methods. This mod never changes `Server.GetISPCost` (which hosting prices use), hosting deals or product bandwidth, and its deals use their own id range in the Deals window. ISP deals only read product bandwidth, so they follow Server Hosting Business's curves when it's installed.
- **Realistic Population growth:** compatible. Nothing population-related is touched.
- Uses none of the namespaces or types the game blocks for mods, so it doesn't need the "all access" prompt.
- ISP data is stored only as text in the save's mod data, and no custom objects go into the save. Removing the mod returns ISP pools to normal server groups.

## Building

```
dotnet build src/ISPBusiness.csproj -c Release -p:ManagedDir="<game>\Software Inc_Data\Managed"
```

Lib.Harmony 2.3.3 is referenced for compiling only and isn't copied to the output.
