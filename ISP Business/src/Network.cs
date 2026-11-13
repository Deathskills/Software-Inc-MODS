// ISP Business - ISP pools on vanilla server groups (spec sections 3, 4, 5.6, 13).
//
// An ISP pool is a vanilla server group that the player switched to ISP mode.
//  - Its servers give ISP capacity (Config.Servers) instead of hosting capacity.
//  - It can't host products: if items get assigned to it, it goes back to hosting mode.
//  - ISP traffic is split into "ISP processes", like vanilla server processes: your company's own
//    internet, and one per ISP deal. Each process is assigned to a pool.
//  - Failure and fallback are vanilla: one broken server sets the group's PowerSum to 0, so the whole
//    pool is down, and its processes follow the group's fallback chain to the first group that is up.
//    If that group is not an ISP pool (or there is none), the traffic is not served.
//  - A pool carrying more than its capacity is overloaded: every process on it gets the same share.
//  - Own internet that your pools carry is refunded from the vanilla "Internet" bill and replaced by
//    wholesale transit. Deal traffic pays transit too. Electricity comes from the servers themselves.
// Every player runs this for their own company only: in multiplayer each game simulates its own
// servers and bills, and vanilla broadcasts the resulting transactions.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ISPBusiness
{
    public sealed class PoolState
    {
        public string Name;
        public bool Up;
        public string DownReason;
        public int ServerCount;
        public double Capacity;     // ISP Mbps available now (0 when down)
        public double Installed;    // ISP Mbps if every server worked
        public double Load;         // Mbps of the processes routed here in the last hour
        public double Assigned;     // Mbps actually carried (min of Load and Capacity)
        public double Utilisation;  // Assigned / Capacity
        public double Watts;        // ISP electricity draw at the current load
        public bool Overloaded { get { return Up && Load > Capacity + 1e-9; } }
    }

    /// <summary>One stream of ISP traffic: your own internet, or one deal.</summary>
    public sealed class IspProcess
    {
        public string Key;          // "own" or "deal:<id>"
        public string Label;
        public double Demand;       // Mbps wanted in the last hour
        public double Delivered;    // Mbps delivered in the last hour
        public string ServedBy;     // pool that actually carried it (after fallback), or null
        public string Pool { get { return IspNetwork.GetAssignment(Key); } }
    }

    public static class IspNetwork
    {
        public const string OwnKey = "own";

        /// <summary>Names of the vanilla server groups in ISP mode. Saved with the game.</summary>
        public static readonly HashSet<string> Pools = new HashSet<string>();

        /// <summary>Process key -> pool name. Saved with the game.</summary>
        private static readonly Dictionary<string, string> Assign = new Dictionary<string, string>();

        /// <summary>Per-pool runtime figures from the last hour (not saved, rebuilt every hour).</summary>
        public static readonly Dictionary<string, PoolState> State = new Dictionary<string, PoolState>();

        /// <summary>Processes from the last hour, for the window (rebuilt every hour).</summary>
        public static readonly Dictionary<string, IspProcess> Processes = new Dictionary<string, IspProcess>();

        // This-month figures for the window.
        public static double MonthSavings, MonthTransit, MonthRevenue, LastMonthSavings, LastMonthTransit, LastMonthRevenue;
        // Mbps-hours this month, for last month's averages (the window shows last month, not live figures).
        public static double MonthOwnMbpsH, MonthOwnServedMbpsH, MonthCarriedMbpsH, MonthHours;
        public static double LastMonthOwnAvg, LastMonthOwnServedAvg, LastMonthCarriedAvg;
        /// <summary>Changes once a month: the window rebuilds its summary only when this changes.</summary>
        public static int StatsVersion;
        private static int _statsMonth = int.MinValue;

        /// <summary>Messages to show the player on the next frame (not from inside the hourly tick).</summary>
        public static readonly List<string> PendingNotices = new List<string>();

        public static void Reset()
        {
            Pools.Clear();
            Assign.Clear();
            State.Clear();
            Processes.Clear();
            MonthSavings = MonthTransit = MonthRevenue = LastMonthSavings = LastMonthTransit = LastMonthRevenue = 0;
            MonthOwnMbpsH = MonthOwnServedMbpsH = MonthCarriedMbpsH = MonthHours = 0;
            LastMonthOwnAvg = LastMonthOwnServedAvg = LastMonthCarriedAvg = 0;
            StatsVersion++;
            _statsMonth = int.MinValue;
            PendingNotices.Clear();
            Own();
        }

        // ---------- pools

        public static bool IsPool(ServerGroup g)
        {
            return g != null && g.Name != null && Pools.Contains(g.Name);
        }

        /// <summary>Why a group can't become an ISP pool, or null if it can.</summary>
        public static string CannotBePool(ServerGroup g)
        {
            if (g == null) return "This server group no longer exists.";
            if (g.CloudProvider > 0) return "Another player's cloud can't be an ISP pool.";
            if (g.IsCloud) return "This group is your multiplayer cloud. Stop the cloud service first.";
            if (g.Items.Count > 0) return "Move the processes off this group first (Servers window). A server group can either host or be an ISP pool, not both.";
            if (!g.Servers.OfType<Server>().Any()) return "This group has no server furniture.";
            return null;
        }

        public static bool SetPool(ServerGroup g, bool isp, out string error)
        {
            error = null;
            if (g == null) { error = "This server group no longer exists."; return false; }
            if (isp)
            {
                error = CannotBePool(g);
                if (error != null) return false;
                Pools.Add(g.Name);
                // The first pool picks up your own internet, so switching a group on does something right away.
                if (GetAssignment(OwnKey) == null) Assign[OwnKey] = g.Name;
                Recalculate();
                ApplyPower();
                return true;
            }
            Pools.Remove(g.Name);
            State.Remove(g.Name);
            RestoreVanilla(g);
            Recalculate();
            ApplyPower();
            return true;
        }

        private static void RestoreVanilla(ServerGroup g)
        {
            try
            {
                foreach (Server s in g.Servers.OfType<Server>())
                {
                    Furniture f = s.furn;
                    if (f == null) continue;
                    if (f.HasUpg) f.upg.AtrophyModifier = 1f;
                }
                g.RefreshUsage(); // vanilla UseModifier = LastUsed / PowerSum
            }
            catch (Exception e)
            {
                Debug.LogError("[ISP Business] Could not restore server group " + g.Name + ": " + e);
            }
        }

        public static void Rename(string oldName, string newName)
        {
            if (oldName == null || newName == null) return;
            foreach (string k in Assign.Where(kv => kv.Value == oldName).Select(kv => kv.Key).ToList()) Assign[k] = newName;
            if (!Pools.Remove(oldName)) return;
            Pools.Add(newName);
            if (State.TryGetValue(oldName, out PoolState st)) { State.Remove(oldName); st.Name = newName; State[newName] = st; }
        }

        // ---------- process assignment

        /// <summary>Pool a process is assigned to, or null if it isn't assigned to an existing pool.</summary>
        public static string GetAssignment(string key)
        {
            return key != null && Assign.TryGetValue(key, out string p) && Pools.Contains(p) ? p : null;
        }

        public static void SetAssignment(string key, string pool)
        {
            if (pool == null || !Pools.Contains(pool)) Assign.Remove(key);
            else Assign[key] = pool;
        }

        public static void RemoveProcess(string key)
        {
            Assign.Remove(key);
            Processes.Remove(key);
        }

        /// <summary>The pool with the most spare capacity, for a newly accepted deal.</summary>
        public static string BestPool()
        {
            string best = null; double spare = double.MinValue;
            foreach (PoolState st in State.Values)
            {
                double s = st.Installed - st.Load;
                if (s > spare) { spare = s; best = st.Name; }
            }
            if (best == null && Pools.Count > 0) best = Pools.OrderBy(x => x, StringComparer.Ordinal).First();
            return best;
        }

        // ---------- hourly tick (Harmony postfix on TimeOfDay.UpdateHour)

        /// <summary>Called once per in-game hour after vanilla added this hour's internet cost.</summary>
        /// <param name="internetCostThisHour">What vanilla added to GameSettings.ServerCost this hour.</param>
        public static void OnHour(double internetCostThisHour)
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null || gs.MyCompany == null) return;

            double hoursPerMonth = GameClock.HoursPerMonth;
            double ispRate = Server.GetISPCost();            // vanilla $ per Mbps per month
            double transitRate = Config.TransitRate(GameClock.Year);

            RollMonth();

            // The company's own internet traffic this hour, in Mbps, from what vanilla billed for it.
            IspProcess own = Own();
            own.Demand = internetCostThisHour > 0 && ispRate > 0 ? internetCostThisHour / (ispRate / hoursPerMonth) : 0;

            Deals.RefreshDemand();
            Recalculate();

            if (own.Delivered > 0)
            {
                double refund = own.Delivered * ispRate / hoursPerMonth;
                double transit = own.Delivered * transitRate / hoursPerMonth;
                // ServerCost is paid by vanilla as the monthly "Internet" bill. Swapping the vanilla rate
                // for transit here keeps one bill line and survives save/load (ServerCost is saved).
                gs.ServerCost = (float)Math.Max(0.0, gs.ServerCost - refund + transit);
                MonthSavings += refund - transit;
                MonthTransit += transit;
            }

            MonthHours += 1;
            MonthOwnMbpsH += own.Demand;
            MonthOwnServedMbpsH += own.Delivered;
            foreach (PoolState st in State.Values) MonthCarriedMbpsH += st.Assigned;

            Deals.OnHour(hoursPerMonth, transitRate);
            ApplyPower();
        }

        public static IspProcess Own()
        {
            if (!Processes.TryGetValue(OwnKey, out IspProcess own))
            {
                own = new IspProcess { Key = OwnKey, Label = "Your company's internet" };
                Processes[OwnKey] = own;
            }
            return own;
        }

        private static void RollMonth()
        {
            int month = GameClock.MonthIndex;
            if (month == _statsMonth) return;
            if (_statsMonth != int.MinValue)
            {
                LastMonthSavings = MonthSavings;
                LastMonthTransit = MonthTransit;
                LastMonthRevenue = MonthRevenue;
                double h = Math.Max(1.0, MonthHours);
                LastMonthOwnAvg = MonthOwnMbpsH / h;
                LastMonthOwnServedAvg = MonthOwnServedMbpsH / h;
                LastMonthCarriedAvg = MonthCarriedMbpsH / h;
            }
            MonthSavings = MonthTransit = MonthRevenue = 0;
            MonthOwnMbpsH = MonthOwnServedMbpsH = MonthCarriedMbpsH = MonthHours = 0;
            _statsMonth = month;
            StatsVersion++;
        }

        /// <summary>Month figures for the save, so "last month" survives a reload.</summary>
        public static string PackStats()
        {
            return string.Join(Pack.Field.ToString(), new[]
            {
                Pack.L(_statsMonth == int.MinValue ? -1 : _statsMonth), Pack.D(MonthSavings), Pack.D(MonthTransit), Pack.D(MonthRevenue),
                Pack.D(LastMonthSavings), Pack.D(LastMonthTransit), Pack.D(LastMonthRevenue),
                Pack.D(MonthOwnMbpsH), Pack.D(MonthOwnServedMbpsH), Pack.D(MonthCarriedMbpsH), Pack.D(MonthHours),
                Pack.D(LastMonthOwnAvg), Pack.D(LastMonthOwnServedAvg), Pack.D(LastMonthCarriedAvg),
            });
        }

        public static void UnpackStats(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            string[] f = s.Split(Pack.Field);
            if (f.Length < 14) return;
            long m = Pack.L(f[0]);
            _statsMonth = m < 0 ? int.MinValue : (int)m;
            MonthSavings = Pack.D(f[1]); MonthTransit = Pack.D(f[2]); MonthRevenue = Pack.D(f[3]);
            LastMonthSavings = Pack.D(f[4]); LastMonthTransit = Pack.D(f[5]); LastMonthRevenue = Pack.D(f[6]);
            MonthOwnMbpsH = Pack.D(f[7]); MonthOwnServedMbpsH = Pack.D(f[8]); MonthCarriedMbpsH = Pack.D(f[9]); MonthHours = Pack.D(f[10]);
            LastMonthOwnAvg = Pack.D(f[11]); LastMonthOwnServedAvg = Pack.D(f[12]); LastMonthCarriedAvg = Pack.D(f[13]);
            StatsVersion++;
        }

        /// <summary>Rebuilds every pool's capacity, routes every process (with vanilla fallback)
        /// and sets each process's delivered Mbps. Uses the demand already stored on the processes.</summary>
        public static void Recalculate()
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return;

            foreach (string name in Pools.ToList())
            {
                ServerGroup g = gs.GetServerGroup(name);
                if (g == null)
                {
                    // Group gone (all its servers sold or deleted): nothing left to serve from it.
                    Pools.Remove(name);
                    State.Remove(name);
                    continue;
                }
                if (g.Items.Count > 0 || g.IsCloud || g.CloudProvider > 0)
                {
                    Pools.Remove(name);
                    State.Remove(name);
                    RestoreVanilla(g);
                    PendingNotices.Add("Server group \"" + name + "\" was given processes to host, so it left ISP mode. Its ISP traffic is no longer served. A server group can either host or be an ISP pool, not both.");
                    continue;
                }
                if (!State.TryGetValue(name, out PoolState st)) { st = new PoolState { Name = name }; State[name] = st; }
                Measure(g, st);
                st.Load = 0;
            }

            // Route each process to the pool that serves it.
            foreach (IspProcess p in Processes.Values)
            {
                p.ServedBy = Route(gs, p.Pool);
                if (p.ServedBy != null) State[p.ServedBy].Load += p.Demand;
            }
            foreach (PoolState st in State.Values)
            {
                st.Assigned = Math.Min(st.Load, st.Capacity);
                st.Utilisation = st.Capacity > 0 ? st.Assigned / st.Capacity : 0;
            }
            foreach (IspProcess p in Processes.Values)
            {
                if (p.ServedBy == null) { p.Delivered = 0; continue; }
                PoolState st = State[p.ServedBy];
                double share = st.Load > 0 ? Math.Min(1.0, st.Capacity / st.Load) : 1.0;
                p.Delivered = p.Demand * share;
            }
        }

        /// <summary>Vanilla fallback: a group that is down hands its load to its fallback chain.</summary>
        private static string Route(GameSettings gs, string pool)
        {
            if (pool == null) return null;
            ServerGroup g = gs.GetServerGroup(pool);
            var seen = new HashSet<string>();
            while (g != null && seen.Add(g.Name))
            {
                if (g.PowerSum > 0f && !g.Broken)
                    return IsPool(g) && State.TryGetValue(g.Name, out PoolState st) && st.Up ? g.Name : null;
                if (g.Fallback == null) return null;
                g = gs.GetServerGroup(g.Fallback);
            }
            return null;
        }

        private static void Measure(ServerGroup g, PoolState st)
        {
            st.ServerCount = 0;
            st.Installed = 0;
            foreach (Server s in g.Servers.OfType<Server>())
            {
                Furniture f = s.furn;
                Config.Equipment eq = Config.ForServer(s.Power, f != null ? f.Wattage : 0f);
                st.Installed += eq.IspCapacity;
                st.ServerCount++;
            }
            // Vanilla sets PowerSum to 0 as soon as one server in the group is broken.
            st.Up = st.ServerCount > 0 && !g.Broken && g.PowerSum > 0f;
            st.DownReason = st.Up ? null : (st.ServerCount == 0 ? "No servers" : "A server is broken");
            st.Capacity = st.Up ? st.Installed : 0;
        }

        /// <summary>Sets each ISP server's power use and wear from its pool's utilisation.</summary>
        public static void ApplyPower()
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return;
            foreach (PoolState st in State.Values)
            {
                ServerGroup g = gs.GetServerGroup(st.Name);
                if (g != null) ApplyPower(g, st);
            }
        }

        public static void ApplyPower(ServerGroup g, PoolState st)
        {
            double watts = 0;
            float util = (float)st.Utilisation;
            foreach (Server s in g.Servers.OfType<Server>())
            {
                Furniture f = s.furn;
                if (f == null) continue;
                Config.Equipment eq = Config.ForServer(s.Power, f.Wattage);
                double target = eq.WattsPerMbps * eq.IspCapacity * util;   // watts this server should draw
                float mod = f.Wattage > 0f ? (float)(target / f.Wattage) : 0f;
                if (Math.Abs(f.UseModifier - mod) > 1e-6f) f.UseModifier = mod; // setter refreshes the electricity delta
                if (f.HasUpg) f.upg.AtrophyModifier = Math.Max(Config.IdleWearFloor, util + 0.25f);
                watts += f.CurrentWattage;
            }
            st.Watts = watts;
        }

        // ---------- save data (local save of each player, like vanilla server groups)

        public static string PackPools()
        {
            return Pack.List(Pools.OrderBy(x => x, StringComparer.Ordinal));
        }

        public static void UnpackPools(string s)
        {
            Pools.Clear();
            State.Clear();
            foreach (string name in Pack.Unlist(s)) if (name.Length > 0) Pools.Add(name);
        }

        public static string PackAssign()
        {
            return Pack.List(Assign.Select(kv => kv.Key + Pack.Field + kv.Value));
        }

        public static void UnpackAssign(string s)
        {
            Assign.Clear();
            foreach (string rec in Pack.Unlist(s))
            {
                string[] f = rec.Split(Pack.Field);
                if (f.Length == 2 && f[0].Length > 0) Assign[f[0]] = f[1];
            }
        }

        public static string Describe(double mbps)
        {
            if (mbps > 0 && mbps < 1)
            {
                double kbps = mbps * 1000.0;
                if (kbps < 0.01) return "< 0.01 kbps";
                return kbps.ToString(kbps < 10 ? "0.##" : "0", CultureInfo.InvariantCulture) + " kbps";
            }
            return ((float)mbps).Bandwidth();
        }
    }

    /// <summary>Text packing for save data: records split by \u001E, fields by \u001F.</summary>
    public static class Pack
    {
        public const char Record = '\u001E';
        public const char Field = '\u001F';

        public static string List(IEnumerable<string> items)
        {
            return string.Join(Record.ToString(), items.ToArray());
        }

        public static IEnumerable<string> Unlist(string s)
        {
            if (string.IsNullOrEmpty(s)) return new string[0];
            return s.Split(Record);
        }

        public static string D(double v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        public static double D(string s) { double v; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0; }
        public static string L(long v) { return v.ToString(CultureInfo.InvariantCulture); }
        public static long L(string s) { long v; return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }
    }

    /// <summary>In-game date helpers (same conversion as vanilla Utilities.BandwidthFactor).</summary>
    public static class GameClock
    {
        public static int Stamp { get { return SDateTime.Now().ToInt(); } }
        public static int DaysPerMonth { get { return Math.Max(1, GameSettings.DaysPerMonth); } }
        public static double HoursPerMonth { get { return 24.0 * DaysPerMonth; } }
        public static int MinutesPerMonth { get { return DaysPerMonth * 24 * 60; } }
        public static int MonthIndex { get { return Stamp / MinutesPerMonth; } }
        public static double MonthsBetween(int fromStamp, int toStamp) { return (toStamp - fromStamp) / (double)MinutesPerMonth; }
        public static double Year
        {
            get { return SDateTime.BaseYear + Stamp / 12.0 / DaysPerMonth / 24.0 / 60.0; }
        }
    }
}
