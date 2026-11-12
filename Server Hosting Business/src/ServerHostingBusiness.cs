// Server Hosting Business - Software Inc. code mod
// Makes server hosting a real business line:
//  - every OS, and antivirus/office software with Network, needs bandwidth that grows by era
//  - multiplayer and MMO bandwidth follow new curves
//  - AI products (new and older ones) ask the player for hosting once they need >= 100 Mbps
//  - hosting deals pay ISP + power (vanilla math) plus a fixed profit per Mbps
// Needs 0HarmonyLoader for the bandwidth patch. Without it, only offers and pricing run.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ServerHostingBusiness
{
    public class ServerHostingMeta : ModMeta
    {
        public override string Name => "Server Hosting Business";

        public override void ConstructOptionsScreen(RectTransform parent, bool inGame)
        {
            Text label = WindowManager.SpawnLabel();
            string harmony = ServerHostingBehaviour.Patched
                ? "Bandwidth patch active (0HarmonyLoader found)."
                : "0HarmonyLoader not found: only hosting offers and pricing are active.";
            label.text =
                "Hosting profit per Mbps per month (on top of ISP + power): Multiplayer $2.00, MMO $6.00, " +
                "OS / Antivirus / Office $8.55, scaled 80-120% by business stars.\n" +
                "OS, and Antivirus/Office with Network, need bandwidth that grows each year.\n" +
                harmony;
            WindowManager.AddElementToElement(label.gameObject, parent.gameObject, new Rect(0f, 0f, 420f, 96f), new Rect(0f, 0f, 1f, 0f));
        }
    }

    public class ServerHostingBehaviour : ModBehaviour
    {
        public const float MinOfferMbps = 100f;
        public const int MaxOffersPerMonth = 3;
        public const double CooldownMonths = 6.0;

        public static bool Patched;

        private bool _active;
        private GameSettings _seenGame;
        private int _lastHour = int.MinValue, _lastDay = int.MinValue, _lastMonth = int.MinValue;

        // Save data that arrived before the game instance was ready.
        private string _pendingOffers, _pendingCooldown, _pendingPriced;
        private bool _hasPending;

        public override void OnActivate()
        {
            _active = true;
            _seenGame = null;
            if (!Patched && HarmonyBridge.HarmonyAvailable())
            {
                try
                {
                    HarmonyBridge.Patch();
                    Patched = true;
                }
                catch (Exception e)
                {
                    Debug.LogError("[Server Hosting Business] Harmony patch failed: " + e);
                }
            }
            if (!Patched)
                Debug.LogWarning("[Server Hosting Business] 0HarmonyLoader not found. Bandwidth curves are off; offers and pricing still run.");
            Hooks.Enabled = true;
        }

        public override void OnDeactivate()
        {
            _active = false;
            Hooks.Enabled = false;
            if (Patched)
            {
                try { HarmonyBridge.Unpatch(); } catch (Exception e) { Debug.LogError("[Server Hosting Business] Unpatch failed: " + e); }
                Patched = false;
            }
            Profiles.Clear();
        }

        // ---------- save data
        public override void Serialize(WriteDictionary data, GameReader.LoadMode mode)
        {
            data["SHB_Offers"] = SaveState.Pack(SaveState.Offers);
            data["SHB_Cooldown"] = SaveState.Pack(SaveState.Cooldown);
            data["SHB_Priced"] = SaveState.Pack(SaveState.Priced);
        }

        public override void Deserialize(WriteDictionary data, GameReader.LoadMode mode)
        {
            _pendingOffers = data.Get<string>("SHB_Offers", null);
            _pendingCooldown = data.Get<string>("SHB_Cooldown", null);
            _pendingPriced = data.Get<string>("SHB_Priced", null);
            _hasPending = true;
            if (_seenGame != null && _seenGame == GameSettings.Instance) ApplyPending();
        }

        private void ApplyPending()
        {
            if (!_hasPending) return;
            _hasPending = false;
            SaveState.Unpack(_pendingOffers, SaveState.Offers);
            SaveState.Unpack(_pendingCooldown, SaveState.Cooldown);
            SaveState.Unpack(_pendingPriced, SaveState.Priced);
        }

        // ---------- main loop
        private void Update()
        {
            if (!_active) return;
            if (GameSettings.Instance.IsReferenceNull() || TimeOfDay.Instance.IsReferenceNull() || HUD.Instance.IsReferenceNull()) return;
            try
            {
                if (_seenGame != GameSettings.Instance)
                {
                    _seenGame = GameSettings.Instance;
                    SaveState.Reset();
                    Profiles.Clear();
                    _lastHour = _lastDay = _lastMonth = int.MinValue;
                    ApplyPending();
                }

                Clock.Refresh();

                int hourStamp = Clock.Stamp / 60;
                int dayStamp = Clock.Stamp / Clock.MinutesPerDay;
                int monthStamp = Clock.Stamp / Clock.MinutesPerMonth;

                if (hourStamp != _lastHour) { _lastHour = hourStamp; RepriceDeals(); }
                if (dayStamp != _lastDay) { _lastDay = dayStamp; RegisterPlayerProducts(); }
                if (monthStamp != _lastMonth)
                {
                    bool first = _lastMonth == int.MinValue;
                    _lastMonth = monthStamp;
                    if (!first) MonthlyOffers(); // not on the frame a save loads
                    Prune();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Server Hosting Business] " + e);
            }
        }

        // ---------- pricing
        /// <summary>Price per Mbps per in-game month: ISP + power (vanilla math) + profit.</summary>
        public static double MonthlyPricePerMbps(RateClass cls)
        {
            double isp = Server.GetISPCost();
            // Power part kept exactly as vanilla computes it (CheapestServer() / 60 per minute).
            double power = ObjectDatabase.Instance.CheapestServer() * 24.0;
            double profit = Rates.Profit(cls) * Rates.StarFactor(GameSettings.Instance.MyCompany.BusinessStars);
            return isp + power + profit;
        }

        public static void ApplyPrice(ServerDeal deal)
        {
            ProductProfile prof = Profiles.Get(deal.Product);
            RateClass cls = Rates.ClassOf(prof.Kind);
            if (cls == RateClass.Vanilla) return;
            // ServerDeal pays PerPower * 60 * 24 per Mbps per month.
            deal.PerPower = (float)(MonthlyPricePerMbps(cls) / 1440.0);
            SaveState.Priced[deal.ID] = Clock.Stamp;
        }

        private static bool IsClient()
        {
            // SINetworking types are blocked for mods; GameSettings exposes the same information.
            try { GameSettings gs = GameSettings.Instance; return gs != null && gs.IsNetworkMode && !gs.IsHost; } catch (Exception) { return false; }
        }

        private void RepriceDeals()
        {
            if (IsClient()) return;
            DealWindow dw = HUD.Instance.dealWindow;
            if (dw == null) return;
            List<Deal> deals;
            lock (dw.AllDeals) deals = dw.AllDeals.Values.ToList();
            foreach (Deal d in deals)
            {
                // Price is set once, when the offer is made, then locked (bandwidth can still grow).
                if (d is ServerDeal sd && sd.Incoming && sd.Product != null && !SaveState.Priced.ContainsKey(sd.ID))
                {
                    if (sd.Active) SaveState.Priced[sd.ID] = Clock.Stamp; // accepted before the mod saw it: keep its price
                    else ApplyPrice(sd);
                }
            }
        }

        // ---------- player products that only need servers because of this mod
        private void RegisterPlayerProducts()
        {
            if (!Patched) return;
            Company me = GameSettings.Instance.MyCompany;
            if (me == null) return;
            foreach (SoftwareProduct p in me.Products.ToList())
            {
                if (p == null || p.ServerReq > 0f) continue; // vanilla already handles these
                if (Profiles.Get(p).Kind == ProductKind.None) continue;
                if (p.ExternalHostingActive || p.DevCompany == null || !p.DevCompany.IsLocalPlayer || p.DevCompany.ID != p.InventorID) continue;
                if (MarketSimulation.Active != null && p.Type == MarketSimulation.Active.DigitalDistSoft) continue;
                if (p.GetLoadRequirement() * Clock.Factor < 1.0) continue; // vanilla drops products under 1 Mbps
                if (IsRegistered(p)) continue;
                GameSettings.Instance.RegisterWithServer(p.Server, p);
            }
        }

        private static bool IsRegistered(SoftwareProduct p)
        {
            GameSettings gs = GameSettings.Instance;
            if (gs.UnsupportedServerItems.Contains(p)) return true;
            ServerGroup g = gs.GetServerGroup(p.Server);
            return g != null && g.Items.Contains(p);
        }

        // ---------- AI hosting offers
        private void MonthlyOffers()
        {
            if (IsClient()) return;
            GameSettings gs = GameSettings.Instance;
            if (gs.simulation == null) return;
            if (gs.Difficulty.Deals < 0.5f) { Debug.Log("[Server Hosting Business] Deals are turned off in this game's difficulty settings, so no hosting offers are sent."); return; }
            DealWindow dw = HUD.Instance.dealWindow;
            if (dw == null) return;
            int needServers = 0, bigEnough = 0, sent = 0; float biggest = 0f;

            SDateTime now = SDateTime.Now();
            var candidates = new List<KeyValuePair<SoftwareProduct, float>>();
            foreach (SoftwareProduct p in gs.simulation.GetAllProducts(false).ToList())
            {
                if (p == null || p.Userbase <= 0 || p.ExternalHostingActive) continue;
                // ExternalHosting can keep the ID of an offer that already expired; only skip live offers.
                if (p.ExternalHosting != 0) { bool live; lock (dw.AllDeals) live = dw.AllDeals.ContainsKey(p.ExternalHosting); if (live) continue; }
                if (!(p.DevCompany is SimulatedCompany sc) || sc.IsSubsidiary() || sc.IsPlayerOwned()) continue;
                if (p.ServerReq <= 0f && Profiles.Get(p).Kind == ProductKind.None) continue;
                float months = SDateTime.GetMonths(p.Release, now);
                if (p.ServerReq > 0f && months < 1f) continue;   // vanilla sends the release offer
                if (!Patched && months > 24f) continue;          // without the patch old offers would be rejected
                if (SaveState.Cooldown.TryGetValue(p.ID, out int last) &&
                    (Clock.Stamp - last) / (double)Clock.MinutesPerMonth < CooldownMonths) continue;
                float mbps = p.GetLoadRequirement() * (float)Clock.Factor;
                needServers++;
                if (mbps > biggest) biggest = mbps;
                if (mbps < MinOfferMbps) continue;
                bigEnough++;
                candidates.Add(new KeyValuePair<SoftwareProduct, float>(p, mbps * UnityEngine.Random.Range(0.7f, 1f)));
            }

            foreach (var c in candidates.OrderByDescending(x => x.Value).Take(MaxOffersPerMonth))
            {
                SDateTime? t = DealWindow.FindReceptionTime();
                if (!t.HasValue) { Debug.Log("[Server Hosting Business] No receptionist found, offers skipped."); break; }
                var deal = new ServerDeal(c.Key);
                ApplyPrice(deal);
                SaveState.Offers[deal.ID] = Clock.Stamp;
                SaveState.Cooldown[c.Key.ID] = Clock.Stamp;
                dw.AddDeal(deal, t, toEveryone: true);
                sent++;
            }
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[Server Hosting Business] {0}: {1} unhosted AI products need servers (largest {2:F1} Mbps), {3} need {4}+ Mbps, {5} offers sent.",
                now.ToCompactString(), needServers, biggest, bigEnough, MinOfferMbps, sent));
        }

        private void Prune()
        {
            double maxAge = Math.Max(CooldownMonths, Hooks.OfferLifetimeMonths) + 1;
            foreach (uint k in SaveState.Offers.Where(kv => (Clock.Stamp - kv.Value) / (double)Clock.MinutesPerMonth > Hooks.OfferLifetimeMonths + 1).Select(kv => kv.Key).ToList())
                SaveState.Offers.Remove(k);
            DealWindow dw = HUD.Instance.dealWindow;
            if (dw != null)
            {
                List<uint> gone;
                lock (dw.AllDeals) gone = SaveState.Priced.Where(kv => !dw.AllDeals.ContainsKey(kv.Key) && (Clock.Stamp - kv.Value) / (double)Clock.MinutesPerMonth > 1).Select(kv => kv.Key).ToList();
                foreach (uint k in gone) SaveState.Priced.Remove(k);
            }
            foreach (uint k in SaveState.Cooldown.Where(kv => (Clock.Stamp - kv.Value) / (double)Clock.MinutesPerMonth > maxAge).Select(kv => kv.Key).ToList())
                SaveState.Cooldown.Remove(k);
        }
    }
}
