// ISP Business - ISP offers and deals shown in the vanilla Deals window.
//
// The mod's own lists (Deals.Offers / Deals.Active) stay the source of truth and are saved in the mod's
// save data. Each one gets a thin vanilla Deal (IspVanillaDeal) so it shows up in Incoming deals and
// Active deals with Accept / Reject / Cancel and a details panel. VanillaDeals.Sync() keeps the two in step.
//
// Safety:
//  - Saves: a postfix on DealWindow.Serialize writes the vanilla deal list without ISP deals, so a save
//    always loads without the mod. ISP deals are rebuilt from mod save data on load.
//  - Ids: ISP deals use their own id range (0xF1xxxxxx, with the player id), so vanilla's multiplayer
//    "deal accepted, cancel it everywhere" message can never hit another player's vanilla deal.
//  - Network: the type id (200) is never registered, so a stray network copy is dropped by vanilla.
//    Offers are never sent through vanilla's AddDeal (which broadcasts); they are inserted locally.
//  - No business guest is spawned (a waiting guest carries the deal and could end up in the save).
//    The offer shows up at the receptionist's time instead, like a guest arriving.
//  - Money: the mod books what each deal earns per delivered Mbps every hour; Payout() hands that over to
//    vanilla's daily deal payment, so the customer pays it and it shows under Deals in the finance bills
//    as "ISP", like hosting deals. Vanilla's daily Performance change is 0; the mod's monthly Performance
//    is mirrored every frame.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ISPBusiness
{
    public sealed class IspVanillaDeal : Deal
    {
        public const byte TypeId = 200;
        public readonly IspDeal Data;
        internal bool Detaching;
        internal bool Restoring;   // being put back into Active deals after a load

        public IspVanillaDeal(Company client, uint id, IspDeal data) : base(client, id, false)
        {
            Data = data;
            Performance = (float)data.Performance;
        }

        public override void RecalculateWorth() { }

        /// <summary>"Offer" column, same unit as hosting deals: income per Mb per hour (monthly price / 24).</summary>
        public override float Worth() { return (float)(Data.Rate / 24.0); }

        public override float Payout() { return Client != null ? Deals.TakeUnpaid(Data) : 0f; }

        public override float ReputationEffect(bool ending) { return ending ? Deals.CancelPenalty(Data) : 0f; }

        public override string ReputationCategory() { return "ISP"; }

        public override string Description()
        {
            if (Data.IsCompany) return "Company-wide ISP for " + Data.CompanyName;
            if (Data.IsMunicipal) return "Municipal ISP for " + Data.CompanyName + ", " + Data.Residents.ToString("N0", CultureInfo.InvariantCulture) + " residents";
            return "ISP service for " + Data.ProductName;
        }

        public override string Title() { return "ISP"; }

        public override void HandleUpdate()
        {
            Performance = (float)Data.Performance;
            PerfDiff = (float)Data.PerfTrend;
        }

        public override void GetDetailedDescription(List<string> vars, List<string> values)
        {
            VanillaDeals.Describe(this, vars, values);
        }

        public override bool StillValid(bool active)
        {
            if (Restoring) return true;
            if (Detaching) return false;
            return active ? Deals.Active.Contains(Data) : Deals.IsOpenOffer(Data);
        }

        public override void Accept(Company company)
        {
            base.Accept(company);
            if (!Detaching && !Restoring) Deals.AcceptFromVanilla(Data);
        }

        public override bool Cancel()
        {
            bool was = Active;
            base.Cancel();
            if (!Detaching) Deals.CancelFromVanilla(Data, was);
            return was;
        }

        public override byte GetTypeID() { return TypeId; }

        public override void WriteTypeData(Stream st) { }
    }

    public static class VanillaDeals
    {
        public const uint IdBase = 0xF1000000u;
        private static readonly Dictionary<IspDeal, IspVanillaDeal> Shown = new Dictionary<IspDeal, IspVanillaDeal>();
        private static DealWindow _window;

        public static bool IsIspId(uint id) { return id >= IdBase; }

        /// <summary>True when the deal is in the vanilla window as an active deal (so vanilla pays it).</summary>
        public static bool IsShown(IspDeal d)
        {
            return Shown.TryGetValue(d, out IspVanillaDeal v) && v.Active && v.Client != null;
        }

        public static void Reset()
        {
            Shown.Clear();
            _window = null;
        }

        private static DealWindow Window()
        {
            return HUD.Instance != null ? HUD.Instance.dealWindow : null;
        }

        /// <summary>Called every frame: puts arrived offers and active deals in the vanilla window and takes
        /// out the ones the mod no longer has.</summary>
        public static void Sync()
        {
            DealWindow w = Window();
            if (w == null) return;
            if (w != _window) { Shown.Clear(); _window = w; }
            GameSettings gs = GameSettings.Instance;
            if (gs == null || gs.MyCompany == null || gs.simulation == null) return;
            int now = GameClock.Stamp;

            // Remove what the mod dropped, or what changed state (offer -> deal).
            foreach (var kv in Shown.ToList())
            {
                IspDeal d = kv.Key;
                IspVanillaDeal v = kv.Value;
                bool keep = d.Active ? (Deals.Active.Contains(d) && v.Active) : (Deals.IsOpenOffer(d) && !v.Active);
                if (keep && Deals.ClientOf(d) != v.Client) keep = false; // product changed hands: show it with the new owner
                if (!keep) Detach(w, d, v);
            }

            if (gs.Difficulty.Deals < 0.5f) return; // the vanilla Deals window is off in this game

            foreach (IspDeal d in Deals.Offers)
            {
                if (Shown.ContainsKey(d) || d.ShowAt > now || !Deals.IsOpenOffer(d)) continue;
                IspVanillaDeal v = Create(d);
                if (v == null) continue;
                Shown[d] = v;
                w.InsertDeal(v);
            }
            foreach (IspDeal d in Deals.Active)
            {
                if (Shown.ContainsKey(d)) continue;
                IspVanillaDeal v = Create(d);
                if (v == null) continue;
                Shown[d] = v;
                // Restore through vanilla's own accept, which adds it to the real Active deals list.
                // (GetActiveDealsPerformance() returns a copy, so adding to it showed nothing.)
                lock (w.AllDeals) { w.AllDeals[v.ID] = v; }
                v.Restoring = true;
                try { w.ActuallyAcceptDeal(v, true); }
                finally { v.Restoring = false; }
            }
        }

        private static IspVanillaDeal Create(IspDeal d)
        {
            Company client = Deals.ClientOf(d);
            if (client == null) return null;
            byte pid = GameSettings.Instance.MyCompany.NetworkPlayerID;
            uint id = IdBase | ((uint)pid << 16) | ((uint)d.Id & 0xFFFFu);
            return new IspVanillaDeal(client, id, d);
        }

        private static void Detach(DealWindow w, IspDeal d, IspVanillaDeal v)
        {
            Shown.Remove(d);
            v.Detaching = true;
            try { w.CancelDeal(v, false, false); }
            catch (Exception e) { Debug.LogError("[ISP Business] Could not remove an ISP deal from the Deals window: " + e.Message); }
            lock (w.AllDeals)
            {
                if (w.AllDeals.TryGetValue(v.ID, out Deal cur) && cur == v) w.AllDeals.Remove(v.ID);
            }
        }

        /// <summary>Takes every ISP deal out of the vanilla window (mod turned off).</summary>
        public static void RemoveAll()
        {
            DealWindow w = Window();
            if (w != null && w == _window)
                foreach (var kv in Shown.ToList()) Detach(w, kv.Key, kv.Value);
            Shown.Clear();
        }

        /// <summary>DealWindow.Serialize postfix: write the vanilla lists without ISP deals.</summary>
        public static void FilterSave(DealWindow w, WriteDictionary dict)
        {
            if (w == null || dict == null) return;
            Dictionary<uint, Deal> all;
            lock (w.AllDeals) { all = w.AllDeals.Where(kv => !(kv.Value is IspVanillaDeal)).ToDictionary(kv => kv.Key, kv => kv.Value); }
            if (all.Count == w.AllDeals.Count) return; // nothing of ours in it
            dict["AllDeals"] = all;
            dict["NewDeals"] = dict.Get("NewDeals", new uint[0]).Where(all.ContainsKey).ToArray();
            dict["ActiveDeals"] = dict.Get("ActiveDeals", new uint[0]).Where(all.ContainsKey).ToArray();
        }

        // ---------- details panel

        public static void Describe(IspVanillaDeal v, List<string> vars, List<string> values)
        {
            IspDeal d = v.Data;
            if (d.IsMunicipal)
            {
                Settlement s = Municipal.Get(d.Settlement);
                double mine = Municipal.MyCoverage(d.Settlement);
                double after = d.Active ? mine : mine + d.Residents;
                Add(vars, values, "Type".Loc(), "Municipal ISP coverage");
                Add(vars, values, "Settlement", d.CompanyName);
                Add(vars, values, "Residents covered by this deal", d.Residents.ToString("N0", CultureInfo.InvariantCulture));
                if (s != null) Add(vars, values, "Settlement population", Municipal.PopulationNow(s.Type).ToString("N0", CultureInfo.InvariantCulture));
                Add(vars, values, "Still available", Municipal.Available(d.Settlement).ToString("N0", CultureInfo.InvariantCulture));
                Add(vars, values, "Internet users", (Municipal.AdoptionNow * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%, " +
                                  (Municipal.UsageNow >= 1000 ? (Municipal.UsageNow / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " GB" : Municipal.UsageNow.ToString("0", CultureInfo.InvariantCulture) + " MB") + " a month each");
                Add(vars, values, "Coverage payment (all your deals here)", ((float)Municipal.CoveragePayment(after)).Currency() + " a month at full service");
            }
            else if (d.IsCompany)
            {
                Add(vars, values, "Type".Loc(), "Company-wide ISP service");
                Add(vars, values, "Covers", "All products of " + d.CompanyName + ", now and later, and its development");
            }
            else
            {
                Add(vars, values, "Type".Loc(), "ISP service");
                Add(vars, values, "Product", d.ProductName);
            }
            Add(vars, values, "IncomeperMbperhour".Loc(), v.Worth().Currency());
            Add(vars, values, "Price per Mbps per month", ((float)d.Rate).Currency());
            Add(vars, values, "Bandwidth needed now", IspNetwork.Describe(d.EstimatedMbps));
            if (d.IsCompany) Add(vars, values, "Of which development", IspNetwork.Describe(d.DevMbps));
            Add(vars, values, d.IsMunicipal ? "Bandwidth income per month now" : "Income per month now", ((float)(d.Rate * d.EstimatedMbps)).Currency());
            if (!d.Active)
            {
                double left = Config.OfferLifetimeMonths - GameClock.MonthsBetween(d.OfferedAt, GameClock.Stamp);
                Add(vars, values, "Offer expires in", Math.Max(0, Math.Ceiling(left)).ToString("0", CultureInfo.InvariantCulture) + " months");
                Add(vars, values, "Served by", IspNetwork.Pools.Count > 0 ? "Your best ISP pool (change it in the ISP window)" : "No ISP pool yet: switch a server group to ISP mode first");
                return;
            }
            IspNetwork.Processes.TryGetValue(d.Key, out IspProcess p);
            double now = p != null && p.Demand > 0 ? p.Delivered / p.Demand : 1.0;
            Add(vars, values, "Delivered now", Pct(now));
            Add(vars, values, "Delivered last month", Pct(d.LastFulfilment));
            Add(vars, values, "Performance", d.Performance.ToString("0.00", CultureInfo.InvariantCulture));
            Add(vars, values, "ISP pool", IspNetwork.GetAssignment(d.Key) ?? "None (not served)");
            Add(vars, values, "Earned so far", ((float)d.TotalRevenue).Currency());
        }

        private static void Add(List<string> vars, List<string> values, string k, string v) { vars.Add(k); values.Add(v); }

        private static string Pct(double f) { return (Math.Max(0, Math.Min(1, f)) * 100).ToString("0", CultureInfo.InvariantCulture) + "%"; }
    }
}
