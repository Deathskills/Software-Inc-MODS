// ISP Business - municipal ISP (spec 5.3, 8-12, 14; Milestone D).
//
//  - Three settlements (one rural, one town, one city) with their own population, independent of vanilla.
//    Only the one matching the map chosen when the game was created (GameSettings.EnvType: Rural, Town or
//    City) makes offers; it's the same map for every player in multiplayer.
//    Population(year) = min(40M, initial x (1 + g)^(year - 1980)), g reaching 40M in the target year.
//    Nothing vanilla (labour, market size, population) is read or changed.
//  - Coverage deals: a settlement contracts you to serve a number of residents. Your first deal there is
//    small (100 / 200 / 300 residents); expansion offers follow at 3, 6, 9 and 12 months (up to 2x, 4x,
//    8x the first deal, then the whole available population), then every 6-12 months for growth.
//    Expansions need good service in that settlement (90%+ last month and Performance above 0.5).
//  - Bandwidth: residents x adoption x MB per user per month x 8 / 2,592,000 (30-day month, so it doesn't
//    depend on the days-per-month setting). Adoption 5% (1980) -> 70% (2020) -> 100% (2080).
//  - Revenue: delivered Mbps x $5.06 x star multiplier, plus a coverage payment per settlement:
//    $1000 + $25 x sqrt(residents you cover there) a month, computed once on all your coverage in that
//    settlement (several small deals don't stack minimum payments), split across its deals by residents,
//    and paid only for the share of traffic you actually delivered. No capacity, no payment.
//  - Breaches: under 90% for 3 months in a row -> breach notice (reputation hit); under 75% for 5 months,
//    or no service at all for a whole month -> the settlement sues you (vanilla lawsuit, if available).
//    Counters reset when service recovers; one suit per continuing breach.
//  - "Consolidate" merges your deals in one settlement into one, keeping weighted price and Performance.
//  - Multiplayer: each player tells the others how many residents they cover per settlement, so the
//    available population is shared. One side decides: each player only reports their own coverage.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ISPBusiness
{
    public sealed class Settlement
    {
        public int Id;
        public string Name;
        public int Type;            // 0 rural, 1 town, 2 city
        public string TypeName { get { return Type == 0 ? "Rural" : Type == 1 ? "Town" : "City"; } }
    }

    /// <summary>Your municipal history in one settlement (saved).</summary>
    public sealed class SettlementState
    {
        public int FirstAcceptedAt = -1;   // stamp of your first deal there, or -1
        public int NextGrowthOfferAt = -1; // after the 12-month stage: next growth offer stamp
        public int LowMonths90, LowMonths75, ZeroMonths;
        public bool NoticeSent, SuitFiled;
        public double LastFulfilment = 1.0;
        public double MonthDemand, MonthDelivered;   // Mbps-hours this month, all your deals there
        public bool MonthHadCapacity;                // some of your pools carried any traffic there this month
    }

    public static class Municipal
    {
        // One of each. Ids stay fixed (they're in saves and multiplayer messages).
        public static readonly Settlement[] Settlements =
        {
            new Settlement { Id = 1, Name = "Maple Hollow", Type = 0 },
            new Settlement { Id = 4, Name = "Riverton",     Type = 1 },
            new Settlement { Id = 7, Name = "Port Haven",   Type = 2 },
        };

        public static readonly Dictionary<int, SettlementState> States = new Dictionary<int, SettlementState>();

        /// <summary>Residents other players cover: settlement -> (player -> residents). Not saved.</summary>
        public static readonly Dictionary<int, Dictionary<byte, double>> Others = new Dictionary<int, Dictionary<byte, double>>();

        public static void Reset()
        {
            States.Clear();
            Others.Clear();
            _snapMonth = int.MinValue;
        }

        // ---------- monthly snapshot: population, adoption and use per user only change when the month
        // ticks (just before offers go out), not every hour. Rebuilt on load, never saved.
        private static int _snapMonth = int.MinValue;
        private static readonly double[] _pop = new double[3];
        private static double _adoption, _usageMB;

        /// <summary>Takes the month's snapshot (called at the month change before offers, and lazily after a load).</summary>
        public static void Snapshot()
        {
            double year = GameClock.Year;
            for (int t = 0; t < 3; t++) _pop[t] = Math.Floor(Population(t, year));
            _adoption = Adoption(year);
            _usageMB = UsageMBPerUser(year);
            _snapMonth = GameClock.MonthIndex;
        }

        private static void EnsureSnapshot() { if (_snapMonth == int.MinValue) Snapshot(); }

        /// <summary>This month's population of a settlement type.</summary>
        public static double PopulationNow(int type) { EnsureSnapshot(); return _pop[Math.Max(0, Math.Min(2, type))]; }
        public static double AdoptionNow { get { EnsureSnapshot(); return _adoption; } }
        public static double UsageNow { get { EnsureSnapshot(); return _usageMB; } }

        /// <summary>This month's average Mbps for a number of covered residents.</summary>
        public static double DemandNow(double residents)
        {
            EnsureSnapshot();
            return residents * _adoption * _usageMB * 8.0 / 2592000.0;
        }

        public static Settlement Get(int id) { return Settlements.FirstOrDefault(s => s.Id == id); }

        /// <summary>Map type chosen at game creation: 0 rural, 1 town, 2 city (saved by vanilla, shared in multiplayer).</summary>
        public static int MapType
        {
            get
            {
                GameSettings gs = GameSettings.Instance;
                return gs != null ? (int)gs.EnvType : 1;
            }
        }

        /// <summary>The settlement of this game's map: the only one that makes offers.</summary>
        public static bool OnMap(Settlement s) { return s != null && s.Type == MapType; }

        /// <summary>Settlements to show: the map's, plus any you still have deals in.</summary>
        public static IEnumerable<Settlement> Shown() { return Settlements.Where(s => OnMap(s) || MyCoverage(s.Id) > 0); }

        public static SettlementState State(int id)
        {
            if (!States.TryGetValue(id, out SettlementState st)) { st = new SettlementState(); States[id] = st; }
            return st;
        }

        // ---------- curves

        public static double Population(int type, double year)
        {
            double initial = Config.MuniInitialPopulation[type];
            double target = Config.MuniTargetYear[type];
            double cap = Config.MuniPopulationCap;
            if (year <= 1980) return initial;
            double g = Math.Pow(cap / initial, 1.0 / (target - 1980.0)) - 1.0;
            return Math.Min(cap, initial * Math.Pow(1.0 + g, year - 1980.0));
        }

        public static double Adoption(double year)
        {
            double a;
            if (year <= 1980) a = 0.05;
            else if (year <= 2020) a = 0.05 + (year - 1980) * 0.01625;
            else if (year <= 2080) a = 0.70 + (year - 2020) * 0.005;
            else a = 1.0;
            return Math.Min(1.0, a);
        }

        private static readonly double[] UsageYears = { 1980, 1989, 1990, 1993, 1994, 1995, 1996, 1997, 1998, 1999, 2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007, 2008, 2009, 2010, 2011, 2012, 2013, 2014, 2015, 2016, 2020, 2025, 2030, 2035, 2050, 2080 };
        private static readonly double[] UsageMB    = { 1, 5, 5, 9, 10, 20, 35, 60, 100, 180, 350, 650, 1100, 1800, 2900, 4500, 6800, 10200, 14500, 20000, 27500, 36000, 46500, 58000, 72000, 92000, 125000, 150000, 187000, 233000, 285000, 553000, 740000 };

        /// <summary>Average MB per adopting user per month (decimal MB), linear between control points.</summary>
        public static double UsageMBPerUser(double year)
        {
            if (year <= UsageYears[0]) return UsageMB[0];
            for (int i = 1; i < UsageYears.Length; i++)
            {
                if (year <= UsageYears[i])
                {
                    double t = (year - UsageYears[i - 1]) / (UsageYears[i] - UsageYears[i - 1]);
                    return UsageMB[i - 1] + (UsageMB[i] - UsageMB[i - 1]) * t;
                }
            }
            return UsageMB[UsageMB.Length - 1];
        }

        /// <summary>Average Mbps for a number of covered residents now.</summary>
        public static double DemandMbps(double residents, double year)
        {
            return residents * Adoption(year) * UsageMBPerUser(year) * 8.0 / 2592000.0;
        }

        /// <summary>Monthly coverage payment for all of one player's coverage in one settlement.</summary>
        public static double CoveragePayment(double residents)
        {
            if (residents <= 0) return 0;
            return Config.MuniCoverageBase + Config.MuniCoveragePerSqrt * Math.Sqrt(residents);
        }

        // ---------- coverage figures

        public static double MyCoverage(int id)
        {
            return Deals.Active.Where(d => d.IsMunicipal && d.Settlement == id).Sum(d => d.Residents);
        }

        public static double OthersCoverage(int id)
        {
            return Others.TryGetValue(id, out var m) ? m.Values.Sum() : 0;
        }

        public static double Available(int id)
        {
            Settlement s = Get(id);
            if (s == null) return 0;
            return Math.Max(0, Math.Floor(PopulationNow(s.Type) - MyCoverage(id) - OthersCoverage(id)));
        }

        // ---------- hourly (called from Deals.OnHour for each municipal deal)

        /// <summary>Revenue for one municipal deal this hour: bandwidth + its share of the earned coverage payment.</summary>
        public static double HourlyRevenue(IspDeal d, double demand, double delivered, double hoursPerMonth)
        {
            double fulfil = demand > 0 ? Math.Min(1.0, delivered / demand) : 0.0;
            double total = MyCoverage(d.Settlement);
            double share = total > 0 ? d.Residents / total : 0;
            double coverage = CoveragePayment(total) * share * fulfil / hoursPerMonth;
            double bandwidth = delivered * d.Rate / hoursPerMonth;
            SettlementState st = State(d.Settlement);
            st.MonthDemand += demand;
            st.MonthDelivered += delivered;
            if (delivered > 0) st.MonthHadCapacity = true;
            return coverage + bandwidth;
        }

        // ---------- monthly

        /// <summary>Breach tracking per settlement, after the month's deal reviews.</summary>
        public static void MonthlyBreaches()
        {
            foreach (Settlement s in Settlements)
            {
                if (!States.TryGetValue(s.Id, out SettlementState st)) continue;
                bool covered = MyCoverage(s.Id) > 0;
                if (!covered)
                {
                    st.LowMonths90 = st.LowMonths75 = st.ZeroMonths = 0;
                    st.NoticeSent = st.SuitFiled = false;
                    st.MonthDemand = st.MonthDelivered = 0;
                    st.MonthHadCapacity = false;
                    continue;
                }
                double f = st.MonthDemand > 0 ? Math.Min(1.0, st.MonthDelivered / st.MonthDemand) : 1.0;
                st.LastFulfilment = f;
                st.LowMonths90 = f < 0.90 ? st.LowMonths90 + 1 : 0;
                st.LowMonths75 = f < 0.75 ? st.LowMonths75 + 1 : 0;
                st.ZeroMonths = st.MonthDemand > 0 && !st.MonthHadCapacity ? st.ZeroMonths + 1 : 0;
                if (f >= 0.90) { st.NoticeSent = false; st.SuitFiled = false; } // recovered: a later breach is a new one

                if (st.LowMonths90 >= 3 && !st.NoticeSent)
                {
                    st.NoticeSent = true;
                    GameSettings.Instance.MyCompany.ChangeBusinessRep((float)Config.MuniBreachRep, "ISP");
                    IspNetwork.PendingNotices.Add(s.Name + " sent you a breach notice: your ISP service there has been under 90% for " + st.LowMonths90 +
                                                  " months. Keep it under 75% for 5 months, or give no service for a whole month, and they will sue.");
                }
                if (!st.SuitFiled && (st.LowMonths75 >= 5 || st.ZeroMonths >= 1))
                {
                    st.SuitFiled = Lawsuits.FileMunicipal(s, st.ZeroMonths >= 1, MyCoverage(s.Id));
                }
                st.MonthDemand = st.MonthDelivered = 0;
                st.MonthHadCapacity = false;
            }
        }

        /// <summary>Starting and expansion offers. Returns how many were made.</summary>
        public static void MonthlyOffers(int stars, int now, Func<string, int, bool> recentlyDeclined, Func<SDateTime?> reception, Func<int> nextId)
        {
            int starts = 0;
            var rng = new System.Random(now);
            foreach (Settlement s in Settlements.OrderBy(x => rng.Next()))
            {
                if (!OnMap(s)) continue;
                if (Deals.Offers.Any(o => o.IsMunicipal && o.Settlement == s.Id)) continue;
                if (recentlyDeclined("m:" + s.Id, now)) continue;
                double available = Available(s.Id);
                double mine = MyCoverage(s.Id);
                double initial = Config.MuniInitialCoverage[s.Type];
                double size;
                SettlementState st = State(s.Id);

                if (mine <= 0)
                {
                    if (starts >= Config.MuniStartOffersPerMonth) continue;
                    size = Math.Min(initial, available);
                    if (size < 1) continue;
                    starts++;
                }
                else
                {
                    // Expansions need good recent service there.
                    if (st.LastFulfilment < 0.90) continue;
                    double perf = Deals.Active.Where(d => d.IsMunicipal && d.Settlement == s.Id).Select(d => d.Performance).DefaultIfEmpty(1.0).Min();
                    if (perf < 0.5) continue;
                    double months = st.FirstAcceptedAt < 0 ? 0 : GameClock.MonthsBetween(st.FirstAcceptedAt, now);
                    double target;
                    if (months >= 12)
                    {
                        if (st.NextGrowthOfferAt > now) continue;
                        target = mine + available;
                    }
                    else if (months >= 9) target = initial * 8;
                    else if (months >= 6) target = initial * 4;
                    else if (months >= 3) target = initial * 2;
                    else continue;
                    size = Math.Min(available, target - mine);
                    if (size < Math.Max(1, initial * 0.5)) continue;
                    if (months >= 12) st.NextGrowthOfferAt = now + (int)(GameClock.MinutesPerMonth * (6 + rng.NextDouble() * 6));
                }

                SDateTime? when = reception();
                if (!when.HasValue) break; // no receptionist
                Deals.Offers.Add(new IspDeal
                {
                    Id = nextId(),
                    Kind = IspDeal.KindMunicipal,
                    Settlement = s.Id,
                    Residents = Math.Floor(size),
                    ProductName = s.Name,
                    CompanyName = s.Name + " (" + s.TypeName.ToLowerInvariant() + ")",
                    Rate = Config.MunicipalReferencePrice * Config.StarMultiplier(stars, UnityEngine.Random.value),
                    OfferedAt = now,
                    EstimatedMbps = DemandNow(Math.Floor(size)),
                    ShowAt = when.Value.ToInt(),
                });
            }
        }

        public static void OnAccepted(IspDeal d)
        {
            SettlementState st = State(d.Settlement);
            if (st.FirstAcceptedAt < 0 || MyCoverage(d.Settlement) <= d.Residents) st.FirstAcceptedAt = GameClock.Stamp;
            Net.BroadcastCoverage(d.Settlement, MyCoverage(d.Settlement));
        }

        public static void OnEnded(IspDeal d)
        {
            if (MyCoverage(d.Settlement) <= 0) State(d.Settlement).FirstAcceptedAt = -1;
            Net.BroadcastCoverage(d.Settlement, MyCoverage(d.Settlement));
        }

        /// <summary>Merges your deals in one settlement into one. Not a cancellation: no penalty, no new coverage.</summary>
        public static bool Consolidate(int id, out string message)
        {
            List<IspDeal> list = Deals.Active.Where(d => d.IsMunicipal && d.Settlement == id).OrderBy(d => d.AcceptedAt).ToList();
            Settlement s = Get(id);
            if (list.Count < 2) { message = "You need at least two coverage deals in " + (s != null ? s.Name : "this settlement") + " to consolidate them."; return false; }
            IspDeal keep = list[0];
            double residents = list.Sum(d => d.Residents);
            double w(IspDeal d) => residents > 0 ? d.Residents / residents : 1.0 / list.Count;
            keep.Rate = list.Sum(d => d.Rate * w(d));
            keep.Performance = list.Sum(d => d.Performance * w(d));
            keep.LastFulfilment = list.Sum(d => d.LastFulfilment * w(d));
            keep.MonthDemand = list.Sum(d => d.MonthDemand);
            keep.MonthDelivered = list.Sum(d => d.MonthDelivered);
            keep.TotalRevenue = list.Sum(d => d.TotalRevenue);
            keep.Unpaid = list.Sum(d => d.Unpaid);
            keep.Residents = residents;
            foreach (IspDeal d in list.Skip(1)) Deals.Retire(d);
            message = "Your " + list.Count + " coverage deals in " + (s != null ? s.Name : "the settlement") + " are now one deal for " +
                      residents.ToString("N0", CultureInfo.InvariantCulture) + " residents.";
            return true;
        }

        // ---------- multiplayer

        public static void OnRemoteCoverage(byte player, int settlement, double residents)
        {
            if (!Others.TryGetValue(settlement, out var m)) { m = new Dictionary<byte, double>(); Others[settlement] = m; }
            if (residents <= 0) m.Remove(player); else m[player] = residents;
        }

        public static void OnPlayerLeft(byte player)
        {
            foreach (var m in Others.Values) m.Remove(player);
        }

        public static IEnumerable<KeyValuePair<int, double>> MyCoverages()
        {
            return Settlements.Select(s => new KeyValuePair<int, double>(s.Id, MyCoverage(s.Id))).Where(kv => kv.Value > 0);
        }

        // ---------- save data

        public static string PackStates()
        {
            return Pack.List(States.Select(kv => string.Join(Pack.Field.ToString(), new[]
            {
                Pack.L(kv.Key), Pack.L(kv.Value.FirstAcceptedAt), Pack.L(kv.Value.NextGrowthOfferAt), Pack.L(kv.Value.LowMonths90),
                Pack.L(kv.Value.LowMonths75), Pack.L(kv.Value.ZeroMonths), Pack.L(kv.Value.NoticeSent ? 1 : 0), Pack.L(kv.Value.SuitFiled ? 1 : 0),
                Pack.D(kv.Value.LastFulfilment), Pack.D(kv.Value.MonthDemand), Pack.D(kv.Value.MonthDelivered), Pack.L(kv.Value.MonthHadCapacity ? 1 : 0),
            })));
        }

        public static void UnpackStates(string s)
        {
            States.Clear();
            foreach (string rec in Pack.Unlist(s))
            {
                string[] f = rec.Split(Pack.Field);
                if (f.Length < 12) continue;
                States[(int)Pack.L(f[0])] = new SettlementState
                {
                    FirstAcceptedAt = (int)Pack.L(f[1]), NextGrowthOfferAt = (int)Pack.L(f[2]), LowMonths90 = (int)Pack.L(f[3]),
                    LowMonths75 = (int)Pack.L(f[4]), ZeroMonths = (int)Pack.L(f[5]), NoticeSent = Pack.L(f[6]) == 1, SuitFiled = Pack.L(f[7]) == 1,
                    LastFulfilment = Pack.D(f[8]), MonthDemand = Pack.D(f[9]), MonthDelivered = Pack.D(f[10]), MonthHadCapacity = Pack.L(f[11]) == 1,
                };
            }
        }
    }
}
