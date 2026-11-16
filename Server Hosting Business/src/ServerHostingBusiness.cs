// Server Hosting Business - Software Inc. code mod
// Makes server hosting a real business line:
//  - every OS, and antivirus/office software with Network, needs bandwidth that grows by era
//  - multiplayer and MMO bandwidth follow new curves
//  - AI products (new and older ones) ask the player for hosting once they need >= 1 Mbps
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
                harmony + (Profiles.MegaModTermsSeen ? "\nMegaMod Data game features detected." : "");
            WindowManager.AddElementToElement(label.gameObject, parent.gameObject, new Rect(0f, 0f, 420f, 120f), new Rect(0f, 0f, 1f, 0f));
        }
    }

    public class ServerHostingBehaviour : ModBehaviour
    {
        public const float MinOfferMbps = 1f;
        public const double CooldownMonths = 6.0;

        public static bool Patched;
        private const string IspProductType = "Internet Service Provider";
        private static bool? _ispBusinessActive;

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
            _ispBusinessActive = null;
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
                    _ispBusinessActive = null;
                    _lastHour = _lastDay = _lastMonth = int.MinValue;
                    _offersPending = false;
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
                    // Not on the frame a save loads. The offers wait for a clean product snapshot (see below).
                    if (!first) { _offersPending = true; _offerTries = 0; }
                    Prune();
                }
                if (_offersPending)
                {
                    List<SoftwareProduct> products = SnapshotProducts();
                    if (products != null) { _offersPending = false; MonthlyOffers(products); }
                    else if (++_offerTries > MaxOfferTries)
                    {
                        _offersPending = false;
                        Debug.Log("[Server Hosting Business] The market was busy all month; hosting offers are skipped until next month.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Server Hosting Business] " + e);
            }
        }

        // ---------- pricing
        /// <summary>Electricity charged per Mbps per in-game month, whatever servers the player uses.</summary>
        public const double PowerPerMbpsMonth = 7.00;

        /// <summary>Price per Mbps per in-game month: ISP (vanilla) + fixed power + profit.</summary>
        public static double MonthlyPricePerMbps(RateClass cls)
        {
            double isp = Server.GetISPCost();
            double profit = Rates.Profit(cls) * Rates.StarFactor(GameSettings.Instance.MyCompany.BusinessStars);
            return isp + PowerPerMbpsMonth + profit;
        }

        public static void ApplyPrice(ServerDeal deal)
        {
            ProductProfile prof = Profiles.Get(deal.Product);
            RateClass cls = Rates.ClassOf(prof.Kind);
            if (cls == RateClass.Vanilla) return;
            double isp = Server.GetISPCost();
            int stars = GameSettings.Instance.MyCompany.BusinessStars;
            double profit = Rates.Profit(cls) * Rates.StarFactor(stars);
            double monthly = isp + PowerPerMbpsMonth + profit;
            // ServerDeal pays PerPower * 60 * 24 per Mbps per month.
            deal.PerPower = (float)(monthly / 1440.0);
            SaveState.Priced[deal.ID] = Clock.Stamp;
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[Server Hosting Business] Priced hosting for {0} ({1}): ISP ${2:F2} + power ${3:F2} + profit ${4:F2} ({5} stars) = ${6:F2} per Mbps per month, shown in the deal window as ${7:F2} (game dollars).",
                deal.Product.Name, cls, isp, PowerPerMbpsMonth, profit, stars, monthly, deal.PerPower * 60f));
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
        private static bool IspBusinessActive
        {
            get
            {
                if (!_ispBusinessActive.HasValue) _ispBusinessActive = DetectIspBusiness();
                return _ispBusinessActive.Value;
            }
        }

        private static bool DetectIspBusiness()
        {
            try
            {
                foreach (ModController.DLLMod m in ModController.Instance.Mods)
                {
                    if (m == null || !m.Active) continue;
                    if ((m.ItemTitle ?? "").IndexOf("ISP Business", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (m.Meta != null && m.Meta.Name == "ISP Business")) return true;
                }
            }
            catch { return false; }
            return false;
        }

        private bool _offersPending;
        private int _offerTries;
        private const int MaxOfferTries = 2000; // frames

        /// <summary>The game simulates the market on another thread and adds products while it runs
        /// (mostly right at the month change). Reading the product list at that moment throws
        /// "Collection was modified", so take a copy and report null when the market is busy;
        /// the caller tries again on a later frame.</summary>
        private static List<SoftwareProduct> SnapshotProducts()
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null || gs.simulation == null) return new List<SoftwareProduct>();
            for (int i = 0; i < 3; i++)
            {
                try { return gs.simulation.GetAllProducts(false).ToList(); }
                catch (InvalidOperationException) { }
            }
            return null;
        }

        private void MonthlyOffers(List<SoftwareProduct> products)
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
            foreach (SoftwareProduct p in products)
            {
                if (p == null || p.Userbase <= 0 || p.ExternalHostingActive) continue;
                if (p.Type != null && p.Type.Name == IspProductType && IspBusinessActive) continue;
                // ExternalHosting can keep the ID of an offer that already expired; only skip live offers.
                if (p.ExternalHosting != 0) { bool live; lock (dw.AllDeals) live = dw.AllDeals.ContainsKey(p.ExternalHosting); if (live) continue; }
                if (!(p.DevCompany is SimulatedCompany sc)) continue;
                // Subsidiaries ask too when their owner can't host them. AI owners never run servers
                // in the simulation, so only subsidiaries you own (you host those yourself) are skipped.
                if (sc.IsPlayerOwned() || (sc.IsSubsidiary() && sc.OwnerCompany != null && sc.OwnerCompany.Player)) continue;
                if (p.ServerReq <= 0f && Profiles.Get(p).Kind == ProductKind.None) continue;
                float months = SDateTime.GetMonths(p.Release, now);
                if (p.ServerReq > 0f && months < 1f && !sc.IsSubsidiary()) continue;   // vanilla sends the release offer (not for subsidiaries)
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

            foreach (var c in candidates.OrderByDescending(x => x.Value))
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
