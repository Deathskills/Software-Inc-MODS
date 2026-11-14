# Software Inc. — Player-Owned ISP Mod
## Complete Design & Technical Specification v0.7

**Project:** Software Inc. — Internet Service Provider Expansion  
**Implementation:** C# / Harmony  
**Target:** Single-player and multiplayer  
**Status:** Design approved; implementation and game-code verification pending  
**Version:** 0.7  
**Previous version:** 0.6

---

# 1. Project Overview

## 1.1 Objective

Create a Software Inc. mod that allows players to establish and operate an Internet Service Provider (ISP) as an additional business activity.

The mod should support:

- Independent ISP companies that do not develop software.
- Existing software companies expanding into ISP operations.
- Corporate ISP deals serving AI companies and products.
- Municipal ISP deals covering growing populations.
- Player-to-player internet service agreements in multiplayer.
- Internal internet service for a player's own company.
- Long-term technological and economic progression from 1980 through 2080 and beyond.

The ISP system must integrate with existing game mechanics rather than becoming an entirely separate telecommunications simulator.

## 1.2 Core Design Principles

1. Reuse existing servers, employees, Deals, hosting, maintenance, reputation, and financial systems.
2. Minimize new 3D models, textures, furniture, and employee types.
3. Prioritize integration with the vanilla Deals interface.
4. Allow players to choose their own level of infrastructure redundancy and operating risk.
5. Make ISP profitability depend on infrastructure efficiency, reliability, service demand, and reputation.
6. Make corporate service more profitable per Mbps than municipal service.
7. Keep municipal ISP operations independently profitable when managed efficiently.
8. Preserve meaningful multiplayer risk and opportunities for cooperation.
9. Avoid individual residential subscriber simulation.
10. Make all economic values configurable.
11. Preserve vanilla server hosting functionality.
12. Optimize for large late-game populations and multiplayer.
13. Use Harmony runtime patches without modifying core game assemblies directly.
14. Ensure save/load compatibility and safe multiplayer synchronization.

## 1.3 ISP Service Categories

The mod supports four service categories:

**Corporate Product ISP Deals**

Provide internet service to individual AI software products.

**Corporate Company ISP Deals**

Provide company-wide internet service covering an AI company's eligible current and future products and development activities.

**Municipal ISP Deals**

Provide aggregate internet coverage for a defined settlement population.

**Multiplayer ISP Agreements**

Allow a player-operated ISP to provide internet service directly to another player.

Only player-operated companies can become alternative ISP providers.

AI companies continue using vanilla ISP rates for their default network services.

The default vanilla ISP remains available whenever a player does not have an applicable player-owned ISP arrangement.

---

# 2. Implementation Architecture

## 2.1 Harmony Framework

Implement gameplay modifications through Harmony patches targeting the relevant classes and methods in `Assembly-CSharp.dll`.

Do not directly modify or replace the game's original assembly.

Implementation requirements:

- Use a dedicated Harmony identifier.
- Verify target method signatures before applying patches.
- Prefer narrowly scoped postfixes and prefixes.
- Use transpilers only when simpler patch types cannot achieve the required behavior.
- Preserve vanilla behavior outside the ISP system.
- Detect incompatible game versions or missing patch targets.
- Log patch failures clearly.
- Disable affected functionality safely when initialization fails.
- Avoid partially initialized ISP state that could corrupt saved games.

The implementing agent must inspect the actual game assemblies before selecting final patch points.

## 2.2 Mod Architecture

Separate the ISP system into logical components:

| Component | Responsibility |
|---|---|
| ISP Network Manager | Server assignments, pools, capacity |
| ISP Economy Manager | Pricing, transit, operating expenses |
| ISP Deal Manager | Corporate and municipal deals |
| ISP Demand Manager | Product, company, and development usage |
| ISP Population Manager | Municipal population and adoption |
| ISP Reliability Manager | Fulfillment, Performance, reputation |
| ISP Multiplayer Manager | Player agreements and reconnection |
| ISP Persistence Manager | Save/load and state validation |
| ISP UI Manager | Network, Services, Reliability interface |

These are conceptual components. They do not require separate assemblies or classes if a simpler implementation is more appropriate.

---

# 3. ISP Infrastructure

## 3.1 Reusing Vanilla Servers

The player must be able to link existing server furniture to an ISP network.

A server operates in one of two mutually exclusive roles:

- **Hosting Mode:** Provides its vanilla hosting capacity.
- **ISP Mode:** Provides ISP bandwidth capacity.

A server cannot provide both capacities simultaneously.

Unlinking an ISP server restores its ordinary hosting role.

### Preferred implementation

Extend existing server objects with an ISP assignment managed by the mod.

### Fallback implementation

Create dedicated ISP furniture variants using the same vanilla furniture models, dimensions, and textures.

Only modify the necessary names, identifiers, and gameplay parameters.

Do not create dedicated router or telecommunications furniture models unless absolutely necessary.

## 3.2 Equipment Capacity

| Equipment | Proposed ISP capacity |
|---|---:|
| Small Server | 50 Mbps |
| Medium Server | 250 Mbps |
| Tower Server | 15,000 Mbps |
| Server Rack | 50,000 Mbps |
| Big Server Rack | 250,000 Mbps |

Small and Medium servers retain bandwidth capacities comparable to their vanilla hosting capacities.

The larger generations provide substantially greater bandwidth capacity when assigned to ISP operation.

All values are configurable.

## 3.3 ISP Electrical Efficiency

Target equipment efficiency:

| Equipment | Target generation | W/Mbps | Rural electricity/Mbps/month |
|---|---:|---:|---:|
| Small Server | 1980 | 4.000 | $1.814 |
| Medium Server | 1990 | 2.000 | $0.907 |
| Tower Server | 1995 | 1.000 | $0.454 |
| Server Rack | 2000 | 0.250 | $0.113 |
| Big Server Rack | 2005 | 0.053 | $0.024 |

Electricity calculations assume:

- Rural electricity: $0.63/kWh.
- Economic month: 720 hours.
- Equipment operating at the relevant utilization.

### Additional requirements

The original preferred approach was reducing the equivalent vanilla server electricity consumption by 80%, making ISP equipment consume approximately 20% of normal hosting power.

However, the proposed capacity values and the desired watts-per-Mbps efficiency progression are not fully compatible with a universal 20% wattage reduction.

**Implementation priority:**

1. Preserve vanilla hosting behavior.
2. Maintain the intended ISP capacity progression.
3. Achieve progressively better electricity efficiency.
4. Remain as close as practical to the 20%-of-vanilla electricity objective.

Reconcile the final numbers after inspecting vanilla furniture data.

The mod must not impose an additional artificial electricity charge when actual furniture power consumption already accounts for that cost.

## 3.4 ISP Server Pooling

ISP-enabled servers must support linked network pools similar to vanilla server groups.

Players should be able to:

- Create an ISP pool.
- Link multiple ISP servers.
- Unlink individual servers.
- Combine eligible ISP equipment into shared bandwidth capacity.
- Manage multiple ISP pools if supported.
- Observe available capacity and current demand.

Example:

| Linked equipment | ISP capacity |
|---|---:|
| Tower | 15 Gbps |
| Tower | 15 Gbps |
| Server Rack | 50 Gbps |
| **Total ISP pool** | **80 Gbps** |

Operational ISP capacity equals the sum of the available capacity of linked, functional equipment.

`OperationalISPCapacity = Sum(AvailableLinkedISPServerCapacity)`

A server linked to an ISP pool must not simultaneously contribute its capacity to a vanilla hosting pool.

## 3.5 Maintenance and Failure

Reuse vanilla server mechanics:

- Equipment condition deterioration.
- IT repair behavior.
- Power consumption.
- Breakdowns.
- Existing equipment-related fire mechanics.
- Room temperature and other existing environmental effects where applicable.

Servers above 0% condition should continue delivering their normal capacity unless vanilla behavior requires otherwise.

At 0% condition, the equipment becomes unavailable according to the game's failure rules.

Do not introduce arbitrary additional ISP-specific breakdown probabilities.

Do not give ISP servers automatic repair priority over hosting servers.

Players are responsible for:

- IT employee scheduling.
- Room and area assignments.
- Preventative maintenance.
- Spare capacity.
- Equipment replacement.
- Infrastructure expansion.

## 3.6 Equipment Removal and Movement

Selling, deleting, moving, unlinking, or losing ISP equipment should behave consistently with equivalent vanilla hosting-server operations.

If the equipment becomes unavailable:

- Remove its capacity from the relevant ISP pool.
- Recalculate remaining available capacity.
- Preserve other operational ISP equipment.
- Allow the normal reliability system to handle resulting shortages.
- Remove stale furniture references from ISP state.

Do not prohibit equipment removal simply because the player has active ISP deals.

---

# 4. Wholesale Transit and Electricity

## 4.1 Wholesale Transit

Wholesale transit represents connectivity purchased from larger external internet networks.

It is a recurring operating expense, not a separate piece of furniture.

## 4.2 Transit Cost Curve

| Year | Transit cost/Mbps/month |
|---|---:|
| 1980 | $0.50 |
| 1990 | $0.42 |
| 2000 | $0.34 |
| 2010 | $0.26 |
| 2020 | $0.18 |
| 2030 | $0.10 |
| 2050 | $0.05 |

Use linear interpolation between these dates.

After 2050, hold the rate at $0.05 unless configured otherwise.

All values are configurable.

## 4.3 Transit Billing

Transit costs must depend on actual bandwidth delivered.

Unused capacity should not create additional wholesale transit charges.

The full monthly expense at constant utilization is:

`MonthlyTransitExpense = DeliveredMbps × MonthlyTransitRate`

The actual settlement calculation must respect the game's simulation time settings.

## 4.4 DaysPerMonth Scaling

The mod must maintain consistent economics across different `DaysPerMonth` settings.

The game's supported settings are expected to range from 1 through 8 playable days per month, but the actual valid range must be verified.

Distinguish:

**Simulated hours:**

`SimulatedHoursPerMonth = GameSettings.DaysPerMonth × 24`

**Economic hours:**

`EconomicHoursPerMonth = 720`

Under a constant-load model, each simulated hourly update should account for the appropriate portion of a full economic month.

Proposed transit settlement:

`TransitPerSimulatedHour = DeliveredMbps × MonthlyTransitRate / SimulatedHoursPerMonth`

Example:

A constant 1,000 Mbps connection at $0.50/Mbps/month must produce $500 of transit expense per economic month regardless of the number of playable days.

| Days/month | Simulated hours | Approx. transit/simulated hour | Total/month |
|---|---:|---:|---:|
| 1 | 24 | $20.83 | $500 |
| 2 | 48 | $10.42 | $500 |
| 4 | 96 | $5.21 | $500 |
| 8 | 192 | $2.60 | $500 |

### Mandatory verification

Before implementation, inspect:

- `GameSettings.DaysPerMonth`.
- Vanilla electricity accounting.
- Internet billing calculations.
- Hosting deal payment intervals.
- Hourly and daily update behavior.
- Multiplayer simulation synchronization.

If vanilla uses an alternative internal time normalization, adapt the ISP calculations to match it.

The desired result is monthly economic parity across different simulation settings, not necessarily the literal use of these provisional formulas.

Historical monthly traffic calculations must continue representing a consistent 30-day economic month.

---

# 5. ISP Pricing and Profitability

## 5.1 Default Vanilla ISP Prices

| Map | Electricity/kWh | Default ISP/Mbps/month |
|---|---:|---:|
| City | $0.60 | $17.10 |
| Town | $0.66 | $17.55 |
| Rural | $0.63 | $18.00 |

Allow these default rates to be updated through configuration.

The vanilla provider remains the standard fallback provider unless an active player-to-player agreement specifies otherwise.

## 5.2 Corporate ISP Pricing

Initial reference price:

`CorporateReferencePrice = $11.45/Mbps/month`

Calculate the offered price using the appropriate vanilla business-star multiplier.

`CorporateDealRate = CorporateReferencePrice × StarMultiplier`

## 5.3 Municipal ISP Pricing

Initial reference price:

`MunicipalReferencePrice = $5.06/Mbps/month`

`MunicipalDealRate = MunicipalReferencePrice × StarMultiplier`

Municipal service should generally produce less contribution per Mbps than corporate service.

Municipal revenue can nevertheless become substantial through population coverage and aggregate traffic volume.

## 5.4 Vanilla Business-Star Multipliers

| Stars | Multiplier range | Average margin above reference |
|---|---:|---:|
| 0 | 1.050–1.079 | 6.4% |
| 1 | 1.079–1.107 | 9.3% |
| 2 | 1.107–1.136 | 12.1% |
| 3 | 1.136–1.164 | 15.0% |
| 4 | 1.164–1.193 | 17.9% |
| 5 | 1.193–1.221 | 20.7% |
| 6 | 1.221–1.250 | 23.6% |

Prefer the game's actual multiplier selection logic and randomization.

The multiplier is applied to the reference service rate rather than directly to a player's electricity or transit expenses.

## 5.5 Profitability Targets

### Corporate

Target contribution after variable electricity and transit:

- Early-game lower bound: approximately $7.26/Mbps/month.
- High-end target using initial transit pricing: approximately $13.79/Mbps/month.
- Higher late-game contribution may occur because transit becomes cheaper.

The current $11.45 reference rate and 4 W/Mbps starting equipment actually produce approximately $9.71/Mbps/month at the minimum zero-star multiplier in Rural.

Therefore, the $7.26 value should be treated as a minimum balancing target rather than an exact outcome of the starting parameters.

### Municipal

- Minimum desired contribution: approximately $3/Mbps/month in Rural using the least-efficient starting equipment.
- Lower per-Mbps profitability than corporate service.
- Profitable aggregate operations through usage and municipal coverage payments.

These are variable contribution margins, not complete net profits.

Final profitability must account for:

- Equipment purchase costs.
- IT wages.
- Electricity.
- Wholesale transit.
- Maintenance.
- Equipment replacement.
- Unused capacity.
- Service failures.
- Other applicable operating expenses.

Do not guarantee profitability for inefficient or poorly maintained ISPs.

## 5.6 Internal ISP Usage

If a player supplies internet service to their own company:

- Do not create artificial external revenue.
- Replace eligible vanilla ISP costs with actual internal ISP operating expenses.
- Continue charging transit and electricity.
- Preserve normal hosting accounting.
- Prevent duplicate charges.

The benefit is reduced expense, not fictional revenue from selling bandwidth to oneself.

---

# 6. Corporate Product-Level ISP Deals

## 6.1 Overview

Product-level ISP deals serve individual AI software products.

They are available in the early game, before company-wide deals become available.

## 6.2 Demand Calculation

Prioritize the software product's current live users and actual network requirements.

Do not use total historical users as the primary demand metric.

If vanilla already provides an appropriate product network load calculation, reuse it.

Demand should update as the product's current usage changes.

## 6.3 Monthly Offer Generation

Each month:

1. Identify eligible ISP service requirements.
2. Exclude requirements already covered by an active ISP agreement.
3. Estimate the current value of eligible deals.
4. Rank offers primarily by current live demand and expected value.
5. Generate up to 10 new product-level ISP offers.
6. Preserve unaccepted offers for up to 24 months.
7. Remove offers that expire, become invalid, or are fulfilled by another player.

Use vanilla hosting-deal offer presentation wherever possible.

Respect:

- Receptionist requirements.
- Deal availability difficulty settings.
- Existing Deals interface behavior.
- Vanilla deal settlement and cancellation rules where applicable.

## 6.4 Hosting Compatibility

A player may hold both:

- A hosting deal for an AI product.
- An ISP deal supplying that same product.

Hosting and external internet service remain separate billable functions.

Prevent duplicate ISP charging.

---

# 7. Corporate Company-Wide ISP Deals

## 7.1 Availability

Company-wide deals become available from 1990 onward.

Minimum eligible bandwidth:

`MinimumCompanyISPBandwidth = 1,000 Mbps`

This threshold is configurable.

## 7.2 Umbrella Agreement Behavior

A company-wide ISP agreement includes all eligible current and future products of the contracted AI company.

It may include:

- Released product internet traffic.
- Eligible company-level network requirements.
- Active development-related bandwidth.
- Other existing network activity that can be represented safely.

New products should automatically enter the company's ISP coverage when they become eligible.

The customer company's bandwidth requirement updates as its operations evolve.

## 7.3 AI Development Bandwidth

Use vanilla-derived AI development calculations instead of simulating development traffic independently.

`DevTime` represents months.

Define:

`Optimal(n) = Round(n / 4 + 1)`

AI virtual design team:

`DesignTeam = Optimal(Round(DevTime / 3))`

AI virtual development team:

`DevTeam = Optimal(Round(DevTime × 2 / 3))`

Development load:

`DevelopmentLoad = (DesignTeam + DevTeam) × (DevTime / 24) × (0.75 + Log10(Year − 1969))`

These formulas are based on the supplied descriptions of:

- `SimulatedCompany.PayExpenses`
- `SoftwareAlpha.GetLoadRequirement`

### Implementation requirements

- Verify the actual game methods and calculations.
- Reproduce vanilla rounding semantics.
- Verify the units of the development-load result.
- Apply an appropriate conversion if the result is not already Mbps.
- Include development overhead only in eligible company-wide ISP demand.
- Avoid repeated frame-by-frame project scans.
- Cache the resulting company demand.
- Refresh when relevant projects change or at a suitable periodic interval.

### Important UsesISP restriction

Vanilla `SoftwareAlpha` and `SoftwareUpdate` reportedly use `UsesISP = false`.

Do not globally change those values.

Globally overriding them could unintentionally affect ordinary vanilla internet billing, source control, development servers, and unrelated AI behavior.

Instead, calculate development overhead separately inside the ISP company-demand aggregator.

The ISP deal then bills for that calculated service without altering the vanilla meaning of `UsesISP`.

## 7.4 Company-Wide Deal Offers

- Maximum 4 new company-wide ISP offers per month.
- Each offer remains valid for up to 24 months.
- Rank based on current aggregated live internet requirements and current financial value.
- Do not rank simply by lifetime product users.
- Reuse hosting-deal-style presentation.

The company-wide monthly limit is separate from the 10 product-level offers.

## 7.5 Product Deal Consolidation

When a company-wide agreement replaces relevant product-level ISP deals:

- Consolidate applicable product traffic.
- Prevent duplicate ISP billing.
- Preserve relevant provider rights and service commitments.
- Avoid cancelling unrelated hosting deals.
- Ensure the company-wide agreement covers future eligible products.

If existing product deals are held by different providers, the implementing agent must determine a safe consolidation mechanism rather than silently terminating unrelated players' agreements.

## 7.6 Mergers and Acquisitions

| Event | ISP behavior |
|---|---|
| New AI product released | Automatically include eligible demand in company-wide coverage |
| AI company acquired but remains operational | Preserve existing ISP agreement |
| AI company merged into another entity | Resolve or consolidate the ISP agreement without cancellation penalty |
| AI company dissolved | Terminate its ISP agreement without player cancellation penalty |
| Product ownership transferred | Recalculate eligible ISP coverage without duplicate billing |

Do not automatically terminate company-wide ISP agreements merely because an acquisition occurs.

Termination depends on whether the customer company continues to exist as an operational entity.

---

# 8. Municipal ISP Population

## 8.1 Independent Population System

Municipal ISP population must remain independent of vanilla game population.

This is necessary because other mods may change the default population cap substantially.

The ISP population system must not modify:

- Labor pools.
- Employee availability.
- Hiring candidates.
- Vanilla software market size.
- Vanilla population progression.
- Other unrelated economic systems.

The custom population applies only to municipal ISP coverage and demand.

## 8.2 Settlement Categories

Use three types:

- Rural.
- Town.
- City.

Each has a different starting population and year in which it reaches the maximum.

## 8.3 Exponential Population Formula

Use compound annual growth:

`Population(Year) = Min(PopulationCap, InitialPopulation × (1 + GrowthRate)^(Year − 1980))`

Where:

`GrowthRate = (PopulationCap / InitialPopulation)^(1 / (TargetYear − 1980)) − 1`

Default population cap:

`PopulationCap = 40,000,000`

Use full-precision growth rates.

## 8.4 Settlement Growth Targets

| Settlement | Initial population in 1980 | Target year | Approx. annual growth |
|---|---:|---:|---:|
| Rural | 2,500 | 2080 | 10.16% |
| Town | 10,000 | 2065 | 10.25% |
| City | 50,000 | 2050 | 10.02% |

Each settlement reaches 40 million inhabitants in its target year and remains capped afterward.

## 8.5 Population Reference Table

| Year | Rural | Town | City |
|---|---:|---:|---:|
| 1980 | 2,500 | 10,000 | 50,000 |
| 1981 | 2,754 | 11,025 | 55,010 |
| 1982 | 3,034 | 12,155 | 60,522 |
| 1983 | 3,342 | 13,401 | 66,587 |
| 1984 | 3,682 | 14,774 | 73,259 |
| 1985 | 4,056 | 16,289 | 80,600 |
| 1986 | 4,469 | 17,958 | 88,676 |
| 1987 | 4,923 | 19,799 | 97,562 |
| 1988 | 5,423 | 21,828 | 107,338 |
| 1989 | 5,975 | 24,065 | 118,093 |
| 1990 | 6,582 | 26,532 | 129,926 |
| 1991 | 7,251 | 29,252 | 142,945 |
| 1992 | 7,988 | 32,250 | 157,269 |
| 1993 | 8,800 | 35,555 | 173,028 |
| 1994 | 9,694 | 39,199 | 190,365 |
| 1995 | 10,680 | 43,217 | 209,441 |
| 1996 | 11,765 | 47,647 | 230,427 |
| 1997 | 12,961 | 52,531 | 253,516 |
| 1998 | 14,278 | 57,915 | 278,919 |
| 1999 | 15,730 | 63,851 | 306,868 |
| 2000 | 17,329 | 70,395 | 337,617 |
| 2010 | 45,622 | 186,774 | 877,307 |
| 2020 | 120,112 | 495,550 | 2,279,705 |
| 2030 | 316,228 | 1,314,797 | 5,923,873 |
| 2040 | 832,553 | 3,488,435 | 15,393,340 |
| 2050 | 2,191,916 | 9,255,552 | 40,000,000 |
| 2055 | 3,556,559 | 15,076,073 | 40,000,000 |
| 2060 | 5,770,800 | 24,556,932 | 40,000,000 |
| 2065 | 9,363,581 | 40,000,000 | 40,000,000 |
| 2070 | 15,193,156 | 40,000,000 | 40,000,000 |
| 2075 | 24,652,104 | 40,000,000 | 40,000,000 |
| 2080 | 40,000,000 | 40,000,000 | 40,000,000 |

The exact formula and target years are authoritative. Intermediate reference-table values are approximate and may differ from the formula due to rounding.

## 8.6 Population Caching

Calculate population dynamically from the current game year.

Cache results by settlement type and year.

Do not simulate individual residents.

Derived population caches may be rebuilt on loading rather than persisted if that is safer.

---

# 9. Municipal ISP Deals

## 9.1 Starting Municipal Deals

Initial population coverage:

| Settlement | Initial coverage |
|---|---:|
| Rural | 100 people |
| Town | 200 people |
| City | 300 people |

These are the first service agreements available to an ISP entering the municipal market.

They do not automatically provide coverage for the settlement's entire population.

## 9.2 Expansion Offers

The first municipal agreement enables a staged expansion sequence.

| Time since initial agreement | Potential cumulative coverage |
|---|---|
| 3 months | Up to 2× initial coverage |
| 6 months | Up to 4× initial coverage |
| 9 months | Up to 8× initial coverage |
| 12 months | Potentially original total settlement population |

These are maximum offer targets, not automatic guaranteed expansions.

Actual offers should consider:

- Available population.
- Existing coverage.
- Time since previous expansion.
- Business reputation.
- Historical reliability.
- Settlement growth.

After the first year, continue expansion opportunities generally every 6–12 months.

A player may accept more coverage than the available ISP equipment can reliably serve.

The resulting shortages and penalties are handled by the existing ISP reliability mechanics.

## 9.3 Population vs. Coverage

Maintain separate values for:

1. Total addressable settlement population.
2. Population contracted through ISP deals.
3. Population still available for service expansion.

Population growth does not automatically expand existing coverage.

New coverage requires new or expanded agreements.

## 9.4 City Deal Consolidation

Introduce a **Consolidate City Coverage** action.

It should:

1. Identify the player's eligible municipal deals.
2. Combine their contracted population.
3. Aggregate relevant bandwidth requirements.
4. Preserve weighted deal Performance and reliability history.
5. Retire the old agreements without ordinary cancellation penalties.
6. Generate a consolidated replacement agreement during the next monthly offer cycle.

Consolidation must not award additional coverage or reset poor reliability history.

It is an administrative replacement rather than a breach or voluntary early cancellation.

Ordinary early cancellation penalties remain applicable where appropriate.

---

# 10. Historical Municipal Internet Consumption

## 10.1 Average Monthly Usage

Use the supplied historical-style estimates for monthly data usage per adopting internet user.

### Early period

- 1980–1989: Progress from 1 MB to 5 MB/month.
- 1990–1993: Progress from 5 MB to 9 MB/month.

### 1994–2015

| Year | Monthly usage |
|---|---:|
| 1994 | 10 MB |
| 1995 | 20 MB |
| 1996 | 35 MB |
| 1997 | 60 MB |
| 1998 | 100 MB |
| 1999 | 180 MB |
| 2000 | 350 MB |
| 2001 | 650 MB |
| 2002 | 1.1 GB |
| 2003 | 1.8 GB |
| 2004 | 2.9 GB |
| 2005 | 4.5 GB |
| 2006 | 6.8 GB |
| 2007 | 10.2 GB |
| 2008 | 14.5 GB |
| 2009 | 20 GB |
| 2010 | 27.5 GB |
| 2011 | 36 GB |
| 2012 | 46.5 GB |
| 2013 | 58 GB |
| 2014 | 72 GB |
| 2015 | 92 GB |

These are gameplay balancing reference values based on the supplied consumption progression, not independently verified historical measurements for every country.

## 10.2 Extended 2016–2080 Consumption

Stretch the original supplied 2016–2025 consumption progression across 2016–2080.

The purpose is to slow late-game bandwidth growth and prevent excessive income scaling.

Representative control values:

| Year | Monthly usage |
|---|---:|
| 2016 | 125 GB |
| 2020 | ~150 GB |
| 2025 | ~187 GB |
| 2030 | ~233 GB |
| 2035 | ~285 GB |
| 2050 | ~553 GB |
| 2080 | 740 GB |

Interpolate between the appropriate original consumption control points, redistributed over the extended timeline.

The exact remapping should be defined consistently in configuration, with 2016 as the starting anchor and 2080 as the final anchor.

After 2080, hold consumption at 740 GB/month unless configured otherwise.

---

# 11. Internet Adoption

## 11.1 Adoption Milestones

Internet adoption must remain independent of total population and municipal service coverage.

The approved milestones are:

- 1980: 5%.
- 2020: 70%.
- 2080: 100%.

Use piecewise linear interpolation.

## 11.2 Reference Table

| Year | Adoption |
|---|---:|
| 1980 | 5% |
| 1990 | 21.25% |
| 2000 | 37.50% |
| 2010 | 53.75% |
| 2020 | 70% |
| 2030 | 75% |
| 2040 | 80% |
| 2050 | 85% |
| 2060 | 90% |
| 2070 | 95% |
| 2080 | 100% |

## 11.3 Adoption Formula

For 1980–2020:

`AdoptionRate = 0.05 + (Year − 1980) × 0.01625`

For 2020–2080:

`AdoptionRate = 0.70 + (Year − 2020) × 0.005`

Implementation:

```text
if Year <= 1980:
    AdoptionRate = 0.05

else if Year <= 2020:
    AdoptionRate = 0.05 + (Year - 1980) * 0.01625

else if Year <= 2080:
    AdoptionRate = 0.70 + (Year - 2020) * 0.005

else:
    AdoptionRate = 1.0
```

Adoption must never exceed 100%.

Use the same adoption curve for Rural, Town, and City initially.

Allow settlement-specific overrides through configuration if desired later.

## 11.4 Municipal Bandwidth Demand

Calculate:

`AverageMbps = CoveredPopulation × AdoptionRate × MonthlyMBPerUser × 8 / 2,592,000`

Where:

- `CoveredPopulation` is the population represented by active municipal ISP coverage.
- `AdoptionRate` is the current adoption fraction.
- `MonthlyMBPerUser` is average usage per adopting user.
- `2,592,000` is the number of seconds in a 30-day economic month.

Use decimal MB and Mbps.

The resulting value represents average continuous bandwidth.

It must remain consistent across different `DaysPerMonth` settings.

The system must not simulate each subscriber individually.

---

# 12. Municipal Revenue and Anti-Stacking

## 12.1 Two-Part Revenue

Municipal internet usage in 1980–1990 is too small to support a profitable ISP through Mbps-based revenue alone.

Municipal agreements therefore pay:

`MunicipalRevenue = EarnedCoveragePayment + DeliveredMbps × MunicipalDealRate`

The two components are:

**Coverage payment:** Compensation for maintaining internet access for a contracted population.

**Bandwidth payment:** Compensation based on actual internet traffic delivered.

## 12.2 Coverage Payment

The coverage payment may use:

- A configurable minimum payment.
- Payment per covered resident.
- Tiered or diminishing per-person payments.
- A combination of these approaches.

The exact amounts must be balanced against vanilla server costs and IT wages.

Coverage payments should support early-game operations without creating runaway revenue at very large populations.

## 12.3 Anti-Stacking

Coverage payments must be calculated using unique aggregate municipal coverage, not a separate full fixed payment for each deal.

For a given player company and settlement:

`BaseCoveragePayment = CoveragePaymentFunction(UniqueCoveredPopulation)`

This prevents players from multiplying minimum payments by accepting numerous small city deals.

The payment should be distributed appropriately across active city agreements for financial reporting.

## 12.4 Payment Based on Actual Service

A player must not receive full municipal coverage payments while providing little or no service.

Proposed formula:

`EarnedCoveragePayment = BaseCoveragePayment × FulfillmentRatio`

If fulfillment is 100%, the company receives the full payment.

If fulfillment is 50%, the company receives 50%.

If fulfillment is 0%, the company receives nothing.

Actual service performance is calculated through authoritative bandwidth-delivery data.

---

# 13. Reliability and Performance

## 13.1 Reliability Philosophy

Reliability is primarily determined by:

- Operational ISP server capacity.
- Equipment condition and breakdowns.
- Electricity availability.
- IT repair coverage.
- Bandwidth demand.
- Spare capacity selected by the player.

Do not add unnecessary ISP-specific random failures.

Players are free to accept more deals than they can serve.

## 13.2 Fulfillment Calculation

`FulfillmentRatio = TotalDeliveredBandwidth / TotalRequestedBandwidth`

Use demand-weighted measurements over the evaluation period.

If all customers receive their requested bandwidth, fulfillment is 100%.

If equipment breaks or demand exceeds operational capacity, fulfillment falls.

When demand is zero, avoid division by zero and define an explicit no-demand treatment consistent with deal settlement.

## 13.3 Deal Performance

Use the approved monthly-equivalent Performance adjustments:

| Fulfillment | Performance change |
|---|---:|
| 100% | +0.06 |
| 98% to below 100% | +0.03 |
| 96% to below 98% | 0.00 |
| 95% to below 96% | −0.02 |
| 90% to below 95% | −0.08 |
| Below 90% | −0.20 |

Performance begins at 1.0.

A +0.06 change raises the score from 1.00 to 1.06.

It does not directly increase revenue by 6%.

Performance determines whether a deal remains viable.

Follow vanilla cancellation behavior if Performance reaches zero or below.

Adapt monthly-equivalent changes to the game's actual deal settlement intervals.

## 13.4 Business Reputation

Reliable ISP service increases company reputation.

Poor service reduces it.

Reputation effects should consider:

- Service quality.
- Delivered bandwidth.
- Deal size.
- Diminishing returns for very large deals.

Proposed structure:

`ReputationChange = ServiceQualityFactor × VolumeWeight × BaseReputationChange`

Use a logarithmic or square-root volume factor.

The actual coefficients must be calibrated against the vanilla hosting-deal reputation system.

Business stars continue influencing future ISP deal prices.

## 13.5 Redundancy and Risk

Players choose whether to install spare capacity.

Example:

- Contracted demand: 20 Gbps.
- Installed capacity: 20 Gbps.
- Available redundancy: none.

If one server fails, service suffers.

Alternatively:

- Contracted demand: 20 Gbps.
- Installed capacity: 30 Gbps.
- Spare capacity: 10 Gbps.

The additional infrastructure provides protection against equipment failure.

The mod must not force redundancy or prohibit overcommitment.

---

# 14. Municipal Breaches and Lawsuits

## 14.1 Native Legal Integration

Severe municipal ISP failures should use Software Inc.'s existing lawsuit system where technically possible.

Do not immediately invent a separate legal simulation.

The implementation must first inspect the vanilla legal system and determine whether ISP contract breaches can be represented safely.

## 14.2 Approved Breach Thresholds

| Condition | Consequence |
|---|---|
| Normal under-delivery | Revenue and Performance effects |
| Below 90% fulfillment for 3 consecutive months | Eligible for municipal breach notice |
| Below 75% fulfillment for 5 consecutive months | Eligible for lawsuit |
| Zero operational ISP capacity for 1 full month with active municipal coverage | Guaranteed lawsuit initiation |

The 90% threshold generates a breach warning or eligibility for notice.

The 75% threshold creates lawsuit eligibility according to the available legal framework.

The zero-capacity threshold guarantees that a lawsuit is initiated after one complete month.

The guaranteed trigger does not guarantee that the city wins the case.

Legal damages and case outcomes should follow the native legal mechanics where possible.

## 14.3 Breach Tracking

Track consecutive underperforming months for applicable municipal obligations.

Requirements:

- Reset a threshold counter once service recovers sufficiently.
- Prevent duplicate lawsuits for the same continuing breach.
- Distinguish a new breach from an ongoing unresolved breach.
- Preserve relevant breach history across saves.
- Avoid lawsuit creation during multiplayer or save/load state reconstruction.

A lawsuit must not be initiated repeatedly simply because the same zero-capacity condition remains active.

## 14.4 Zero-Capacity Exploit Protection

When a municipality is under active agreement but its ISP has no operational capacity:

- Delivered traffic is zero.
- Usage-based revenue is zero.
- Earned coverage payment is zero.
- Fulfillment and Performance are affected normally.
- The one-month zero-capacity lawsuit counter advances.

This prevents a player from accepting city deals, removing all servers, and collecting guaranteed coverage income.

---

# 15. Multiplayer ISP Agreements

## 15.1 Player Provider Selection

A player may select another player's company as an ISP provider.

The provider offers service using its operational ISP capacity.

The customer pays the agreed service price rather than the vanilla default rate.

Provider demand and payments must be calculated through authoritative multiplayer state.

## 15.2 Standard Agreement

Under a standard agreement:

- The provider supplies bandwidth.
- The customer pays for delivered service.
- Missing ISP capacity produces real service shortages.
- There is no automatic fallback during ordinary provider under-delivery.
- The customer's network-dependent business operations may be affected.
- The provider experiences revenue and reliability consequences.

## 15.3 Manual Reconnection

A customer may leave a standard ISP agreement and reconnect to the vanilla ISP.

This requires a configurable reconnection fee.

The fee should discourage risk-free switching without trapping customers indefinitely.

The reconnection must occur at a consistent multiplayer simulation boundary.

## 15.4 Premium Agreement

Premium agreements charge a higher service price.

If the provider fails to meet the defined premium service requirement:

1. The agreement automatically terminates.
2. The customer reconnects to the default vanilla ISP.
3. The normal vanilla ISP rate applies afterward.
4. The provider experiences relevant reliability consequences.

This is a termination-and-reconnection guarantee, not continuous automatic backup bandwidth.

Do not claim uninterrupted reconnection unless the game's simulation supports it.

---

# 16. Multiplayer Disconnection and Bankruptcy

## 16.1 Core Offline Rule

A disconnected player's company should be financially and operationally frozen.

While offline, the player must not:

- Earn ongoing ISP revenue.
- Accumulate new ISP operating expenses.
- Experience new ISP equipment deterioration.
- Receive new ISP offers.
- Experience new ISP reliability penalties.
- Become bankrupt because of continuing offline ISP expenses.

Where necessary, the freeze should integrate with the game's broader offline-company simulation rather than applying only to ISP accounting.

## 16.2 ISP Provider Disconnect

If Player A operates an ISP serving Player B and Player A disconnects:

1. Cancel the player-to-player ISP agreement without penalties.
2. Automatically reconnect Player B to the vanilla ISP.
3. Apply the normal vanilla ISP rate from reconnection onward.
4. Do not charge a reconnection fee.
5. Do not apply service-failure reputation penalties solely because of the disconnect.
6. Stop further revenue and expenses attributable to that player-to-player agreement.
7. Freeze Player A's ISP business state.

The cancellation and reconnection should occur through the authoritative multiplayer session.

## 16.3 ISP Customer Disconnect

If the customer disconnects:

- Cancel the player-to-player ISP agreement without penalties.
- Stop subsequent billing and provider revenue attributable to that customer.
- Freeze the disconnected customer's company state.
- Allow the ISP provider to continue operating for remaining active customers.
- When the customer reconnects, use vanilla ISP service until another player agreement is established.

## 16.4 AI and Municipal Deals During Disconnect

AI corporate and municipal deals held by the disconnected player are frozen.

During the freeze:

- Do not generate revenue.
- Do not generate ISP operating expenses.
- Do not change deal Performance due to elapsed offline time.
- Do not accumulate new municipal breach months.
- Do not generate new offers or expand demand.

The implementation must determine how to suspend relevant dates, expiration counters, and obligations without conflicting with the host's game calendar.

## 16.5 Bankruptcy

If a player ISP becomes bankrupt while actively simulated:

- Terminate player-to-player agreements without penalizing affected customers.
- Reconnect remaining online customers to the vanilla ISP.
- Avoid charging reconnection fees to customers.
- Follow the game's normal bankruptcy handling for the provider company.

A disconnected company must not become bankrupt merely because ISP operating expenses continue accumulating while its owner is offline.

## 16.6 Synchronization Safeguards

All multiplayer disconnect, cancellation, freeze, and reconnection operations must occur exactly once.

Avoid:

- Duplicate cancellation.
- Duplicate bills.
- Stale ISP providers.
- Reconnection fees charged during automatic fallback.
- Revenue generated by disconnected providers.
- Frozen states persisting incorrectly after a player returns.

---

# 17. Vanilla Hosting and ISP Integration

## 17.1 Separate Services

Hosting and ISP service remain independent.

A player can simultaneously:

- Host a product.
- Supply internet service to that product.
- Host an entire customer's software infrastructure.
- Supply that customer with ISP connectivity.

Neither service automatically replaces the other.

## 17.2 Coverage Validation

When a product or company needs internet access:

1. Determine its eligible internet demand.
2. Check for a valid company-wide ISP agreement.
3. Check any applicable product-level ISP agreement.
4. Exclude traffic already covered by another agreement.
5. Apply the appropriate default service rules to remaining uncovered traffic.

Do not charge the same internet traffic to multiple ISP providers.

Do not automatically treat a hosting agreement as an ISP agreement.

## 17.3 Self-Hosting and Internal ISP Service

When a player supplies both hosting and ISP infrastructure internally:

- Keep hosting capacity separate from ISP capacity.
- Calculate internet traffic only once.
- Record legitimate savings from replacing vanilla ISP service.
- Continue accounting for real equipment and transit expenses.
- Do not generate revenue simply by transferring money within the same company.

---

# 18. ISP Management Interface

## 18.1 UI Location

Preferred choices:

1. A dedicated ISP tab in the existing Server management window.
2. A dedicated ISP entry in the Logistics section of the main game interface.

Choose the approach that requires the least invasive UI modification while remaining intuitive.

## 18.2 Network Tab

Display:

- ISP pools.
- Linked ISP servers.
- Installed capacity.
- Current utilization.
- Available capacity.
- Failed or unavailable capacity.
- Equipment condition.
- Electricity usage.
- Link and unlink controls.

## 18.3 Services Tab

Display:

- Corporate product ISP deals.
- Corporate company ISP deals.
- Municipal coverage deals.
- Multiplayer customers.
- Total contracted demand.
- Revenue.
- Operating expenses.
- Municipal coverage population.
- Settlement population.
- Expansion opportunities.
- Consolidate City Coverage action.

## 18.4 Reliability Tab

Display:

- Current service fulfillment.
- Historical reliability.
- Equipment failures.
- Failed server capacity.
- Current deal Performance.
- Estimated reputation changes.
- Recent service shortages.
- Relevant municipal breach warnings.

Avoid adding unnecessary UI complexity.

Reuse vanilla styles and controls where possible.

---

# 19. Save/Load Persistence

## 19.1 Dedicated Mod State

Vanilla furniture objects do not necessarily persist ISP-specific assignments.

Store authoritative ISP state in a versioned mod save-data structure.

Persist:

- ISP company identifiers.
- ISP pools.
- Server assignments.
- Stable furniture identifiers.
- Active corporate ISP agreements.
- Active municipal agreements.
- Multiplayer ISP relationships.
- Deal Performance.
- Reliability history required for penalties.
- Municipal coverage commitments.
- Breach counters and lawsuit state.
- Billing checkpoints.
- Relevant freeze/disconnect state.

Population and adoption caches may be recomputed rather than saved.

## 19.2 Stable Object References

Use stable furniture, company, and agreement identifiers wherever available.

Do not assume that an object's runtime memory reference remains valid after loading.

Verify that GUIDs or other stable identifiers survive the game's save/load reconstruction.

## 19.3 Two-Stage Loading

Loading should follow this general sequence:

1. Deserialize mod-owned data.
2. Validate the mod save schema version.
3. Allow vanilla companies and furniture to initialize.
4. Rebuild ISP references during `GameReader` or post-load initialization.
5. Recalculate available ISP capacity.
6. Restore active agreements and reliability history.
7. Validate provider and customer identities.
8. Rebuild derived caches.
9. Resume authoritative billing only after initialization completes.

## 19.4 Missing-Object Recovery

If referenced furniture or companies no longer exist:

- Do not crash.
- Remove invalid equipment assignments.
- Mark affected ISP capacity unavailable.
- Resolve invalid deals safely.
- Log recoverable errors.
- Preserve unrelated valid ISP state.

Avoid `NullReferenceException` failures from stale server or company references.

## 19.5 Save Schema Versioning

Include a version number in the mod save-data structure.

Future versions should be able to migrate older ISP saves safely.

If migration is impossible, identify the incompatibility clearly rather than silently discarding important state.

---

# 20. Billing and Anti-Duplication

## 20.1 Authoritative Settlements

All ISP financial operations must run through authoritative simulation state.

Do not allow multiple clients to independently settle the same agreement.

## 20.2 Settlement Identifiers

Each financial settlement should have a unique processed-period identifier.

Example:

`CompanyID + AgreementID + GameMonth + SimulationPeriod`

Before processing a settlement:

1. Verify agreement validity.
2. Verify provider and customer identities.
3. Determine delivered bandwidth.
4. Check the processed-period record.
5. Apply payment or expense once.
6. Record settlement completion.

## 20.3 Save/Load Protection

Reloading a saved game must not allow players to receive duplicate ISP income or pay duplicate transit expenses for an already completed period.

The same principle applies to:

- Multiplayer synchronization.
- Product/company deal consolidation.
- Municipal deal consolidation.
- Player disconnects.
- Automatic vanilla ISP reconnections.

Use authoritative processed-period records and idempotent settlement operations.

---

# 21. Performance and Optimization

## 21.1 Corporate Demand

Do not continuously scan all AI products and projects every frame.

Prefer:

- Event-driven updates.
- Cached company demand.
- Periodic demand refreshes.
- Monthly offer ranking.
- Existing vanilla billing intervals.

Avoid computing identical company-wide bandwidth requirements independently for multiple deals.

## 21.2 AI Development Overhead

Use vanilla-derived formulas.

Cache development-load estimates.

Refresh when projects are created, completed, cancelled, or materially changed, or when an appropriate periodic update is required.

## 21.3 Population

Population is calculated from the exponential growth formula.

Cache by settlement type and year.

Do not simulate individual residents.

Do not access unrelated vanilla population systems for ISP growth.

## 21.4 Adoption and Consumption

Cache yearly or monthly:

- Internet adoption.
- Consumption per adopting user.
- Settlement population.

Calculate aggregate demand from these values.

## 21.5 Equipment Capacity

Update ISP pool capacity when:

- Equipment is linked or unlinked.
- Equipment breaks or is repaired.
- Equipment is removed or sold.
- Relevant power availability changes.
- Equipment availability changes through vanilla mechanics.

Avoid unnecessary full scans when cached aggregates can be updated safely.

## 21.6 Multiplayer

Use authoritative, deterministic state changes.

Avoid independently processing financial settlements on every client.

Prefer existing game simulation update mechanisms.

---

# 22. Configuration Requirements

Expose all major balancing and operating values through configuration files.

## 22.1 Infrastructure

- ISP equipment capacities.
- Equipment power consumption.
- Efficiency progression.
- Equipment unlock years.
- Optional idle power behavior.
- ISP pool configuration where necessary.

## 22.2 Economics

- Vanilla ISP prices.
- Wholesale transit curve.
- Corporate reference price.
- Municipal reference price.
- Business-star payout behavior.
- Coverage payment formula.
- Coverage payment scaling.
- Reconnection fees.
- Premium agreement pricing.

## 22.3 Corporate Deals

- Monthly product offer limit: 10.
- Monthly company offer limit: 4.
- Offer expiration: 24 months.
- Company-wide unlock: 1990.
- Company-wide minimum demand: 1,000 Mbps.
- Development overhead parameters.
- Consolidation behavior.

## 22.4 Municipal Population

- Rural starting population: 2,500.
- Town starting population: 10,000.
- City starting population: 50,000.
- Rural population target year: 2080.
- Town population target year: 2065.
- City population target year: 2050.
- Population cap: 40,000,000.
- Growth-rate calculation.

## 22.5 Municipal Coverage

- Initial coverage sizes.
- Expansion offer timing.
- Expansion size multipliers.
- Available population limitations.
- Reliability requirements.
- Consolidation behavior.

## 22.6 Internet Adoption

- 1980 adoption: 5%.
- 2020 adoption: 70%.
- 2080 adoption: 100%.
- Interpolation rules.
- Optional future settlement-specific overrides.

## 22.7 Internet Consumption

- Historical MB/GB usage curve.
- Extended 2016–2080 usage curve.
- Maximum reference usage of 740 GB/month in 2080.
- Interpolation method.

## 22.8 Reliability

- Performance thresholds.
- Performance adjustments.
- Reputation scaling.
- Volume-weighting formula.
- Municipal breach thresholds.
- Lawsuit eligibility parameters.
- Zero-capacity lawsuit delay.

## 22.9 Multiplayer

- Manual reconnection fee.
- Premium contract rate.
- Premium failure threshold.
- Disconnect behavior.
- Freeze-state handling.
- Automatic reconnection rules.

---

# 23. Implementation Roadmap

## Phase 1 — Technical Investigation

Inspect the game assemblies and identify appropriate Harmony patch points.

Priority targets:

- Server furniture definitions.
- Server group management.
- Server equipment condition.
- IT repair behavior.
- Electricity calculation.
- ISP billing.
- Hosting deals.
- Business reputation.
- Business-star pricing.
- `SimulatedCompany.PayExpenses`.
- `SoftwareAlpha.GetLoadRequirement`.
- Software ISP flags.
- Existing lawsuit mechanics.
- Save/load processing.
- Multiplayer company simulation.
- Multiplayer synchronization.
- Logistics and Server interfaces.

Do not assume class or method signatures are correct until verified.

## Phase 2 — Infrastructure

Implement:

- ISP mode.
- Server linking.
- ISP pools.
- Bandwidth capacity.
- Electricity handling.
- Wholesale transit.
- Internal service accounting.

## Phase 3 — Corporate Product Deals

Implement:

- Product ISP offer generation.
- Live-user demand.
- Pricing.
- Revenue settlement.
- Offer backlog and expiration.

## Phase 4 — Reliability and Reputation

Implement:

- Capacity shortages.
- Equipment failure integration.
- Bandwidth fulfillment.
- Deal Performance.
- Volume-weighted reputation.

## Phase 5 — Company-Wide Deals

Implement:

- Company-level demand.
- Development bandwidth.
- Umbrella coverage.
- Monthly company offers.
- Product deal consolidation.
- Acquisition and dissolution behavior.

## Phase 6 — Municipal ISP System

Implement:

1. Independent settlement population.
2. Exponential growth.
3. Population caps.
4. Municipal coverage agreements.
5. Expansion offers.
6. Historical internet consumption.
7. Extended 2016–2080 usage.
8. Adoption curve.
9. Aggregate bandwidth demand.
10. Coverage payments.
11. Anti-stacking.
12. Deal consolidation.
13. Municipal breach tracking.
14. Vanilla lawsuit integration, if supported.

## Phase 7 — Multiplayer

Implement:

- Player ISP agreements.
- Customer billing.
- Manual reconnection.
- Premium agreements.
- Automatic fallback upon premium termination.
- Disconnect-related penalty-free cancellation.
- Offline company freezing.
- Bankruptcy cleanup.
- Authoritative multiplayer synchronization.

## Phase 8 — Save/Load and UI

Complete:

- Versioned persistence.
- Reference reconstruction.
- Missing-object recovery.
- Duplicate-billing protection.
- ISP Network tab.
- ISP Services tab.
- ISP Reliability tab.

Persistence should be introduced early enough in development to support testing and not postponed entirely until this phase.

## Phase 9 — Economic Balancing

Using actual vanilla game data, balance:

- Equipment purchase prices.
- ISP capacities.
- Power consumption.
- IT staffing costs.
- Wholesale transit.
- Corporate deal payouts.
- Municipal coverage payments.
- Revenue per Mbps.
- Reliability and reputation.
- Early-game profitability.
- Long-term municipal income.

Do not substitute guessed vanilla values where actual game data is available.

## Phase 10 — Testing

### Game progression

Test:

- 1980–1990.
- 1990–2000.
- 2000–2010.
- 2010–2020.
- 2020–2030.
- 2030–2050.
- 2050–2080.
- Beyond 2080.

### Population milestones

- City reaches 40 million in 2050.
- Town reaches 40 million in 2065.
- Rural reaches 40 million in 2080.
- Population remains capped afterward.

### Adoption milestones

- 5% in 1980.
- 70% in 2020.
- 100% in 2080.
- Correct interpolation.

### ISP infrastructure

- Server linking and unlinking.
- ISP pool aggregation.
- Server breakdown.
- Repair.
- Equipment sale.
- Server movement.
- Hosting/ISP mode exclusivity.
- Electricity and transit expense.

### Deals

- Product offer generation.
- Company offer generation.
- Offer expiration.
- Company umbrella coverage.
- Product deal consolidation.
- Municipal expansion.
- Municipal consolidation.
- Duplicate-billing prevention.
- Hosting and ISP coexistence.

### Reliability and legal

- Capacity shortage.
- Overcommitment.
- Performance adjustments.
- Reputation changes.
- Three-month below-90% breach notice.
- Five-month below-75% lawsuit eligibility.
- One-month zero-capacity guaranteed lawsuit initiation.
- Duplicate lawsuit prevention.
- Coverage payment reduction.

### Multiplayer

- Player provider selection.
- Standard agreement failure.
- Premium termination.
- Manual reconnection.
- Provider disconnect.
- Customer disconnect.
- Offline financial freeze.
- Automatic vanilla reconnection.
- Bankruptcy.
- Player return.
- Save/load during active agreements.

### Time scaling

Test all valid `DaysPerMonth` settings.

Verify monthly economic parity for equivalent service usage.

---

# 24. Outstanding Technical Questions

The implementation agent must resolve the following through actual game-code inspection.

1. Can existing server furniture be assigned ISP roles without creating duplicates?
2. How can ISP pools reuse vanilla server-linking behavior?
3. What equipment wattage and capacities produce the intended ISP efficiency progression?
4. How does vanilla measure and charge ISP bandwidth?
5. How exactly does `DaysPerMonth` affect economic time and hourly billing?
6. What units does `SoftwareAlpha.GetLoadRequirement` return?
7. How should AI development bandwidth be converted into Mbps?
8. Which exact methods generate hosting deals and receptionist visits?
9. How does the game calculate business reputation changes?
10. Does deal Performance have effects beyond cancellation?
11. Can a native lawsuit represent municipal service failure?
12. Which lawsuit methods should be patched to create a valid breach claim?
13. How does the game represent AI acquisitions, subsidiaries, mergers, and dissolution?
14. What stable furniture identifiers survive save/load?
15. What save extension mechanism can safely persist ISP data?
16. Does the multiplayer host continue simulating disconnected companies?
17. How can an offline company be frozen without affecting other players?
18. How can ISP provider substitution be integrated into multiplayer billing?
19. What is the correct mechanism for reconnection to vanilla service?
20. How can billing remain idempotent across saves and multiplayer synchronization?
21. What municipal coverage payment makes the business profitable in 1980?
22. Is additional peak-bandwidth capacity needed when converting average monthly traffic to Mbps?

Where the existing game architecture cannot safely support a requested feature, identify the limitation and propose an alternative for review rather than silently altering the design.

---

# 25. Final Development Instructions

The intended mod is a business expansion for Software Inc., not a full telecommunications simulator.

Prioritize reuse of:

- Vanilla server furniture.
- Vanilla server linking.
- Vanilla IT employees.
- Vanilla equipment condition.
- Vanilla electricity.
- Vanilla Deals.
- Vanilla hosting-demand calculations.
- Vanilla business reputation.
- Vanilla lawsuits.
- Vanilla company accounting.
- Vanilla multiplayer synchronization.

Avoid introducing:

- Unnecessary custom furniture.
- New router models.
- Special telecommunications employee types.
- Individual residential subscribers.
- Fiber installation or routing mechanics.
- Competing AI-owned ISPs.
- Separate oversubscription mechanics.
- Arbitrary network failure probabilities.
- Global modifications to development `UsesISP` behavior.
- Artificial internal-company ISP revenue.
- Continuous frame-by-frame scans of all companies.
- Unsafe direct edits to the game's core assemblies.

## Implementation strategy

1. Begin with technical investigation of actual game files.
2. Verify important class and method assumptions.
3. Implement the smallest working ISP infrastructure system.
4. Integrate corporate ISP deals.
5. Extend to company-wide corporate demand.
6. Add municipal ISP coverage and historical usage.
7. Implement reliability, reputation, and legal consequences.
8. Add multiplayer agreements and disconnection handling.
9. Finalize persistence, interface, and economic balancing.
10. Test across the entire game timeline and supported simulation settings.

Preserve the approved gameplay intent wherever technically feasible.

**Final objective:** Create a scalable, profitable, player-operated ISP business that feels native to Software Inc., allows service to AI companies, municipalities, and other players, and rewards efficient infrastructure management, reliable service delivery, and long-term business growth.

---

**End of Design Specification v0.7**
