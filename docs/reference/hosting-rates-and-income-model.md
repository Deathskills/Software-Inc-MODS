# Hosting rates and income model

> Exported from two interactive project artifacts made for the Server Hosting Business mod (12 Nov 2026):
> - "Server Hosting Rates" (draft numbers for review): [artifacts/server-hosting-rates-draft.html](artifacts/server-hosting-rates-draft.html). A later version that says "power at cost" lives next to the mod as `Server Hosting Business/hosting-rates.html`.
> - "Hosting Income Comparison" (vanilla vs mod, 1985–2020): [artifacts/hosting-income-comparison.html](artifacts/hosting-income-comparison.html)
>
> Both pages are self-contained HTML with JavaScript charts and tables; open them in a browser. This file keeps the model and the agreed rules in text. The live implementation is `Server Hosting Business/src/Rates.cs`, which wins if the two differ. After these pages were made, power pricing changed to a fixed $7 per Mbps per month (PR #3, see [hosting-power-options.md](hosting-power-options.md)).

## Profit per Mbps per month (on top of ISP + power)

| Product class | Mod profit per Mbps per month |
|---|---|
| Game, Multiplayer | $2.00 |
| Game, MMO | $6.00 |
| Enterprise: OS, Antivirus, Office | $8.55 |
| Vanilla, any product | $0.90–4.50 (5–25% of the $18 ISP cost, by stars; average about $2.70) |

Business stars scale profit from 80% (0 stars) to 120% (5 stars) around these midpoints.

## Baseline bandwidth, Mbps per 100k active users (5-year points)

| Year | Computer OS | Console OS | Phone OS | Antivirus (Network) | Office offline (Network) | Office cloud |
|---|---|---|---|---|---|---|
| 1985 | 0.01 | 0 | 0 | 0.01 | 0.01 | 0 |
| 1990 | 0.05 | 0 | 0 | 0.05 | 0.03 | 0 |
| 1995 | 0.5 | 0 | 0 | 0.5 | 0.3 | 0 |
| 2000 | 3 | 0.1 | 0 | 3 | 2 | 0.1 |
| 2005 | 12 | 1 | 0.1 | 12 | 8 | 0.5 |
| 2010 | 35 | 12 | 5 | 25 | 15 | 8 |
| 2015 | 105 | 55 | 35 | 45 | 25 | 45 |
| 2020 | 220 | 125 | 95 | 60 | 35 | 180 |
| 2025 | 355 | 191 | 142 | 75 | 45 | 560 |

Games:

- Vanilla bandwidth factor `F(y) = 0.75 + log10(y − 1969)`. Vanilla Multiplayer = 30 × F(y), vanilla MMO = 230 × F(y) per 100k users.
- Mod Multiplayer: straight line from vanilla's 1990 value (about 62) to 120 in 2000, then 350 in 2010, 500 in 2020, then +5% every 5 years.
- Mod MMO (whole game, Multiplayer + MMO): straight line from vanilla's 1990 value (about 477) to 900 in 2020, then +5% every 5 years.

## Agreed rules

1. **Zero stays zero.** When a 5-year point is 0, the rate stays 0 until the next point, then jumps to it (Console OS is 0 until 2000, then 0.1).
2. **The rate follows the current year.** A product released in 2003 and still used in 2015 uses the 2015 rate.
3. **After 2025, baselines keep growing in a straight line** at their 2020–2025 rate per year (for example Computer OS +27 per year, Office cloud +76 per year).
4. **Multiplayer 1990–2000** goes in a straight line from vanilla's 62 to 120.
5. **MMO** is the whole game. Straight line from vanilla's 477 in 1990 to 900 in 2020, then 5% every 5 years.
6. **Office.** Ticking Network adds the "Office offline" rate. Adding Cloud backup also adds the "Office cloud" rate, on top of vanilla Cloud backup.
7. **OS baseline** applies to every OS, whether or not Network is ticked (Network is forced on Computer and Phone OS, optional on Console OS).
8. **Business stars** still matter as in vanilla: 80% at 0 stars to 120% at 5 stars.

Vanilla add-ons (Auto-update, App store, Cloud backup: 20 × F(y) each for OS; Virus database 10 × F(y); Office Cloud backup 100 × F(y)) keep their vanilla values and are added on top.

## Offer-count model (Hosting Income Comparison, an estimate)

The game has no fixed number of AI releases. The page used these editable assumptions:

| Market segment | AI releases / year | Years on the market | Share with a vanilla server feature | Share that needs servers in the mod | From |
|---|---|---|---|---|---|
| Games, Multiplayer only | 3.4 | 2 | 1.0 | 1.0 | 1990 |
| Games, MMO | 0.9 | 3 | 1.0 | 1.0 | 1990 |
| Computer OS | 0.7 | 5 | 0.6 | 1.0 | 1985 |
| Console OS | 0.6 | 5 | 0.3 | 1.0 | 1985 |
| Phone OS | 1.2 | 4 | 0.6 | 1.0 | 1995 |
| Antivirus | 2.0 | 2 | 0.5 | 0.7 | 1985 |
| Office | 1.0 | 3 | 0.5 | 0.8 | 1985 |

Company counts come from the game's company types (Games 3–6 companies, Computer OS 2, Console OS 2–3, Phone OS 2–3, Antivirus 2, Office 2).

- **Vanilla offers:** one per AI release that has a vanilla server feature, at any size.
- **Mod offers:** those same offers, plus one for every unhosted AI product (subsidiaries of AI companies included) that needs more than 1 Mbps. Each can ask again every 6 months.
- **Potential profit** = monthly profit added if every offer from that year were accepted, with every product at the chosen userbase (100k to 100M users).
