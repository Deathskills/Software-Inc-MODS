# ISP Business

A Software Inc. code mod that lets you run your own Internet Service Provider. It's built from the design spec v0.7 in milestones. This is **Milestone A: the ISP network core**.

## What it does (Milestone A)

**ISP pools**
- Open the **ISP Network** window from its button on the bottom bar.
- Any of your server groups with nothing assigned to it can be switched to ISP mode. It's then an ISP pool.
- A group is either a hosting group or an ISP pool, never both. If you assign a product to an ISP pool, it goes back to hosting mode and you get a message.
- Server failure works exactly like vanilla: if one server in a pool breaks, the whole pool is down. Repair it with IT staff as usual.

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
- Each hour, your company's internet traffic (the vanilla "Internet" bill) is carried by your ISP pools, up to their capacity.
- Traffic your pools carry costs wholesale transit instead of the vanilla ISP rate. Transit starts at $0.50 per Mbps per month in 1980 and falls to $0.05 in 2050.
- Traffic above your capacity, or while your pools are down, is billed by the vanilla ISP as usual.
- There's no fake income: you only save money, and you still pay the electricity for your ISP servers.
- Costs are the same per month whatever days-per-month setting you play with.

**Multiplayer**
- Each player runs their own ISP for their own company. No extra syncing is needed, because the game simulates each player's servers and bills on their own machine.
- The mod checks which players run it, and which version. Agreements between players come in a later milestone.
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
- **Server Hosting Business:** compatible. The two mods patch different methods. This mod never changes `Server.GetISPCost` (which hosting prices use), hosting deals, product bandwidth or the deal list.
- **Realistic Population growth:** compatible. Nothing population-related is touched.
- Uses none of the namespaces or types the game blocks for mods, so it doesn't need the "all access" prompt.
- ISP data is stored only as text in the save's mod data, and no custom objects go into the save. Removing the mod returns ISP pools to normal server groups.

## Building

```
dotnet build src/ISPBusiness.csproj -c Release -p:ManagedDir="<game>\Software Inc_Data\Managed"
```

Lib.Harmony 2.3.3 is referenced for compiling only and isn't copied to the output.
