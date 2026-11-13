// ISP Business - ISP pools on vanilla server groups (spec sections 3, 4, 5.6).
//
// An ISP pool is a vanilla server group that the player switched to ISP mode in the ISP window.
//  - Its servers give ISP capacity (Config.Servers) instead of hosting capacity.
//  - It can't host products: if items get assigned to it, it goes back to hosting mode.
//  - Failure is vanilla: one broken server sets the group's PowerSum to 0, so the whole pool is down.
//    Its traffic then moves to the other ISP pools that are up, or back to the vanilla ISP.
//  - Each hour, the company's own internet traffic (the vanilla "Internet" bill) is served by its pools
//    up to their capacity. Served traffic is refunded from the vanilla bill and replaced by wholesale
//    transit. Electricity comes from the servers themselves, at the ISP watts per Mbps.
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
        public double Assigned;     // Mbps carried in the last hour
        public double Utilisation;  // Assigned / Capacity
        public double Watts;        // ISP electricity draw at the current load
    }

    public static class IspNetwork
    {
        /// <summary>Names of the vanilla server groups in ISP mode. Saved with the game.</summary>
        public static readonly HashSet<string> Pools = new HashSet<string>();

        /// <summary>Per-pool runtime figures from the last hour (not saved, rebuilt every hour).</summary>
        public static readonly Dictionary<string, PoolState> State = new Dictionary<string, PoolState>();

        // Last-hour and this-month figures for the ISP window.
        public static double LastDemandMbps, LastServedMbps;
        public static double MonthSavings, MonthTransit, LastMonthSavings, LastMonthTransit;
        private static int _statsMonth = int.MinValue;

        /// <summary>Messages to show the player on the next frame (not from inside the hourly tick).</summary>
        public static readonly List<string> PendingNotices = new List<string>();

        public static void Reset()
        {
            Pools.Clear();
            State.Clear();
            LastDemandMbps = LastServedMbps = 0;
            MonthSavings = MonthTransit = LastMonthSavings = LastMonthTransit = 0;
            _statsMonth = int.MinValue;
            PendingNotices.Clear();
        }

        // ---------- pool membership

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
            if (g.Items.Count > 0) return "Move the products and other items off this group first. A server group can either host or be an ISP pool, not both.";
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
                RecalculatePools(LastServedMbps);
                ApplyPower();
                return true;
            }
            Pools.Remove(g.Name);
            State.Remove(g.Name);
            RestoreVanilla(g);
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
            if (oldName == null || newName == null || !Pools.Remove(oldName)) return;
            Pools.Add(newName);
            if (State.TryGetValue(oldName, out PoolState st)) { State.Remove(oldName); st.Name = newName; State[newName] = st; }
        }

        // ---------- hourly tick (Harmony postfix on TimeOfDay.UpdateHour)

        /// <summary>Called once per in-game hour after vanilla added this hour's internet cost.</summary>
        /// <param name="internetCostThisHour">What vanilla added to GameSettings.ServerCost this hour.</param>
        public static void OnHour(double internetCostThisHour)
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null || gs.MyCompany == null) return;

            int dpm = Math.Max(1, GameSettings.DaysPerMonth);
            double hoursPerMonth = 24.0 * dpm;
            double ispRate = Server.GetISPCost();            // vanilla $ per Mbps per month
            double year = GameClock.Year;

            RollMonth();

            // The company's own internet traffic this hour, in Mbps, from what vanilla billed for it.
            double demand = internetCostThisHour > 0 && ispRate > 0 ? internetCostThisHour / (ispRate / hoursPerMonth) : 0;

            double capacity = RecalculatePools(demand);
            double served = Math.Min(demand, capacity);

            if (served > 0)
            {
                double refund = served * ispRate / hoursPerMonth;
                double transit = served * Config.TransitRate(year) / hoursPerMonth;
                // ServerCost is paid by vanilla as the monthly "Internet" bill. Swapping the vanilla rate
                // for transit here keeps one bill line and survives save/load (ServerCost is saved).
                gs.ServerCost = (float)Math.Max(0.0, gs.ServerCost - refund + transit);
                MonthSavings += refund - transit;
                MonthTransit += transit;
            }
            LastDemandMbps = demand;
            LastServedMbps = served;
            ApplyPower();
        }

        private static void RollMonth()
        {
            int month = GameClock.MonthIndex;
            if (month == _statsMonth) return;
            if (_statsMonth != int.MinValue)
            {
                LastMonthSavings = MonthSavings;
                LastMonthTransit = MonthTransit;
            }
            MonthSavings = MonthTransit = 0;
            _statsMonth = month;
        }

        /// <summary>Rebuilds every pool's capacity and spreads <paramref name="demand"/> over the pools that are up.
        /// Returns the total capacity available.</summary>
        public static double RecalculatePools(double demand)
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return 0;

            foreach (string name in Pools.ToList())
            {
                ServerGroup g = gs.GetServerGroup(name);
                if (g == null)
                {
                    // Group gone (all its servers sold or deleted). Vanilla behaviour: nothing left to serve.
                    Pools.Remove(name);
                    State.Remove(name);
                    continue;
                }
                if (g.Items.Count > 0 || g.IsCloud || g.CloudProvider > 0)
                {
                    Pools.Remove(name);
                    State.Remove(name);
                    RestoreVanilla(g);
                    PendingNotices.Add("Server group \"" + name + "\" was given products or other items to host, so it left ISP mode. A server group can either host or be an ISP pool, not both.");
                    continue;
                }
                if (!State.TryGetValue(name, out PoolState st)) { st = new PoolState { Name = name }; State[name] = st; }
                Measure(g, st);
            }

            double total = 0;
            foreach (PoolState st in State.Values) total += st.Capacity;
            double served = Math.Min(demand, total);
            foreach (PoolState st in State.Values)
            {
                st.Assigned = total > 0 ? served * st.Capacity / total : 0;
                st.Utilisation = st.Capacity > 0 ? Math.Min(1.0, st.Assigned / st.Capacity) : 0;
            }
            return total;
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

        private const char Sep = '\u001F';

        public static string PackPools()
        {
            return string.Join(Sep.ToString(), Pools.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        public static void UnpackPools(string s)
        {
            Pools.Clear();
            State.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (string name in s.Split(Sep))
                if (name.Length > 0) Pools.Add(name);
        }

        public static string Describe(double mbps)
        {
            if (mbps >= 1000000) return (mbps / 1000000).ToString("0.##", CultureInfo.InvariantCulture) + " Tbps";
            if (mbps >= 1000) return (mbps / 1000).ToString("0.##", CultureInfo.InvariantCulture) + " Gbps";
            return mbps.ToString("0.##", CultureInfo.InvariantCulture) + " Mbps";
        }
    }

    /// <summary>In-game date helpers (same conversion as vanilla Utilities.BandwidthFactor).</summary>
    public static class GameClock
    {
        public static int Stamp { get { return SDateTime.Now().ToInt(); } }
        public static int MinutesPerMonth { get { return Math.Max(1, GameSettings.DaysPerMonth) * 24 * 60; } }
        public static int MonthIndex { get { return Stamp / MinutesPerMonth; } }
        public static double Year
        {
            get { return SDateTime.BaseYear + Stamp / 12.0 / Math.Max(1, GameSettings.DaysPerMonth) / 24.0 / 60.0; }
        }
    }
}
