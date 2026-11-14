# Software Inc. Servers and Monetization Reference

Nov 12, 2026 · Deathskills + Claude. Exported from the project's Claude Doc on 14 Nov 2026.

## Scope and sources

This covers the base game as currently installed (Beta 1.8.x, files dated 20 Aug 2026), with no mods. Announced future updates are left out.

The mechanics come from disassembling `Assembly-CSharp.dll` and reading the game's built-in data and changelog in `resources.assets`. Formulas are as written in code; nothing has been checked by playtesting. Where the official wiki disagrees with the code, the code is listed and the difference noted.

## How servers work

Servers are furniture that you wire into groups. Each group shares its load and costs money every hour for the bandwidth it actually uses.

- **Groups:** load is spread across every server in a group. If one server breaks, the whole group stops unless you've set a fallback group. Processes left without a server show as "unsupported".
- **What uses servers:** your own released products that have server features, plus your source control, IT stations and distribution platform. AI products don't use servers unless you host them through a deal.
- **Product load:** server need per user × userbase × the bandwidth factor (formula below).
- **Auto-release:** after 6 months on the market, a product using less than 1 Mbps is taken off its server for good.

```latex
\text{Mbps} = \text{ServerReq} \times \text{Userbase} \times \left(0.75 + \log_{10}(\text{year} - 1969)\right)
```

ServerReq is the sum of the selected features' `Server` values. Any `RelativeServer` values then multiply that sum.

| Cost or effect | How it's calculated |
| --- | --- |
| ISP | $18 per Mbps per month × the map's ISP cost factor. Charged hourly, only on bandwidth used. |
| Electricity | Server wattage × 0.03 × the electricity price, scaled by how busy the server is |
| Wear | Wear speed × max(1, load + 0.25): up to 1.25× faster above 75% load |

## Overload penalties

A product is overloaded when it gets less than 75% of the bandwidth it needs. It is then penalized every hour, and the penalty grows the longer the overload lasts.

Every hour, take *s* as the share of bandwidth the product received, and let *x* = *s* / 0.75.

1. **Users and awareness:** both are multiplied by 1 − (1 − *x*) × min(0.9, 0.05 × 1.25^incidents).
2. **Incident counter:** goes up by 1. The highest count the product ever reaches is kept.
3. **Fans:** you lose (1 − *x*) × 0.25 × userbase fans in the product's category. This doesn't apply to externally hosted products.
4. **Alert:** an "Overloaded" server notification appears.

**Recovery:** each hour the product gets 75% or more, the incident counter drops by 1.

**Refunds:** refund rate = min(1, peak incidents mapped 0–24 to 0–0.5, plus 0.75 × min(1, bugs / max bugs)²). Long overloads can add up to 50 points of refunds.

**DDoS:** a DDoS on a competitor's online product forces these same overload hours on it, which raises its refunds and lowers its market recognition. A DDoS on a product you host yourself is cancelled.

## Player monetization

Servers make you money in four ways. Subscriptions pay best. Hosting deals earn only a thin 5–25% margin.

| Income source | How it pays | Requirements and notes |
| --- | --- | --- |
| Subscription-based software | Monthly fee × active userbase, every month | Set at design time. Pricing uses a separate subscription ideal price. A boxed copy counts as 12 months (`PhysicalSubscriptionCutFactor` = 12). |
| Your own digital distribution platform | You set the cut. Third-party stores take 30% (`DistributionStandardCut` = 0.3). | Needs a server group and its own platform software. Every digital sale uses bandwidth, and you earn nothing from it if you don't have the capacity. |
| Hosting deals for AI products | Paid hourly: product load × rate per Mbps | Rate = a random 1.05–1.25× your cost per Mbps (ISP + electricity of the most efficient server unlocked), weighted by your business stars. Needs a reception desk with free time. |
| Cloud service (multiplayer only) | Other players pay per Mbps per month | You set the markup over ISP cost. The game notes that a 25–50% markup roughly breaks even, and 100% doubles the ISP price. |

**Hosting deal reputation:** a deal with no overloads raises your reputation in proportion to its load (load × bandwidth factor / 10,000,000). Any overload costs −0.1. Failing a deal doesn't hurt your market recognition.

## AI companies and servers

AI companies don't run simulated servers. Their online products never overload unless the player hosts them or DDoSes them.

- **Hosting offers:** each month, every newly released AI product with server needs creates one hosting offer, unless a subsidiary made it. You only get the offer if your reception desk has free time.
- **Offer lifetime:** the offer stays valid while the product is 24 months old or less.
- **Deal end:** a hosted product is dropped automatically once it is past 6 months and uses less than 1 Mbps.
- **Hosted AI product overloads:** these hit the AI product's users and refunds. They don't cost the AI company fans.
- **No subscriptions:** AI products are never subscription-based. The flag is hard-coded off in the AI's product creation and pricing (`SimulatedCompany.Simulate`).

## Market caps in the game

Every market is capped by one fixed population of 145,613,576 people, the same in every year. The game has no year-based adoption curve.

**Operating system ceiling:** category Popularity × population. This is the most users an OS category can ever reach.

| OS category | Popularity | Ceiling | Available from | User retention |
| --- | --- | --- | --- | --- |
| Computer | 0.95 | \~138.3M | game start | 72 months |
| Console | 0.75 | \~109.2M | game start | 48 months |
| Phone | 1.0 | \~145.6M | 1995 (Network features from 2000) | 36 months |

**Software customer cap:** for OS-specific software such as games, office and antivirus, the cap is the lower of two numbers. The first is category Popularity × population. The second is the OS coverage: the combined current userbase of every compatible OS, counting each OS line back up to 4 sequel generations (`SoftwareType.GetReach`, `MarketSimulation.GetOSCoverage`).

**How the caps change during a game:** the ceilings never move. OS coverage rises only as OS products sell into the remaining pool, and it falls as users drop off over the retention period. How fast it grows depends on marketing, quality and price, not the calendar. The hosting deal window's "maximum usage" uses this same cap.

**Modding:** `MarketSimulation.Population` is a hard-coded constant, so data mods can't change it. Category `Popularity` can be changed in data, but it is a single value with no year attached. Real adoption curves would need a code mod that scales one of them by year.

## Real-world sales compared with the game

Real markets grew more than 100× over these decades, while the game's ceilings stay flat at 109–146M from day one. The fixed population means the game can only match real life in shape, not in size.

**PCs, worldwide shipments**

| Period | Per year | Period total |
| --- | --- | --- |
| 1975–1979 | 0.005M → 0.58M | \~1M |
| 1980–1989 | 0.72M → 21M | \~78M |
| 1990–1999 | 20M → 113.5M | \~556M |
| 2000–2009 | 135M → 306M | \~2.09B |
| 2010–2019 | peak 353M (2011–2012) → 263M | \~3.03B |
| 2020–2023 | 275M → 340M (2021) → 242M | \~1.14B |

Source tables disagree for 1994–1995 (41M vs 47.9M, and 50M vs 59.7M); the totals above use the higher figures. Definitions also change over time, for example whether x86 servers are counted.

**Consoles, lifetime units by generation**

I found no reliable worldwide total for all consoles per year, so these are lifetime units summed by generation.

| Generation | Years | Home | Handheld |
| --- | --- | --- | --- |
| 2nd | 1977–1980s | \~39M | – |
| 3rd | 1983–1993 | \~128M (incl. \~41M NES clones) | – |
| 4th | 1987–1997 | \~113–118M | \~135M (Game Boy/GBC 119M) |
| 5th | 1994–2001 | \~148M | \~4M (Game Boy not counted twice) |
| 6th | 1998–2013 | \~232M | \~93M |
| 7th | 2005–2013 | \~273M | \~234M |
| 8th | 2012–2020 | \~189M, plus Switch \~157M | \~92M |
| 9th | 2020– | \~131M so far | – |

**Mobile phones and smartphones, sold per year**

| Year | All phones | Smartphones |
| --- | --- | --- |
| 1995–1996 | no data | essentially none |
| 1997 / 1999 | 108M / 284M | – |
| 2000 / 2005 | 413M / 817M | no data |
| 2006 | 991M | 64M |
| 2010 | 1,597M | no reliable figure |
| 2012 | 1,746M | 680M (Gartner) |
| 2013 | 1,807M | 968M (Gartner) |
| 2015–2019 | – | \~1.42–1.55B a year, peak \~2017–2018 |
| 2020–2021 | – | \~1.28–1.5B (sources disagree) |
| 2022 / 2023 | – | 1,206M / 1,167M (IDC shipments) |

Gartner counts sales to end users and IDC counts shipments. Other years come from a Wikipedia compilation that names no firm.

**What differs from the game**

- **Computers:** the game allows 138M PC users from 1980. Real PC shipments totaled only \~78M by the end of 1989, but more than 138M PCs were shipping every year by 2003.
- **Consoles:** the game's ceiling stays at 109M. Real sales per generation grew from \~39M to \~500M (7th gen).
- **Phones:** the game allows 146M phone users from 1995 and phone Network features from 2000. Real smartphone sales were 64M a year in 2006 and only took off after \~2010.

Real-world sources: [PC vendor market share (Wikipedia)](https://en.wikipedia.org/wiki/Market_share_of_personal_computer_vendors), [Gartner PC shipments 2023](https://www.gartner.com/en/newsroom/press-releases/01-10-2024-gartner-says-worldwide-pc-shipments-increased-zero-point-three-percent-in-fourth-quarter-of-2023-but-declined-fourteen-point-eight-percent-for-the-year), [Best-selling game consoles (Wikipedia)](https://en.wikipedia.org/wiki/List_of_best-selling_game_consoles), [Video Game Sales Wiki generation pages](https://vgsales.fandom.com/wiki/Fourth_generation_of_video_games), [Best-selling mobile phones (Wikipedia)](https://en.wikipedia.org/wiki/List_of_best-selling_mobile_phones), [Gartner smartphone sales 2013](https://www.gartner.com/en/newsroom/press-releases/2014-02-13-gartner-says-annual-smartphone-sales-surpassed-sales-of-feature-phones-for-the-first-time-in-2013), [IDC smartphone shipments 2023](https://www.businesswire.com/news/home/20240115096425/en).

## Durability, fire and staff repairs

Durability (the months shown on servers, wind turbines and so on) is `Upgradable.TimeToAtrophy`: how many months it takes Quality to fall from 1 to 0 under normal conditions. At Quality 0 the item breaks.

**Vanilla server values** (from the furniture prefabs in `globalgamemanagers.assets`):

| Server | Power (Mbps) | Durability (months) | FireStarter | MinutesToRepair |
| --- | --- | --- | --- | --- |
| Small | 50 | 32 | 0.05 | 15 |
| Medium | 250 | 48 | 0.05 | 15 |
| Tower | 1,000 | 56 | 0.05 | 15 |
| Rack | 5,000 | 64 | 0.05 | 15 |
| Big Rack | 50,000 | 64 | 0.05 | 15 |

All servers degrade all the time, even when idle, and are affected by temperature and air quality.

**Wear.** Simple items lose Quality in a straight line over their durability, counted from the last repair. Servers use the advanced model. On each update, wear = Δmonths / TimeToAtrophy × (1 − room award bonus) × temperature factor × (1 + bad air) × load factor × Random(0.25, 2).

- Temperature factor: 1 up to 15°C. Above that it is 1 + (T − 12) / 20. Above 28°C you also get a heat warning.
- Load factor: max(1, load + 0.25), so a server running above 75% load wears faster.
- Cold rooms do not slow wear below the normal rate. They only avoid the heat penalty.

**Fire.** A server can only start a fire at the moment it breaks, never while it still works. The roll is seeded by the furniture's ID, so the same server always gives the same result: it ignites if roll < FireStarter × heat multiplier. The heat multiplier is 1 at or below 30°C and rises to 2 at 50°C. A server has a 5% chance at normal temperatures and a 10% chance at 50°C or hotter. Cold does not reduce it below 5%.

- Fires need the Fires difficulty setting on, the Security mission done, a player-owned room, and an item not placed from inventory.
- A burning room destroys a random flammable item about every 5 in-game minutes. Fire spreads to the room above or below, or else to a room sharing a wall. It does not jump from server to server inside a room as a separate mechanic; the whole room burns.
- Sprinklers lower the fire faster the more of them there are per room area.

**Repair speed.** Repair time = (1 − Quality) × MinutesToRepair. A dead server takes 15 in-game minutes in person. Remote repair from an IT desk runs at `ITSupportProcess.Effectiveness × 0.5` of that speed.

### How staff choose repairs

The behaviour scripts in `resources.assets` (the IT and Janitor node lists) plus `AI.FindITRepair`, `AI.FindRepair`, `AI.LookForITDesk`, `AI.RemoteRepair` and `AI.TryFixFurn` define this. **No item type is prioritised:** servers, PCs and printers compete only on condition and room.

**IT staff**

1. **Desk first.** They head to an IT desk if one exists and the IT Support process is running with effectiveness above 0. The exception is when every item on the broken-IT list is fully broken (Quality 0): then they skip the desk and walk.
2. **Remote repair.** `GameSettings.BrokenIT` holds IT items in the order they fell below 80%. A desk worker repairs the last non-broken item in that list (newest first), for free. There is no reservation, so every desk worker repairs the same item at once.
3. **Broken items pull them out.** If a desk worker finds a reachable item at Quality 0 in the list, they leave the desk and fix it in person.
4. **Walking repairs** (no desk, or after step 3): any IT item at or below 80% Quality, or broken, and not on fire.

**Target order for walking repairs** (IT and janitors use the same logic):

- Assigned rooms: in assignment order. They take the first room that has any work.
- No assigned rooms, staff indoors: rooms connected to where they stand, in pathfinding order (likely nearest first, not fully confirmed).
- No assigned rooms, staff outdoors: all rooms, worst item quality first.
- Inside a room: fewest failed path attempts first, then lowest Quality first.

**Rules that apply to IT repairs in person**

- Each repair costs (1 − Quality) × $500, so a dead server costs $500. If the company can't afford it, the item is skipped.
- They only kick someone off a working PC at or below 15% Quality, or 50% for on-call staff. A broken PC is always taken.
- Repair speed is 1.0, the full rate.

**Janitors** fix only non-IT items, starting at 75% Quality or when broken. They use the same room and item ordering and have no desk or remote mode. They skip a room while its toilet is in use. No money cost appears in their repair code.

**Practical takeaway for big server rooms.** Assign IT staff to the server room so office PCs elsewhere don't compete. Remote repair handles one item at a time, newest first, so with dozens of servers the ones that degraded earliest can wait a long time, and every broken server pulls desk staff away to fix it in person.

## Electricity generation and the ISP

The ISP is the main ongoing server cost. Self-generated power is worth full price when you use it yourself, but only 25% of the price when sold to the grid.

**Map presets** (`EnvironmentPreset` assets in `resources.assets`, referenced by `ObjectDatabase.EnvironmentPresets`):

| Map | UtilitiesCostFactor | Electricity per kWh | ISPCostFactor | ISP per Mbps per month |
| --- | --- | --- | --- | --- |
| City | 1.00 | $0.60 | 0.95 | $17.10 |
| Town | 1.10 | $0.66 | 0.975 | $17.55 |
| Rural | 1.05 | $0.63 | 1.00 | $18.00 |

Electricity price = `0.6 × UtilitiesCostFactor` (`Furniture.GetElectricityPrice`). ISP price = `18 × ISPCostFactor` (`Server.GetISPCost`).

### Grid-only baseline (no generation)

Electricity costs 0.6 × the map's UtilitiesCostFactor per kWh. One watt drawn nonstop for a month uses 0.72 kWh, whatever the days-per-month setting.

| Map | Price per kWh | Per watt, nonstop, per month | Per 1 kW, nonstop, per month |
| --- | --- | --- | --- |
| City | $0.60 | $0.432 | $432 |
| Town | $0.66 | $0.475 | $475 |
| Rural | $0.63 | $0.454 | $454 |

The whole draw is billed monthly as "Electricity". Servers draw power in proportion to their load, so an idle server adds nothing.

**Player generation options at a glance:**

- Solar Panel: $30k, up to 3,000 W. Output depends on weather and daylight.
- Wind Turbine: $1M, up to 90,000 W. Output follows a daily wind roll around the climate average.
- Battery: $20k, stores 15,000 kWh with no losses.

Power you use yourself is worth the full price. Surplus sold to the grid earns 25% of the price.

### How electricity is billed

- Each powered item draws `Wattage × UseModifier` (`Furniture.RefreshUsage`). Positive wattage adds to `ElectricityDelta`; negative wattage (generators) adds to `ElectricityGenerationDelta`.
- `GameSettings.UpdateUtilities` runs every frame with elapsed in-game minutes. Energy = `0.03 × W × (minutes / 60) / DaysPerMonth`, so a month of nonstop use is `0.72 × W` kWh (W / 1000 × 24 h × 30 days) whatever the days-per-month setting. The build panel uses the same figure.
- Generation offsets use first. Any shortfall is drawn from batteries, then the grid. Any surplus charges batteries, then goes to the grid.
- `TimeOfDay.UpdateMonth` bills grid kWh × price as "Electricity" and pays surplus kWh × price × **0.25** as "ElectricityToGrid".
- Printers add owed power per print (`ProductPrinter.AddOwedWatt`). Walking along one path-node type adds 10 kWh per unit of distance to `ElectricityBurst` (likely elevator rides, not fully confirmed).

### Servers

- A server's `UseModifier` = load ÷ group capacity, so power draw scales with load and **an idle server uses no electricity**. The same load also drives wear.
- Each hour, a server group's electricity cost is multiplied by (1 − share of last hour's use covered by your own generation or batteries) before it is booked to products.

| Server | Wattage | Mbps | W per Mbps | Electricity per Mbps per month at full load (City) |
| --- | --- | --- | --- | --- |
| Small | 800 | 50 | 16 | $6.91 |
| Medium | 2,000 | 250 | 8 | $3.46 |
| Tower | 4,000 | 1,000 | 4 | $1.73 |
| Rack | 20,000 | 5,000 | 4 | $1.73 |
| Big Rack | 200,000 | 50,000 | 4 | $1.73 |

For comparison, the ISP costs $17.10 per Mbps per month in the City.

### Generators and batteries

All three are furniture with `FunctionCategory` Electricity and unlock after the "Servers" mission (Unlockable = Servers).

| Item | Cost | Wattage | Durability (real) | Shown in build menu | ExpectedOn | FireStarter |
| --- | --- | --- | --- | --- | --- | --- |
| Solar Panel | $30,000 | −3,000 | 12 months | about 15 months | 19 h | 0.05 |
| Wind Turbine | $1,000,000 | −90,000 | 24 months | 48 months | 12 h | 0 |
| Battery | $20,000 | 0 | 12 months | 12 months | 24 h | 0.15 |

The build menu shows `TimeToAtrophy × 24 / ExpectedOn`, but all three have DegradeAlways on, so they actually wear out on calendar time. The shown figure overstates lifespan for solar and wind. Janitors repair them (they are not IT items), and staff can be assigned to the outdoor area.

**Solar.** `UseModifier = SunEffectiveness`, refreshed every hour (`TimeOfDay.UpdateSunEffectiveness`):

- SunEffectiveness = (1 − snow) × daylight × clouds.
- Daylight: the light level mapped from 0.3–1.0 to 0–1.
- Clouds: Cloudiness mapped from 0.1–0.8 to 1.0–0.1. Cloudiness is re-rolled daily as |Gauss(0, 0.4)| × 0.8.
- Snow: clamp01(−seasonal minimum temperature / 2). At −2°C or colder, panels produce nothing.
- Best case is about 2,160 kWh a month per panel. Real output is lower because of night and clouds; roughly a third of that is an estimate.

**Wind.** `UseModifier` = the daily wind roll: Gauss(AverageWind, 0.2), reflected into 0–1 (`TimeOfDay.RandomizeCloudShadows`, applied hourly).

| Climate | AverageWind | Seasonal minimum temperature | Solar notes |
| --- | --- | --- | --- |
| Tundra | 0.75 | −10 to about 0°C | Almost no solar except mid-summer |
| Forest | 0.50 | about −2 to 15°C | Reduced solar in winter |
| Desert | 0.25 | 20 to 38°C | Full solar all year |

A Forest turbine averages about 45 kW, or about 32,400 kWh a month: roughly $19,400 a month in the City if all of it is used, or about $4,900 if all of it is sold. Payback is about 4.3 years when used, against about 17 years when sold.

**Batteries.** `Battery.MaxCapacity` = 500. Stored charge = energy / 30, so one battery holds 15,000 kWh. There is no loss going in or out. One battery charges until full before the next one starts, and discharging works the same way (`GameSettings.GetBattery`). Batteries exist to shift surplus to later use at full price instead of selling it at 25% (the in-game tip: "1 watt saved is worth more than 1 watt sold").

### ISP

- Each hour, for each server group: ISP cost = `min(ISP-using load, group capacity) × 18 × ISPCostFactor / 24 / DaysPerMonth`, added to `ServerCost` and paid monthly as "Internet". You pay for bandwidth actually used, not capacity, and overload does not raise the bill.
- `UsesISP` is true for released products (`SoftwareProduct`), hosting deals (`ServerDeal`), `DistributionPlatform`, `PlayerDistribution` and `NetworkServerItem`.
- `UsesISP` is false for internal IT tasks (`ITServerTask`: email, backups and similar), in-development source control (`SoftwareAlpha`) and `SoftwareUpdate`. These still use server electricity.
- In multiplayer, a network server's price is `GetISPCost × (1 + markup)` (`NetworkServer.GetCost`).

## Deals

All deal offers from AI companies go through reception. With no receptionist on staff, you get no hosting, work, print or IP offers.

### How offers arrive

- If the Deals difficulty setting is off (`Difficulty.Deals < 0.5`), `DealWindow.AddDeal` returns without creating anything.
- `DealWindow.FindReceptionTime` picks a random receptionist (staff `AItype` 4) and schedules a business visitor for that receptionist's `StaffOn` hour plus 0–2 hours and a random minute, today or tomorrow. No receptionist means no visit and no offer.
- The visitor waits at the desk. `AI.GuestWaitAtDesk` inserts the offer into the Deals window after a short wait. If the visitor runs out of patience first, they leave and the offer is lost.
- Daily settlement for active deals (`TimeOfDay.UpdateDay`):
  - The client pays `Payout()` to you.
  - `ReputationEffect / DaysPerMonth` is added to your business reputation and to the deal's `Performance`, which starts at 1.
  - If Performance falls to 0 or below, the deal is cancelled (`DealPerformanceCancel`).
- Most pay ranges use `Deal.GetPlayerWorth(min, max)` = `RandomBucketed(BusinessStars, 6, min, max)`. More business stars push offers toward the top of the range.

### Hosting deals (ServerDeal)

See "AI companies and servers" above. One offer is made per new non-subsidiary AI product with ServerReq > 0. You're paid hourly on the load you serve, at 1.05–1.25× your cost per Mbps. The offer stays valid while the product is 24 months old or less.

### Work deals (WorkDeal)

Work types: 0 Design document, 1 Alpha, 2 Support, 3 Marketing. `worth` is per month; `Worth()` = worth / DaysPerMonth is paid daily.

| Type | When offered | Length | Pay per month | Reputation |
| --- | --- | --- | --- | --- |
| Support | 50% for each new AI product (`MarketSimulation.SimulateMonth`) | DevTime × 2 months | DevTime × $500 × GetPlayerWorth(0.6, 1.0) | Daily, after the first 16 h: handling over 10% of tickets gives +0.005 to +0.03; less slides toward −0.1 |
| Marketing | The other 50% | DevTime / 4 months | RealQuality × marketing effort × GetPlayerWorth(PostMarketingPrice + 20k, 2 × PostMarketingPrice) × 0.05 | Daily: up to +0.03 when progress keeps pace with the budget; if spending lags, −0.1 × √(shortfall) |
| Design document | When a non-subsidiary AI starts a prototype, 50% of the time (`SimulatedCompany.Simulate`) | Dev months − 2 | DevTime × $1,000 × GetPlayerWorth(0.6, 1.0) | When the deal ends: from −0.5 to +0.25, by progress |
| Alpha | The other 50%, or always for types with no code/art split | Dev months − 2 | Same as Design document | When the deal ends: (2 × quality − 1) × 2 × DevTime / 48 |

More rules:

- Accepting a Support deal adds 0.7 (±0.1) × DevTime × 20 bugs to the product. The deal ends if the product has no users more than 4 months after release.
- Prototype deals end if the prototype is released.
- Cancelling a prototype deal early hands your progress to the AI, blended by how far along its schedule it is (`WorkDeal.Cancel`, `LerpQ`).
- Work deals use the "Deal" business reputation category.

### Print deals (PrintDeal)

- Offered from `SimulatedCompany.UpdateReleases` when an AI product is 4–11 months from release and has no print deal yet.
- Copy goal (`SimulatedCompany.OfferPrintDeal`) = reach × physical share × U(0.1, 0.5) × quality. It's capped by AI cash × 0.2 / per-copy price, and by months to release × 600,000. No offer if the goal is 10,000 or fewer.
- Pay per copy = PhysicalCopyPrice × GetPlayerWorth(0.15, 0.20). Hardware uses the hardware price × HardwareCopyPriceFactor × (1 + that roll). You're paid per copy delivered. There is no daily payout.
- The deadline is the release date. `FinalizeDeal` runs 1 month after it:
  - Missing copies, after an allowance for the time remaining, cost you a penalty of 10% of the copy price each, paid to the client. Missing more than 1,000 copies also costs −0.25 "Printing" reputation.
  - Meeting the goal gives +goal / 100M reputation.

### IP deals (IPDeal)

- **AI bids for your IP** (`DealWindow.GenerateBid`, checked monthly):
  - Bidder: the non-subsidiary AI with the highest Money × U(0.8, 1.0).
  - Target: your best non-designer-owned product with last-month cash flow over $100k and market-weighted quality above 0.25.
  - The bid must be over $5k and below 25% of the bidder's cash.
  - Chance: Random × 2 < business reputation (always 50% if business reputation is ignored).
  - Valid for 1 month. On sale, the gain over `BoughtFor` is taxed.
- **Valuation** (`IPDeal.GetProductValue`): (1 − royalty shares) × (sales sum + max(floor, PerceivedValue × Popularity × repQ × 1M)).
  - repQ = reputation × discrete reputation × quality² when valuing a player's product, otherwise 1.
  - The floor is $10k for AI products and 0 for player products.
- **AI-to-AI**: when an AI is short on cash, it sells frameworks, then add-ons, then its weakest IP to other AIs (`SimulatedCompany.TryToSellIP`, `MarketSimulation.FindBuyer`). These sales don't appear in your Deals window.

### Publisher deals (PublisherDeal)

Only publishers with `WillPublishPlayer` that are `DoingWell` make offers.

| Service | Royalty it adds (`DealRoyalty`) |
| --- | --- |
| Funding | +0.10 |
| Marketing | +0.40 |
| Printing | +0.40 |
| OS exclusivity | ×0.8 (−0.20, a multiplier) |

- Raw royalty = sum of the positive values × product of (1 + each negative value).
- Each publisher's royalty is then scaled by its own seeded personality, your reputation in that category, and your relationship with it.
- Funding ≈ 1.25 × `EstimateProjectCost` × stars factor (0.5–2) × (1 + reputation / 4) × the publisher's funding tendency (0.5–1.5), rounded to $1k. There's a ×1.5 bonus if Marketing is not included and another if Printing is not included. No offer if funding exceeds 10% of the publisher's cash.

### Distribution and exclusivity (your own store)

- AIs negotiate the cut they pay to sell on your store (`DistDealNegWindow`). Their maximum = `DistributionStandardCut` × lerp(0.25 − 0.25 × their market share, 1, your digital share³). Savvier AIs give up sooner (after 4–8 bids) and lose patience faster.
- Exclusivity deals: you pay AIs up front to keep chosen products exclusive to your store until a chosen date (`ExclusivityDealWindow`).

## Legal system: lawsuits, breaches and penalties

Everything here comes from the vanilla Assembly-CSharp.dll (Lawsuit, LegalWork, ContractWork, ContractResult, PublisherDeal, GameSettings.LaunchSuit). Money is in game dollars, and "rep" means business reputation.

### How reputation changes are scaled

Every penalty below goes through `Company.ChangeBusinessRep(x, category, cap)`:

- A change of 0, NaN or Infinity is ignored.
- x is clamped to ±cap. Every legal and contract call uses cap 1.0.
- Reputation moves by x / 6 and is clamped to 0..1.

Because BusinessStars = floor(rep × 6), **a change of x is worth x stars**. For example, losing a lawsuit at difficulty 1 costs one full star.

### Lawsuit lifecycle

1. **Queueing** (`GameSettings.LaunchSuit`):
   - A new suit merges into a queued one when neither has plaintiff persons, the Subject matches and External matches. The merged suit becomes a class action: Money and PersonCount are summed, and Difficulty takes the higher value.
   - Otherwise it is queued with Start = now + Random(0..1) days, or right away when launched as immediate.
2. **Prompt** (`UpdateLawsuitQueue`): when a suit is due, the game force-pauses and offers **Settle** or **Court action**. A suit with PersonCount ≥ 10 unlocks the EMPLOYEESUIT achievement.
3. **Settle at the prompt:** pays the full Money to the plaintiff, booked under the "Lawsuit" category. External suits can only be settled this way once campaign Mission13 is complete, otherwise the campaign lock error shows.
4. **Court action:** creates a Legal work item. Type 2 is an external suit and Type 1 is an employee suit.

### Court case (LegalWork)

`InitTimeline(Amount, difficulty)` sets up the case:

| Value | Formula |
| --- | --- |
| DevTime (work units) | ceil(max(1, Amount^0.4 / 15)) |
| Months to deadline | max(2, ceil(ProjectDevTimeGeneric(DevTime) × MapRange(difficulty 0..1 → 0.75..1))) |
| Settle window | ends at now + max(1, ceil(Months × MapRange(difficulty 0..1 → 0.75..0.25))) |
| Backlash | = difficulty |

Some reference points for DevTime:

| Amount | DevTime |
| --- | --- |
| $50k | 6 |
| $500k | 13 |
| $1M | 17 |
| $10M | 43 |

**Progress.** Lawyers use the Law specialisation (skill index 4). Each worker adds the following per tick:

PCAddonBonus × effectiveness × GetProjectEffectiveness(workers, DevTime) × MapRange(Law skill 0..1 → 0.5..1) × PerDay × leader factor

Progress increases by that amount / DevTime × 8. The optimal team is GetOptimalEmployees(DevTime) = round(DevTime/4 + 1).

**Settling in court.** While the settle window is open, you can settle for:

SettleAmount = Amount × MapRange(Progress 0..1 → 1..0.5)

So a case worked to 100% settles for half the claim. External suits still need Mission13.

**Win** (Progress ≥ 1): a "LawsuitWon" notification, and rep −0.5 × Backlash ("Lawsuit"). Winning still costs up to half a star. Winning a suit flagged Spiff unlocks the SPIFFBREAK achievement.

**Defeat:** `TimeOfDay.UpdateHour` checks every in-game hour and calls Defeat once now ≥ Deadline. On defeat:

- you pay the full Amount (to the plaintiff company, unless it is bankrupt)
- rep −Backlash ("Lawsuit")

There is no dice roll. A case is a race between legal progress and the deadline.

### What triggers a lawsuit

| Trigger | Plaintiff / subject | Amount | Difficulty | Notes |
| --- | --- | --- | --- | --- |
| Firing an unhappy employee (`Actor.Fire`) | Employee, "Negligence" | ComplaintLevel × $500,000 | MapRange(ComplaintLevel 1..2 → 0.25..1) | Only applies if the employee didn't leave amicably, was employed more than 12 months, has complaint reasons and ComplaintLevel > 1. Suits merge into class actions. |
| Employee dies in a fire (`Actor.FixedUpdate`) | "Negligence" | $1,000,000 | 1 | Only if there is an active fire inspection report that was not passed (reason FireInspectionLawsuit). |
| Tax audit finds fraud (`GameSettings.Audit`) | "TaxFraud" | max($1M, or $10M for the severe variant, offshore balance) | 1 | Launched immediately. Needs evidence found or an offshore balance > 0, and no tax case in the last 12 months. |
| Cancelling a publisher-funded project (`SoftwareWorkItem.Kill`) | Publisher, "PublishingLawsuit" | Funding × 1.5 | 1 | Reason CancelledProjectFund, flagged Spiff. Only when the deal included Funding. |
| Release slips more than 3 months (`SoftwareAlpha.PromoteAction`) | Publisher | $50,000 | 1 | Reason ScheduleNotMet |
| Funded release with quality < 0.15 | Publisher | Funding amount | 1 | Reason SubparProjectFund |
| Event or mod scripts (`DefaultScope.LaunchLawsuit`) | Any | Script-defined | Script-defined | A scripting hook that builds a Lawsuit and queues it. |

### Breach of a publishing deal

**Cancelling a published project** (`SoftwareWorkItem.Kill`, player-initiated):

- rep −clamp01(months since dev start / deal Months) ("Publisher")
- publisher relationship −0.05, or −0.25 when the deal had Funding (floored at 0)
- the deal is abandoned and licences are released
- with Funding, `PublisherDeal.Suit` also runs. It sets WillPublishPlayer = false, so that publisher stops publishing for you, and launches the Funding × 1.5 suit.

**Missing the release date:**

- relationship −0.05 per month of delay
- rep −delay / 4
- if the delay is over 3 months, the $50,000 suit as well

**Funded release with quality < 0.15:** relationship −(1 − quality) × 0.25, plus a suit for the Funding amount.

### Breach of contract (contract work)

**Offer values** (`ContractWork.GenerateWork`). Let W be the contract workload. W is based on the optimal team × the expected months, and art work counts at 70%. Each value below has about ±10% Gaussian noise.

| Field | Value |
| --- | --- |
| Initial (paid up front) | W × 2,500 × ContractIncomeFactor (difficulty) |
| Done (paid on completion) | W × 7,500 × ContractIncomeFactor |
| Penalty | W × 5,000. This is **not** scaled by difficulty. |
| Months (deadline) | floor(1.5 × expected months), with the deadline set from acceptance |
| Quality target | min(0.95, Gauss(target, 0.1)) |
| PerBug | Gauss(0.5, 0.2) × 100, about $50 per bug |

**Delivering** (`ContractResult`). Let f = min(1, progress / MinProg)³.

- Income = round(f × Done). If f < 0.01, Income = −Initial, which means the advance is refunded.
- BugPenalty = max(0, remaining bugs − 10) × PerBug. Remaining bugs are capped at DevTime × 100, and the first 10 are free.
- LatePenalty = Penalty, a flat amount, if the days taken exceed Months × DaysPerMonth.
- Net transaction = Income − BugPenalty − LatePenalty.

**Contract rep on delivery:**

rep × MapRange(Months 1..12 → 0.2..0.5), where rep = −lateDays / devDays − bugs / max(2000, DevTime × 100) + sign(d) × √d × Q + 0.01, with d = achieved − Q + 0.05.

QA note: in this build the quality term uses Mathf.Sqrt(d) on a signed d. When achieved quality is more than 0.05 under target, the result is NaN and ChangeBusinessRep discards it. So in that case the contract gives **no** rep change at all, lateness included. The contract window preview (`GetRep`) uses a linear term instead, so the preview and the actual result can differ.

**Cancelling a contract:**

- you pay Penalty and keep the Initial advance
- you receive no Done payment
- rep −MapRange(Months 1..12 → 0.05..0.25) ("Contract")

**Manufacturing contracts** (Hardware print orders):

- Income = (PerPrintFactor + HardwarePrice) × delivered units, capped at Goal
- shortfall penalty = 10% × HardwarePrice × missing units, charged immediately
- rep −(1 − delivered/Goal) × 0.1

### Other legal items

- **Patents:** a Type 3 LegalWork. DevTime equals the tech's research months. When Progress ≥ 1, `PatentNow` charges TechLevel.PatentPrice ($50,000) and transfers the patent. If someone else got there first, you get "PatentTooSlow" and the work is dropped.
- **Deal penalties** (see Deals): the print-deal shortfall costs 10% of the copy price per missing copy, plus −0.25 Printing rep if more than 1,000 short. Deals are cancelled when Performance reaches 0. Neither of these raises a lawsuit.

## Modding reference

Server load is set entirely by feature data. Server furniture also sets what hosting deals pay, so a modded server changes the whole hosting economy.

**Built-in features with server needs (Mbps per user, before the bandwidth factor):**

| Software type | Feature | Server |
| --- | --- | --- |
| Game | MMO | 0.002 |
| Office Software | Cloud backup | 0.001 |
| Antivirus | Datamining | 0.0005 |
| Game | Multiplayer | 0.0003 |
| Operating System | Microphone surveillance, Auto-update, App store, Cloud backup | 0.0002 each |
| Antivirus | Virus database | 0.0001 |
| Distribution platform | Cloud sync | 0.02 |
| Distribution platform | Social features, Voice chat, Remote streaming | 0.01 each |
| Distribution platform | Delta patching | RelativeServer 0.5 (multiplies the total) |

**Data fields:**

- Feature `Server`: Mbps per active user. Values from all selected features are added together.
- Feature `RelativeServer`: multiplies the summed `Server` value instead of adding to it.
- Furniture `Server { Power }`: capacity in Mbps. `Wattage` sets electricity use, and `Upgradable { TimeToAtrophy }` sets wear speed.
- Company types (`Types`, `PerYear`): if no company type covers a software type, the AI never releases it, so it never generates hosting offers.

**Hosting deal pricing:** deal rates depend on the cheapest server by watts per Mbps. Only furniture with `Type` = "Server" and Wattage / Power under 4 counts, grouped by `UnlockYear`. A very efficient modded server lowers the pay of every hosting deal from its unlock year on.

**Code hooks:**

- Level 3 feature scripts (`Script_EndOfDay`, using `Product.Userbase` and `Product.DevCompany.MakeTransaction`) can add custom recurring income or costs. The AI never selects level 3 features.
- `GameSettings.Instance.OnServersChanged` fires when servers are added or removed.
- Useful types: `ServerDeal`, `ServerGroup`, `SoftwareProduct.HandleLoad / GetRefundRate / DropHosting`, `Server.GetISPCost`, `Utilities.BandwidthFactor`.

**Where the wiki and the code disagree:**

- [Data Modding](https://softwareinc.coredumping.com/wiki/index.php/Data_Modding) gives the bandwidth factor as `1 + log10(year − 1969)`. The code uses `0.75 + log10(year − 1969)`, about 10% lower in 2010.
- [Furniture Modding](https://softwareinc.coredumping.com/wiki/index.php/Furniture_Modding) lists Power in "mb". In game it is used as Mbps.

## Sources

- The installed game's `Assembly-CSharp.dll` and `resources.assets` (built-in data, localization and changelog)
- [Data Modding](https://softwareinc.coredumping.com/wiki/index.php/Data_Modding), [Furniture Modding](https://softwareinc.coredumping.com/wiki/index.php/Furniture_Modding) and [Code Modding](https://softwareinc.coredumping.com/wiki/index.php/Code_Modding) on the official wiki
