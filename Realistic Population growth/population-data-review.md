# Realistic Population growth: data for review

All real-world numbers are in **millions**. The last column is what the mod would write into the game's `MarketSimulation.Population` (vanilla is a flat 145,613,576 for every year).

**How the mod value is picked:** the real-world size of the **largest single platform ecosystem** that year (Atari VCS → MS-DOS → Windows → Facebook/Meta). That is the cap you asked for, and it is always under world population. The PC / console / mobile columns show the real category sizes behind it. In game, the vanilla multipliers then apply unchanged: Computer = value × 0.95, Console = × 0.75, Phone = × 1.0.

**After 2025:** constant growth of 1.53 % per year (the rate that would reach 10.3B real in 2084). The curve stops at 2050, which is set to exactly **1.4B in game**, and stays flat at 1.4B for every later year.

**Scaling (v4):** the population holds at the vanilla **145.6M from 1980 to 1995**. After 1995: in-game value = 145.6M + (real value − 170M) × 0.2100, so each year adds the real number of new users at a fixed scale, reaching exactly **1.4B in 2050**, then flat. Max stays under the game's ~1.43B 32-bit overflow limit.

| Year | World pop | PCs in use | Console owners | Mobile subs | Smartphone users | Largest ecosystem | Real value | **In-game value** | Computer ×0.95 | Console ×0.75 | Phone ×1.0 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1980 | 4,447 | 5.0 | 4.0 | 0.0 |  | Atari VCS | 3.5 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1981 | 4,529 | 7.4 | 6.2 | 0.1 |  | Atari VCS | 6.5 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1982 | 4,612 | 10.9 | 9.5 | 0.1 |  | Atari VCS | 12.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1983 | 4,696 | 16.1 | 14.7 | 0.2 |  | Atari VCS | 14.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1984 | 4,782 | 23.8 | 22.7 | 0.3 |  | Atari VCS | 15.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1985 | 4,870 | 35.2 | 35.0 | 0.8 |  | Atari VCS | 16.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1986 | 4,958 | 43.8 | 42.3 | 1.5 |  | MS-DOS | 20.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1987 | 5,048 | 54.6 | 51.1 | 2.6 |  | MS-DOS | 28.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1988 | 5,139 | 68.0 | 61.7 | 4.4 |  | MS-DOS | 38.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1989 | 5,232 | 84.8 | 74.5 | 7.4 |  | MS-DOS | 50.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1990 | 5,327 | 106 | 90.0 | 11.3 |  | MS-DOS | 65.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1991 | 5,408 | 124 | 103 | 16.3 |  | MS-DOS/Windows | 80.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1992 | 5,490 | 146 | 119 | 23.5 |  | MS-DOS/Windows | 95.0 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1993 | 5,573 | 172 | 136 | 34.5 |  | MS-DOS/Windows | 115 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1994 | 5,658 | 202 | 157 | 56.1 |  | MS-DOS/Windows | 140 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1995 | 5,744 | 238 | 180 | 91.1 |  | Windows | 170 | **145,613,576** | 138,332,897 | 109,210,182 | 145,613,576 |
| 1996 | 5,823 | 280 | 199 | 146 |  | Windows | 210 | **154,014,938** | 146,314,191 | 115,511,204 | 154,014,938 |
| 1997 | 5,903 | 330 | 221 | 216 |  | Windows | 260 | **164,516,642** | 156,290,810 | 123,387,482 | 164,516,642 |
| 1998 | 5,984 | 390 | 245 | 319 |  | Windows | 320 | **177,118,685** | 168,262,751 | 132,839,014 | 177,118,685 |
| 1999 | 6,066 | 460 | 271 | 493 |  | Windows | 400 | **193,921,410** | 184,225,340 | 145,441,058 | 193,921,410 |
| 2000 | 6,149 | 543 | 300 | 738 |  | Windows | 480 | **210,724,135** | 200,187,928 | 158,043,101 | 210,724,135 |
| 2001 | 6,229 | 626 | 318 | 961 |  | Windows | 560 | **227,526,860** | 216,150,517 | 170,645,145 | 227,526,860 |
| 2002 | 6,310 | 722 | 337 | 1,157 |  | Windows | 640 | **244,329,585** | 232,113,106 | 183,247,189 | 244,329,585 |
| 2003 | 6,392 | 816 | 356 | 1,417 |  | Windows | 730 | **263,232,650** | 250,071,018 | 197,424,488 | 263,232,650 |
| 2004 | 6,476 | 923 | 378 | 1,763 |  | Windows | 830 | **284,236,056** | 270,024,253 | 213,177,042 | 284,236,056 |
| 2005 | 6,560 | 1,044 | 400 | 2,224 |  | Windows | 940 | **307,339,803** | 291,972,813 | 230,504,852 | 307,339,803 |
| 2006 | 6,643 | 1,099 | 418 | 2,770 |  | Windows | 1,000 | **319,941,847** | 303,944,755 | 239,956,385 | 319,941,847 |
| 2007 | 6,727 | 1,157 | 437 | 3,404 | 122 | Windows | 1,050 | **330,443,550** | 313,921,372 | 247,832,662 | 330,443,550 |
| 2008 | 6,812 | 1,218 | 457 | 4,067 | 180 | Windows | 1,100 | **340,945,253** | 323,897,990 | 255,708,940 | 340,945,253 |
| 2009 | 6,899 | 1,282 | 478 | 4,691 | 250 | Windows | 1,150 | **351,446,956** | 333,874,608 | 263,585,217 | 351,446,956 |
| 2010 | 6,986 | 1,350 | 500 | 5,351 | 350 | Windows | 1,200 | **361,948,659** | 343,851,226 | 271,461,494 | 361,948,659 |
| 2011 | 7,072 | 1,414 | 510 | 5,955 | 500 | Windows | 1,250 | **372,450,362** | 353,827,844 | 279,337,772 | 372,450,362 |
| 2012 | 7,159 | 1,480 | 519 | 6,336 | 700 | Windows | 1,300 | **382,952,065** | 363,804,462 | 287,214,049 | 382,952,065 |
| 2013 | 7,247 | 1,550 | 529 | 6,747 | 850 | Windows | 1,350 | **393,453,768** | 373,781,080 | 295,090,326 | 393,453,768 |
| 2014 | 7,337 | 1,623 | 540 | 7,094 | 1,030 | Windows | 1,400 | **403,955,471** | 383,757,697 | 302,966,603 | 403,955,471 |
| 2015 | 7,427 | 1,700 | 550 | 7,137 | 1,220 | Facebook | 1,591 | **444,071,977** | 421,868,378 | 333,053,983 | 444,071,977 |
| 2016 | 7,511 | 1,710 | 569 | 7,458 | 1,400 | Facebook | 1,860 | **500,571,140** | 475,542,583 | 375,428,355 | 500,571,140 |
| 2017 | 7,596 | 1,720 | 588 | 7,702 | 1,580 | Facebook | 2,129 | **557,070,302** | 529,216,787 | 417,802,726 | 557,070,302 |
| 2018 | 7,682 | 1,730 | 608 | 7,920 | 1,840 | Meta family | 2,600 | **655,996,345** | 623,196,528 | 491,997,259 | 655,996,345 |
| 2019 | 7,768 | 1,740 | 629 | 8,227 | 2,160 | Meta family | 2,890 | **716,906,223** | 681,060,912 | 537,679,667 | 716,906,223 |
| 2020 | 7,856 | 1,750 | 650 | 8,233 | 2,580 | Meta family | 3,300 | **803,020,188** | 762,869,179 | 602,265,141 | 803,020,188 |
| 2021 | 7,915 | 1,760 | 660 | 8,446 | 2,950 | Meta family | 3,590 | **863,930,065** | 820,733,562 | 647,947,549 | 863,930,065 |
| 2022 | 7,975 | 1,770 | 670 | 8,613 | 3,390 | Meta family | 3,740 | **895,435,175** | 850,663,416 | 671,576,381 | 895,435,175 |
| 2023 | 8,067 | 1,780 | 680 | 8,858 | 3,770 | Meta family | 3,980 | **945,843,349** | 898,551,182 | 709,382,512 | 945,843,349 |
| 2024 | 8,161 | 1,790 | 690 | 9,010 | 4,250 | Meta family | 4,100 | **971,047,437** | 922,495,065 | 728,285,578 | 971,047,437 |
| 2025 | 8,232 | 1,800 | 700 | 9,179 | 4,690 | Meta family | 4,200 | **992,050,843** | 942,448,301 | 744,038,132 | 992,050,843 |
| 2026 | 8,285 |  |  |  |  | growth 1.53%/yr | 4,264 | **1,005,556,033** | 955,278,231 | 754,167,025 | 1,005,556,033 |
| 2030 | 8,500 |  |  |  |  | growth 1.53%/yr | 4,532 | **1,061,719,141** | 1,008,633,184 | 796,289,356 | 1,061,719,141 |
| 2040 | 9,064 |  |  |  |  | growth 1.53%/yr | 5,276 | **1,218,026,489** | 1,157,125,165 | 913,519,867 | 1,218,026,489 |
| 2050 | 9,664 |  |  |  |  | growth 1.53%/yr | 6,142 | **1,400,000,000** | 1,330,000,000 | 1,050,000,000 | 1,400,000,000 |
| 2075 | 10,255 |  |  |  |  | flat after 2050 | 6,142 | **1,400,000,000** | 1,330,000,000 | 1,050,000,000 | 1,400,000,000 |
| 2080 | 10,290 |  |  |  |  | flat after 2050 | 6,142 | **1,400,000,000** | 1,330,000,000 | 1,050,000,000 | 1,400,000,000 |
| 2084 | 10,300 |  |  |  |  | flat after 2050 | 6,142 | **1,400,000,000** | 1,330,000,000 | 1,050,000,000 | 1,400,000,000 |
| 2085 | 10,292 |  |  |  |  | flat after 2050 | 6,142 | **1,400,000,000** | 1,330,000,000 | 1,050,000,000 | 1,400,000,000 |
| 2100 | 10,180 |  |  |  |  | flat after 2050 | 6,142 | **1,400,000,000** | 1,330,000,000 | 1,050,000,000 | 1,400,000,000 |

(1980 row shows mobile subs as 0.0 because there were only ~24,000 subscriptions.)

## Sources and confidence

| Column | Source | Confidence |
|---|---|---|
| World pop | UN World Population Prospects 2024, 5-year anchors, interpolated between them | High |
| PCs in use | eTForecasts "Computers-in-Use" 1980–2005 (5.0M, 35.2M, 105.6M, 237.6M, 459.5M, 543.2M, 721.6M, 1,044M), cited in [Jeremy Reimer's source notes](https://jeremyreimer.com/uploads/notes-on-sources.txt); 2010–2025 my estimate from Gartner/Microsoft statements (1B+ in 2008, ~1.4B active Windows devices) | High to 2005, medium after |
| Console owners | My estimate from cumulative hardware sales (Atari VCS, NES, PlayStation, etc.) less retired units, and Newzoo console-player counts for recent years | Medium/low |
| Mobile subs | World Bank / ITU per-100 series via [FRED ITCELSETSP2WLD](https://fred.stlouisfed.org/data/ITCELSETSP2WLD.txt) × world pop; 2000–2004 ITU absolute figures | High (subscriptions, not unique people) |
| Smartphone users | Statista series via [DemandSage](https://www.demandsage.com/smartphone-users/) 2014–2025; 2007–2013 my estimate | Medium |
| Largest ecosystem | 1980–85 Atari VCS installed base (estimate); 1986–2014 MS-DOS/Windows share of PCs in use (estimate); 2015–17 Facebook MAU (Meta reports); 2018–23 Meta "Family monthly active people" (Meta reports, 2018 estimated); 2024–25 estimated, since Meta switched to daily figures ([Q3 2025: 3.54B daily](https://investor.atmeta.com/investor-news/press-release-details/2025/Meta-Reports-Third-Quarter-2025-Results/)) | Medium; 2024–25 is the weakest |

## How the mod would work (minimal impact on other mods)

- It does **not** use Harmony and patches no game method. `MarketSimulation.Population` is a public static field, so the mod just sets that one number from the in-game date (smoothly, month by month), which also makes a new game start at the right value for the chosen start year.
- It touches nothing in software types, OS multipliers, servers, distribution or `FanPopulation` / `FollowerPopulation`. Any mod that reads `Population` simply sees the new value.
- When disabled, it restores the vanilla 145,613,576.
