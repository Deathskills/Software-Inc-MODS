# Server Hosting Business

A Software Inc. code mod that makes server hosting a real business line, alongside making software and hardware.

## What it changes

**Bandwidth (needs 0HarmonyLoader)**
- Every operating system (Computer, Console and Phone OS) needs a baseline bandwidth per user that grows each year.
- Antivirus and Office Software need a baseline when the Network feature is ticked. Office with Cloud backup adds a second "cloud" baseline.
- Multiplayer and MMO games follow new curves instead of vanilla's slow log curve.
- Vanilla add-ons (Auto-update, App store, Cloud backup, Virus database) keep their vanilla values and are added on top.
- Rates follow the current in-game year, so older products also need more bandwidth as time passes.

Mbps per 100k active users at selected years:

| Year | Computer OS | Console OS | Phone OS | Antivirus (Network) | Office (Network) | Office cloud | Multiplayer | MMO |
|---|---|---|---|---|---|---|---|---|
| 1990 | 0.05 | 0 | 0 | 0.05 | 0.03 | 0 | 62 | 477 |
| 2000 | 3 | 0.1 | 0 | 3 | 2 | 0.1 | 120 | 618 |
| 2010 | 35 | 12 | 5 | 25 | 15 | 8 | 350 | 759 |
| 2020 | 220 | 125 | 95 | 60 | 35 | 180 | 500 | 900 |
| 2025 | 355 | 191 | 142 | 75 | 45 | 560 | 525 | 945 |

- Between the 5-year points, values are linear. A point at 0 stays 0 until the next point.
- After 2025, the baselines keep growing at their 2020–2025 rate per year. Multiplayer and MMO grow 5% every 5 years.
- MMO is the whole game (Multiplayer + MMO).

**Hosting offers**
- Vanilla still sends one offer when an AI product that needs servers is released.
- Each month the mod also sends up to 3 offers for AI products (not subsidiaries) that aren't hosted yet and need more than 1 Mbps, including older products. The biggest products are offered first. A product is offered again at most every 6 months.
- An offer stays open for 24 months, then expires if you haven't accepted or refused it.
- You still need a receptionist to receive offers, as in vanilla.

**Hosting pay**
- Price per Mbps per month = ISP cost + power (both exactly as vanilla computes them) + profit.
- Profit per Mbps per month: Multiplayer $2.00, MMO $6.00, OS / Antivirus / Office $8.55.
- Business stars scale the profit from 80% (0 stars) to 120% (5 stars).
- A deal's price per Mbps is locked when the offer is made, like vanilla. Its bandwidth (and so its total pay) still grows with the curves.
- Other server products (for example distribution platforms) keep vanilla pricing.

**Unchanged on purpose**
- User retention. Vanilla keeps users of server products longer. The bandwidth this mod adds doesn't count for that: only products with the vanilla Multiplayer/network server features get the bonus, exactly as in vanilla.
- The vanilla power-cost formula in deal pay.

## Requirements and install

1. Install **0HarmonyLoader** (Harmony 2.3.3). This mod doesn't include or modify any Harmony files.
2. Copy this whole folder to `Software Inc\DLLMods\Server Hosting Business\`. Keep `ServerHostingBusiness.dll` as the only `.dll` in the folder.
3. Enable it in the mod list and **start a new game**. It isn't meant for existing saves.

Without 0HarmonyLoader the mod still loads, but only the offer and pricing parts run.

## Compatibility

- Patches two methods, both with postfixes only: `SoftwareProduct.GetLoadRequirement` and `ServerDeal.StillValid`.
- Uses none of the namespaces or types the game blocks for mods (for example `System.Reflection`, `System.IO` and `SINetworking`), so it passes the mod security check without an "all access" prompt.
- Doesn't change any data files, features or software types.
- Works alongside **Realistic Population growth**. Bigger populations mean bigger userbases, so bandwidth and hosting income grow with it.
- Other mods that rewrite hosting deals or server load (for example ones that replace `GetLoadRequirement` or deal pricing) will conflict.

## Building

The source is in `src/`. Build with the .NET SDK:

```
dotnet build src/ServerHostingBusiness.csproj -c Release -p:ManagedDir="<game>\Software Inc_Data\Managed"
```

Lib.Harmony 2.3.3 is referenced for compiling only and isn't copied to the output.
