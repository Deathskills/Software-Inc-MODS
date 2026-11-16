// ISP Business - the ISP window (spec 18). Opened from a button on the bottom bar.
//  - Your ISP pools only, in the same table style as the vanilla Servers window (status, colour, name,
//    bandwidth, servers, load, processes, electricity, fallback, select fallback, view), with a
//    "Back to hosting" button. "Add server group" turns a hosting group into a pool. Below it, the ISP
//    processes on the selected pool, with "Move selected processes" like vanilla.
//  - The vanilla Servers window gets an "ISP mode" button column too (EnsureServerWindowColumn).
//  - Players tab: your ISP offer to other players, their offers to you, your player customers and your
//    own agreement (Agreements.cs). Multiplayer only.
//  - ISP offers and deals are in the vanilla Deals window (VanillaDeals.cs); the "Deals window" button opens it.
//    Move a deal to another pool from the process list here.
// Vanilla columns are reused by key from GUIListView.ColumnDefinitions, so they behave like vanilla.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ISPBusiness
{
    public static class IspWindow
    {
        private static GUIWindow _window;
        private static RectTransform _netPanel, _townPanel, _playerPanel;
        private static Text _summary, _procLabel, _townSummary, _playerSummary, _openText;
        private static GUIListView _groups, _procs, _towns, _offers, _customers;
        private const int TabPools = 0, TabTowns = 1, TabPlayers = 2;
        private static int _tab;
        private static string _playersKey;
        private static MainBottomButton _button;
        private static float _nextRefresh;
        private static string _groupsKey, _procsKey, _summaryKey, _townKey;
        private static readonly List<KeyValuePair<GUIListView, string[]>> _pendingColumns = new List<KeyValuePair<GUIListView, string[]>>();

        // ---------- bottom-bar button

        public static void EnsureButton(Sprite icon)
        {
            if (_button != null || HUD.Instance == null) return;
            try
            {
                _button = HUD.Instance.AddBottomButton("ISP", "ISP Network", "Your ISP pools and ISP processes. ISP offers and deals are in the Deals window.", icon);
                Button b = _button.GetComponent<Button>() ?? _button.GetComponentInChildren<Button>();
                if (b != null) b.onClick.AddListener(Toggle);
            }
            catch (Exception e)
            {
                Debug.LogError("[ISP Business] Could not add the ISP button: " + e);
            }
        }

        /// <summary>Drops references when the game scene goes away (the objects are destroyed with it).</summary>
        public static void Forget()
        {
            _window = null;
            _netPanel = _townPanel = _playerPanel = null;
            _summary = _procLabel = _townSummary = _playerSummary = _openText = null;
            _groups = _procs = _towns = _offers = _customers = null;
            _playersKey = null;
            _serverList = null;
            _button = null;
            _groupsKey = _procsKey = _summaryKey = _townKey = null;
            _pendingColumns.Clear();
        }

        public static bool IsOpen { get { return _window != null && _window.gameObject.activeSelf; } }

        public static void Toggle()
        {
            try
            {
                if (IsOpen) { _window.Close(); return; }
                if (_window == null) Build();
                _groupsKey = _procsKey = _summaryKey = _townKey = _playersKey = null;
                _window.Show();
                Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError("[ISP Business] ISP window error: " + e);
            }
        }

        // ---------- layout helpers

        /// <summary>Stretches an element inside its parent with margins; anchors are fractions from the top-left.</summary>
        private static void Place(GameObject el, GameObject parent, float l, float t, float r, float b,
                                  float ax0 = 0f, float ay0 = 0f, float ax1 = 1f, float ay1 = 1f)
        {
            WindowManager.AddElementToElement(el, parent, new Rect(l, t, -r - l, -b - t), new Rect(ax0, ay0, ax1 - ax0, ay1 - ay0));
        }

        /// <summary>Fixed-height element at the top of an anchor band.</summary>
        private static void PlaceTop(GameObject el, GameObject parent, float l, float t, float r, float h, float ay = 0f)
        {
            WindowManager.AddElementToElement(el, parent, new Rect(l, t, -r - l, h), new Rect(0f, ay, 1f, 0f));
        }

        private static Button MakeButton(string text, GameObject parent, Rect pos, Rect anchors, Action onClick)
        {
            Button btn = WindowManager.SpawnButton();
            Text t = btn.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
            btn.onClick.AddListener(() => onClick());
            WindowManager.AddElementToElement(btn.gameObject, parent, pos, anchors);
            return btn;
        }

        private static Text MakeLabel(GameObject parent)
        {
            Text label = WindowManager.SpawnLabel();
            label.supportRichText = true;
            label.alignment = TextAnchor.UpperLeft;
            label.transform.SetParent(parent.transform, false);
            return label;
        }

        private static GUIListView MakeList(GameObject parent, Dictionary<string, GUIListView.ColumnDef> defs, string[] keys)
        {
            GUIListView list = WindowManager.SpawnList();
            foreach (var kv in defs) list.ColumnDefinitions[kv.Key] = kv.Value;
            list.IgnoreTranslation = true;
            list.Columns = keys;   // read by the list's Start(), which hasn't run yet
            list.transform.SetParent(parent.transform, false);
            _pendingColumns.Add(new KeyValuePair<GUIListView, string[]>(list, keys));
            return list;
        }

        /// <summary>If a list started before its Columns were set, add the columns by hand.</summary>
        private static void EnsureColumns()
        {
            if (_pendingColumns.Count == 0) return;
            foreach (var kv in _pendingColumns.ToList())
            {
                GUIListView list = kv.Key;
                if (list == null) { _pendingColumns.Remove(kv); continue; }
                if (list.GUIColumns == null) continue; // not started yet
                // Columns were set before the list's Start(), which adds them. Adding them here too
                // duplicated every column (GUIColumns is an empty list, not null, before Start).
                _pendingColumns.Remove(kv);
            }
        }

        // ---------- build

        private static void Build()
        {
            _window = WindowManager.SpawnWindow();
            _window.NonLocTitle = "ISP Network";
            RectTransform rt = _window.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(1180f, 640f);
            GameObject main = _window.MainPanel;

            MakeButton("ISP pools", main, new Rect(8f, 6f, 130f, 28f), new Rect(0f, 0f, 0f, 0f), () => ShowTab(TabPools));
            MakeButton("Towns", main, new Rect(142f, 6f, 130f, 28f), new Rect(0f, 0f, 0f, 0f), () => ShowTab(TabTowns));
            MakeButton("Players", main, new Rect(276f, 6f, 130f, 28f), new Rect(0f, 0f, 0f, 0f), () => ShowTab(TabPlayers));
            MakeButton("Add server group", main, new Rect(430f, 6f, 160f, 28f), new Rect(0f, 0f, 0f, 0f), AddGroup);
            MakeButton("Deals window", main, new Rect(594f, 6f, 160f, 28f), new Rect(0f, 0f, 0f, 0f), OpenDeals);

            _netPanel = WindowManager.SpawnPanel();
            Place(_netPanel.gameObject, main, 0f, 50f, 0f, 0f);
            BuildNetwork(_netPanel.gameObject);
            _townPanel = WindowManager.SpawnPanel();
            Place(_townPanel.gameObject, main, 0f, 50f, 0f, 0f);
            BuildTowns(_townPanel.gameObject);
            _playerPanel = WindowManager.SpawnPanel();
            Place(_playerPanel.gameObject, main, 0f, 50f, 0f, 0f);
            BuildPlayers(_playerPanel.gameObject);
            ShowTab(_tab);
        }

        private static void ShowTab(int tab)
        {
            _tab = tab;
            if (_netPanel != null) _netPanel.gameObject.SetActive(tab == TabPools);
            if (_townPanel != null) _townPanel.gameObject.SetActive(tab == TabTowns);
            if (_playerPanel != null) _playerPanel.gameObject.SetActive(tab == TabPlayers);
            _groupsKey = _procsKey = _summaryKey = _townKey = _playersKey = null;
            if (_window != null) Refresh();
        }

        private static void BuildTowns(GameObject p)
        {
            _townSummary = MakeLabel(p);
            PlaceTop(_townSummary.gameObject, p, 8f, 0f, 8f, 66f);
            Func<Settlement, double> pop = s => Municipal.PopulationNow(s.Type);
            Func<Settlement, double> mine = s => Municipal.MyCoverage(s.Id);
            Func<Settlement, double> demand = s => Municipal.DemandNow(mine(s));
            var defs = new Dictionary<string, GUIListView.ColumnDef>
            {
                { "IspTName", new GUIListView.ColumnDefinition<Settlement>("Settlement", (Settlement s) => s.Name, true, 150f) },
                { "IspTType", new GUIListView.ColumnDefinition<Settlement>("Type", (Settlement s) => s.TypeName, true, 70f) },
                { "IspTPop", new GUIListView.ColumnDefinition<Settlement>("Population", (Settlement s) => (object)Num(pop(s)), (Settlement s) => (float)pop(s), true, 110f) },
                { "IspTMine", new GUIListView.ColumnDefinition<Settlement>("You cover", (Settlement s) => (object)Num(mine(s)), (Settlement s) => (float)mine(s), true, 100f) },
                { "IspTOthers", new GUIListView.ColumnDefinition<Settlement>("Other players", (Settlement s) => (object)Num(Municipal.OthersCoverage(s.Id)), (Settlement s) => (float)Municipal.OthersCoverage(s.Id), true, 100f) },
                { "IspTAvail", new GUIListView.ColumnDefinition<Settlement>("Available", (Settlement s) => (object)Num(Municipal.Available(s.Id)), (Settlement s) => (float)Municipal.Available(s.Id), true, 100f) },
                { "IspTDemand", new GUIListView.ColumnDefinition<Settlement>("Your bandwidth", (Settlement s) => (object)IspNetwork.Describe(demand(s)), (Settlement s) => (float)demand(s), true, 110f) },
                { "IspTPay", new GUIListView.ColumnDefinition<Settlement>("Coverage pay", (Settlement s) => (float)Municipal.CoveragePayment(mine(s)), true, 100f) },
                { "IspTDeals", new GUIListView.ColumnDefinition<Settlement>("Deals", (Settlement s) => Deals.Active.Count(d => d.IsMunicipal && d.Settlement == s.Id), true, 60f) },
                { "IspTService", new GUIListView.ColumnDefinition<Settlement>("Last month", (Settlement s) => mine(s) > 0 ? (float)Municipal.State(s.Id).LastFulfilment : 0f, true, 90f, currency: false, includeDecimal: true, progressBar: true, withTotal: false) },
                { "IspTStatus", new GUIListView.ColumnDefinition<Settlement>("Status", (Settlement s) => TownStatus(s), true, 160f) },
                { "IspTMerge", new GUIListView.ColumnDefinition<Settlement>("Consolidate", (Settlement s) => ConsolidateTown(s), 100f) },
            };
            _towns = MakeList(p, defs, new[] { "IspTName", "IspTType", "IspTPop", "IspTMine", "IspTOthers", "IspTAvail", "IspTDemand", "IspTPay", "IspTDeals", "IspTService", "IspTStatus", "IspTMerge" });
            Place(_towns.gameObject, p, 8f, 70f, 8f, 8f);
        }

        private static string Num(double v) { return Math.Floor(v).ToString("N0", CultureInfo.InvariantCulture); }

        private static string TownStatus(Settlement s)
        {
            if (Municipal.MyCoverage(s.Id) <= 0) return Deals.Offers.Any(o => o.IsMunicipal && o.Settlement == s.Id) ? "Offer in the Deals window" : "Not served";
            SettlementState st = Municipal.State(s.Id);
            if (st.SuitFiled) return "Lawsuit filed";
            if (st.NoticeSent) return "Breach notice";
            if (st.LowMonths90 > 0) return "Under 90% for " + st.LowMonths90 + " month(s)";
            return "OK";
        }

        private static void ConsolidateTown(Settlement s)
        {
            bool ok = Municipal.Consolidate(s.Id, out string message);
            WindowManager.SpawnDialog(message, false, ok ? DialogWindow.DialogType.Information : DialogWindow.DialogType.Warning);
            Refresh();
        }

        private static void RefreshTowns()
        {
            string townKey = IspNetwork.StatsVersion + "|" + Deals.Active.Count(d => d.IsMunicipal) + "|" + Deals.Active.Where(d => d.IsMunicipal).Sum(d => d.Residents);
            bool rebuild = townKey != _townKey;
            _townKey = townKey;
            double covered = Municipal.Settlements.Sum(s => Municipal.MyCoverage(s.Id));
            double pay = Municipal.Settlements.Sum(s => Municipal.CoveragePayment(Municipal.MyCoverage(s.Id)));
            var sb = new StringBuilder();
            sb.Append("<b>Residents you cover:</b> ").Append(Num(covered))
              .Append("   <b>Coverage pay at full service:</b> ").Append(Money(pay)).Append(" a month")
              .Append("   <b>Internet users:</b> ").Append((Municipal.AdoptionNow * 100).ToString("0.0", CultureInfo.InvariantCulture)).Append("% of residents")
              .Append("   <b>Use per user:</b> ").Append(Municipal.UsageNow >= 1000 ? (Municipal.UsageNow / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " GB" : Municipal.UsageNow.ToString("0.#", CultureInfo.InvariantCulture) + " MB").Append(" a month\n");
            Settlement home = Municipal.Settlements.FirstOrDefault(Municipal.OnMap);
            if (home != null) sb.Append("<b>Your map:</b> ").Append(home.TypeName).Append(", so offers come from ").Append(home.Name).Append('\n');
            sb.Append("<i>Town offers come through the Deals window: a small first deal, then bigger ones at 3, 6, 9 and 12 months if your service there stays above 90%. ")
              .Append("Coverage pay is only paid for the traffic you actually deliver. Under 90% for 3 months brings a breach notice; under 75% for 5 months, or a month with no service, a lawsuit. ")
              .Append("Consolidate merges your deals in a town into one.</i>");
            if (rebuild) _townSummary.text = sb.ToString();
            List<Settlement> shown = Municipal.Shown().ToList();
            if (_towns.Items.Count != shown.Count || shown.Any(s => !_towns.Items.Contains(s)))
            {
                _towns.Items.Clear();
                _towns.Items.AddRange(shown.Cast<object>());
            }
        }

        // ---------- Players tab (agreements between players)

        private static void BuildPlayers(GameObject p)
        {
            _playerSummary = MakeLabel(p);
            PlaceTop(_playerSummary.gameObject, p, 8f, 0f, 8f, 88f);

            Button open = MakeButton("Open your offer", p, new Rect(8f, 92f, 170f, 28f), new Rect(0f, 0f, 0f, 0f), ToggleOffer);
            _openText = open.GetComponentInChildren<Text>();
            MakeButton("Standard price", p, new Rect(182f, 92f, 150f, 28f), new Rect(0f, 0f, 0f, 0f), () => PickPrice(Agreements.Standard));
            MakeButton("Premium price", p, new Rect(336f, 92f, 150f, 28f), new Rect(0f, 0f, 0f, 0f), () => PickPrice(Agreements.Premium));
            MakeButton("Pool for players", p, new Rect(490f, 92f, 150f, 28f), new Rect(0f, 0f, 0f, 0f), PickOfferPool);
            MakeButton("Leave your agreement", p, new Rect(680f, 92f, 190f, 28f), new Rect(0f, 0f, 0f, 0f), LeaveAgreement);

            Text offersLabel = MakeLabel(p);
            offersLabel.text = "<b>Offers from other players</b> (prices per Mbps a month; connect to buy your internet from them):";
            PlaceTop(offersLabel.gameObject, p, 8f, 126f, 8f, 22f);
            var defs = new Dictionary<string, GUIListView.ColumnDef>
            {
                { "IspOName", new GUIListView.ColumnDefinition<PlayerOffer>("Provider", (PlayerOffer o) => o.Name ?? "", true, 180f) },
                { "IspOStd", new GUIListView.ColumnDefinition<PlayerOffer>("Standard", (PlayerOffer o) => (float)Agreements.Dollars(o.Standard), true, 100f) },
                { "IspOPrem", new GUIListView.ColumnDefinition<PlayerOffer>("Premium", (PlayerOffer o) => (float)Agreements.Dollars(o.Premium), true, 100f) },
                { "IspOVan", new GUIListView.ColumnDefinition<PlayerOffer>("Vanilla ISP", (PlayerOffer o) => (float)Server.GetISPCost(), true, 100f) },
                { "IspOSpare", new GUIListView.ColumnDefinition<PlayerOffer>("Spare capacity", (PlayerOffer o) => (object)IspNetwork.Describe(o.Spare), (PlayerOffer o) => (float)o.Spare, true, 120f) },
                { "IspOStatus", new GUIListView.ColumnDefinition<PlayerOffer>("Status", (PlayerOffer o) => OfferStatus(o), true, 200f) },
                { "IspOConStd", new GUIListView.ColumnDefinition<PlayerOffer>("Connect standard", (PlayerOffer o) => ConnectTo(o, Agreements.Standard), 140f) },
                { "IspOConPrem", new GUIListView.ColumnDefinition<PlayerOffer>("Connect premium", (PlayerOffer o) => ConnectTo(o, Agreements.Premium), 140f) },
            };
            _offers = MakeList(p, defs, new[] { "IspOName", "IspOStd", "IspOPrem", "IspOVan", "IspOSpare", "IspOStatus", "IspOConStd", "IspOConPrem" });
            Place(_offers.gameObject, p, 8f, 150f, 8f, 4f, 0f, 0f, 1f, 0.55f);

            Text custLabel = MakeLabel(p);
            custLabel.text = "<b>Your player customers</b> (they are ISP processes on your pools, like deals):";
            PlaceTop(custLabel.gameObject, p, 8f, 4f, 8f, 22f, 0.55f);
            var cdefs = new Dictionary<string, GUIListView.ColumnDef>
            {
                { "IspCName", new GUIListView.ColumnDefinition<PlayerCustomer>("Customer", (PlayerCustomer c) => c.Name ?? "", true, 180f) },
                { "IspCTier", new GUIListView.ColumnDefinition<PlayerCustomer>("Type", (PlayerCustomer c) => Agreements.TierName(c.Tier), true, 90f) },
                { "IspCPrice", new GUIListView.ColumnDefinition<PlayerCustomer>("Price", (PlayerCustomer c) => (float)Agreements.Dollars(c.Price), true, 100f) },
                { "IspCUse", new GUIListView.ColumnDefinition<PlayerCustomer>("Bandwidth", (PlayerCustomer c) => (object)IspNetwork.Describe(c.Usage), (PlayerCustomer c) => (float)c.Usage, true, 110f) },
                { "IspCLast", new GUIListView.ColumnDefinition<PlayerCustomer>("Delivered last month", (PlayerCustomer c) => (float)c.LastFulfilment, true, 150f, currency: false, includeDecimal: true, progressBar: true, withTotal: false) },
                { "IspCEarned", new GUIListView.ColumnDefinition<PlayerCustomer>("Earned last month", (PlayerCustomer c) => (float)c.LastEarned, true, 130f) },
                { "IspCPool", new GUIListView.ColumnDefinition<PlayerCustomer>("Pool", (PlayerCustomer c) => IspNetwork.GetAssignment(c.Key) ?? "None", true, 140f) },
                { "IspCDrop", new GUIListView.ColumnDefinition<PlayerCustomer>("Drop", (PlayerCustomer c) => DropCustomer(c), 90f) },
            };
            _customers = MakeList(p, cdefs, new[] { "IspCName", "IspCTier", "IspCPrice", "IspCUse", "IspCLast", "IspCEarned", "IspCPool", "IspCDrop" });
            Place(_customers.gameObject, p, 8f, 30f, 8f, 8f, 0f, 0.55f, 1f, 1f);
        }

        private static string OfferStatus(PlayerOffer o)
        {
            PlayerAgreement a = Agreements.Current;
            if (a != null && a.Provider == o.Player) return "Your ISP (" + Agreements.TierName(a.Tier).ToLowerInvariant() + ")";
            if (Agreements.Pending && Agreements.PendingProvider == o.Player) return "Waiting for their answer";
            if (Agreements.Customers.ContainsKey(o.Player)) return "Your customer";
            return Net.PeerCompatible(o.Player) ? "Available" : "Different mod version";
        }

        private static void RefreshPlayers()
        {
            GameSettings gs = GameSettings.Instance;
            if (_openText != null) _openText.text = Agreements.OfferOpen ? "Close your offer" : "Open your offer";
            string key = Agreements.Version + "|" + IspNetwork.StatsVersion + "|" + Agreements.Offers.Count + "|" + Agreements.Customers.Count + "|" +
                         (gs != null && gs.IsNetworkMode);
            if (key == _playersKey) return;
            _playersKey = key;

            var sb = new StringBuilder();
            if (gs == null || !gs.IsNetworkMode)
            {
                sb.Append("<b>Agreements between players only work in multiplayer games.</b> In a multiplayer game, open your offer here to sell ISP service to other players, or connect to another player's offer to buy yours from them.\n");
            }
            sb.Append("<b>Your offer:</b> ").Append(Agreements.OfferOpen ? "open" : "closed")
              .Append("   <b>Standard:</b> ").Append(Money(Agreements.Dollars(Agreements.StandardPrice)))
              .Append("   <b>Premium:</b> ").Append(Money(Agreements.Dollars(Agreements.PremiumPrice)))
              .Append(" per Mbps a month (vanilla ISP: ").Append(Money(Server.GetISPCost())).Append(")")
              .Append("   <b>Pool:</b> ").Append(Agreements.OfferPool ?? "most spare capacity").Append('\n');
            PlayerAgreement a = Agreements.Current;
            if (a != null)
            {
                sb.Append("<b>Your ISP:</b> ").Append(a.Name).Append(", ").Append(Agreements.TierName(a.Tier).ToLowerInvariant())
                  .Append(", ").Append(Money(Agreements.Dollars(a.Price))).Append(" per Mbps a month. ");
                if (a.LastCost > 0 || a.LastSaved != 0)
                    sb.Append("<b>Last month:</b> delivered ").Append((a.LastFulfilment * 100).ToString("0", CultureInfo.InvariantCulture)).Append("%, paid ")
                      .Append(Money(a.LastCost)).Append(", saved ").Append(Money(a.LastSaved)).Append(" against the vanilla ISP. ");
                sb.Append("Leaving now costs ").Append(Money(Agreements.LeaveFee())).Append(".\n");
            }
            else sb.Append("<b>Your ISP:</b> the vanilla ISP (and your own pools).\n");
            sb.Append("<i>Your own pools carry your internet first; the rest goes to your provider. What they don't deliver stays on the vanilla bill. ")
              .Append("Standard: leaving costs ").Append((Config.ReconnectFeeMonths).ToString("0.#", CultureInfo.InvariantCulture)).Append(" month of service (at least ").Append(Money(Config.ReconnectFeeMin)).Append("). ")
              .Append("Premium: if a day's delivery falls under ").Append((Config.PremiumMinService * 100).ToString("0", CultureInfo.InvariantCulture))
              .Append("%, it ends for free. If either player leaves the game, it ends for free.</i>");
            _playerSummary.text = sb.ToString();

            List<PlayerOffer> offers = Agreements.Offers.Values.OrderBy(o => o.Name, StringComparer.Ordinal).ToList();
            _offers.Items.Clear();
            _offers.Items.AddRange(offers.Cast<object>());
            List<PlayerCustomer> customers = Agreements.Customers.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
            _customers.Items.Clear();
            _customers.Items.AddRange(customers.Cast<object>());
        }

        private static void ToggleOffer()
        {
            if (!Agreements.SetOfferOpen(!Agreements.OfferOpen, out string error))
                WindowManager.SpawnDialog(error, false, DialogWindow.DialogType.Warning);
            _playersKey = null;
            Refresh();
        }

        private static void PickPrice(byte tier)
        {
            double vanilla = Server.GetISPCost();
            double[] steps = Config.PlayerPriceSteps;
            var labels = steps.Select(f => Money(f * vanilla) + " per Mbps a month (" + (f * 100).ToString("0", CultureInfo.InvariantCulture) + "% of vanilla)");
            WindowManager.Instance.MultiWindow.Show(tier == Agreements.Premium ? "Premium price" : "Standard price", labels, (int i) =>
            {
                if (i < 0 || i >= steps.Length) return;
                if (tier == Agreements.Premium) Agreements.SetPrices(Agreements.StandardPrice, steps[i]);
                else Agreements.SetPrices(steps[i], Agreements.PremiumPrice);
                _playersKey = null;
                Refresh();
            }, false);
        }

        private static void PickOfferPool()
        {
            List<string> pools = IspNetwork.Pools.OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (pools.Count == 0)
            {
                WindowManager.SpawnDialog("You have no ISP pools. Switch a server group to ISP mode first.", false, DialogWindow.DialogType.Warning);
                return;
            }
            // "None" means: the pool with the most spare capacity.
            WindowManager.Instance.MultiWindow.Show("Pool for player customers (None: most spare capacity)", pools, (int i) =>
            {
                Agreements.SetPool(i < 0 || i >= pools.Count ? null : pools[i]);
                _playersKey = _procsKey = null;
                Refresh();
            }, true);
        }

        private static void ConnectTo(PlayerOffer o, byte tier)
        {
            if (o == null) return;
            if (!Agreements.Connect(o, tier, out string error))
                WindowManager.SpawnDialog(error, false, DialogWindow.DialogType.Warning);
            _playersKey = null;
            Refresh();
        }

        private static void LeaveAgreement()
        {
            PlayerAgreement a = Agreements.Current;
            if (a == null)
            {
                WindowManager.SpawnDialog("You have no ISP agreement with another player.", false, DialogWindow.DialogType.Information);
                return;
            }
            double fee = Agreements.LeaveFee();
            WindowManager.Instance.MultiWindow.Show("Leave " + a.Name + "'s ISP?", new[] { "Leave and pay a fee of " + Money(fee), "Stay connected" }, (int i) =>
            {
                if (i == 0 && Agreements.Current == a) Agreements.Leave();
                _playersKey = null;
                Refresh();
            }, false);
        }

        private static void DropCustomer(PlayerCustomer c)
        {
            if (c == null) return;
            WindowManager.Instance.MultiWindow.Show("Stop serving " + c.Name + "?", new[] { "Drop them (no fee for them, you lose some reputation)", "Keep serving them" }, (int i) =>
            {
                if (i == 0) Agreements.Drop(c);
                _playersKey = _procsKey = null;
                Refresh();
            }, false);
        }

        private static void OpenDeals()
        {
            DealWindow w = HUD.Instance != null ? HUD.Instance.dealWindow : null;
            if (w == null) return;
            if (GameSettings.Instance.Difficulty.Deals < 0.5f)
            {
                WindowManager.SpawnDialog("Deals are turned off in this game's difficulty settings, so there are no ISP offers.", false, DialogWindow.DialogType.Warning);
                return;
            }
            if (w.Window != null && !w.Window.Shown) w.Toggle();
        }

        private static void BuildNetwork(GameObject p)
        {
            _summary = MakeLabel(p);
            PlaceTop(_summary.gameObject, p, 8f, 0f, 8f, 88f);

            var gdefs = new Dictionary<string, GUIListView.ColumnDef>
            {
                { "IspStatus", new GUIListView.ColumnDefinition<ServerGroup>("Status", (ServerGroup g) => (object)StatusLabel(g), (ServerGroup g) => (float)StatusRank(g), true, 50f, null, null, GUIColumn.ColumnType.WarningIcon) },
                { "IspMode", new GUIListView.ColumnDefinition<ServerGroup>("Mode", (ServerGroup g) => IspNetwork.IsPool(g) ? "ISP" : "Hosting", true, 70f) },
                { "IspBandwidth", new GUIListView.ColumnDefinition<ServerGroup>("Bandwidth", (ServerGroup g) => (object)IspNetwork.Describe(Bandwidth(g)), (ServerGroup g) => (float)Bandwidth(g), true, 95f) },
                { "IspLoad", new GUIListView.ColumnDefinition<ServerGroup>("Load", (ServerGroup g) => LoadOf(g), true, 80f, currency: false, includeDecimal: true, progressBar: true, withTotal: false) },
                { "IspProcs", new GUIListView.ColumnDefinition<ServerGroup>("Processes", (ServerGroup g) => ProcessCount(g), true, 85f) },
                { "IspSwitch", new GUIListView.ColumnDefinition<ServerGroup>("Back to hosting", (ServerGroup g) => TogglePool(g.Name), 120f) },
            };
            _groups = MakeList(p, gdefs, new[] { "IspStatus", "SrvColor", "SrvName", "IspBandwidth", "SrvServers", "IspLoad", "IspProcs", "SrvWatt", "SrvFallback", "SrvSelectFallback", "SrvView", "IspSwitch" });
            Place(_groups.gameObject, p, 8f, 92f, 8f, 4f, 0f, 0f, 1f, 0.6f);
            _groups.OnSelectChange = (bool direct) => { _procsKey = null; RefreshProcesses(); };

            _procLabel = MakeLabel(p);
            PlaceTop(_procLabel.gameObject, p, 8f, 4f, 8f, 22f, 0.6f);

            var pdefs = new Dictionary<string, GUIListView.ColumnDef>
            {
                { "IspPName", new GUIListView.ColumnDefinition<IspProcess>("Name", (IspProcess x) => x.Label ?? x.Key, true, 300f) },
                { "IspPDemand", new GUIListView.ColumnDefinition<IspProcess>("Bandwidth", (IspProcess x) => (object)IspNetwork.Describe(x.Demand), (IspProcess x) => (float)x.Demand, true, 100f) },
                { "IspPDelivered", new GUIListView.ColumnDefinition<IspProcess>("Delivered", (IspProcess x) => x.Demand > 0 ? (float)(x.Delivered / x.Demand) : 1f, true, 90f, currency: false, includeDecimal: true, progressBar: true, withTotal: false) },
                { "IspPPool", new GUIListView.ColumnDefinition<IspProcess>("Assigned pool", (IspProcess x) => x.Pool ?? (x.Key == IspNetwork.OwnKey ? "None (vanilla ISP)" : "None"), true, 140f) },
                { "IspPServed", new GUIListView.ColumnDefinition<IspProcess>("Served by", (IspProcess x) => x.ServedBy ?? "Nobody", true, 140f) },
            };
            _procs = MakeList(p, pdefs, new[] { "IspPName", "IspPDemand", "IspPDelivered", "IspPPool", "IspPServed" });
            _procs.MultiSelect = true;
            Place(_procs.gameObject, p, 8f, 30f, 8f, 44f, 0f, 0.6f, 1f, 1f);

            MakeButton("Move selected processes", p, new Rect(8f, -40f, -16f, 32f), new Rect(0f, 1f, 1f, 0f), MoveSelected);
        }

        // ---------- refresh

        /// <summary>Called every frame by the behaviour; refreshes the open window twice a second.</summary>
        public static void Tick()
        {
            if (!IsOpen) return;
            EnsureColumns();
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;
            try { Refresh(); }
            catch (Exception e) { Debug.LogError("[ISP Business] ISP window refresh error: " + e); _nextRefresh = Time.unscaledTime + 10f; }
        }

        private static List<ServerGroup> OwnGroups()
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return new List<ServerGroup>();
            return gs.GetAllServerGroups(false, false).Where(g => g != null && g.CloudProvider == 0 && IspNetwork.IsPool(g)).ToList();
        }

        private static void Refresh()
        {
            if (_window == null) return;
            if (_tab == TabTowns) RefreshTowns();
            else if (_tab == TabPlayers) RefreshPlayers();
            else RefreshNetwork();
        }

        private static void RefreshNetwork()
        {
            // Summary: last month's results only, rebuilt when the month ticks or pools change (not live).
            double installed = IspNetwork.State.Values.Sum(st => st.Installed);
            bool hasIspProducts = IspNetwork.OwnIspProducts().Any();
            string summaryKey = IspNetwork.StatsVersion + "|" + IspNetwork.Pools.Count + "|" + installed.ToString("R", CultureInfo.InvariantCulture) + "|" + Deals.Active.Count + "|" + hasIspProducts;
            if (summaryKey != _summaryKey)
            {
                _summaryKey = summaryKey;
                float summaryHeight = hasIspProducts ? 110f : 88f;
                PlaceTop(_summary.gameObject, _netPanel.gameObject, 8f, 0f, 8f, summaryHeight);
                Place(_groups.gameObject, _netPanel.gameObject, 8f, summaryHeight + 4f, 8f, 4f, 0f, 0f, 1f, 0.6f);
                var sb = new StringBuilder();
                sb.Append("<b>ISP pools:</b> ").Append(IspNetwork.Pools.Count)
                  .Append("   <b>Installed capacity:</b> ").Append(IspNetwork.Describe(installed))
                  .Append("   <b>ISP deals:</b> ").Append(Deals.Active.Count)
                  .Append("   <b>Transit this month:</b> ").Append(Money(Config.TransitRate(GameClock.Year))).Append(" per Mbps\n");
                sb.Append("<b>Last month:</b> your pools carried ").Append(IspNetwork.Describe(IspNetwork.LastMonthCarriedAvg)).Append(" on average")
                  .Append("   <b>Your company's internet:</b> ").Append(IspNetwork.Describe(IspNetwork.LastMonthOwnAvg))
                  .Append(", ").Append(IspNetwork.Describe(IspNetwork.LastMonthOwnServedAvg)).Append(" through your ISP\n");
                sb.Append("<b>Last month:</b> saved ").Append(Money(IspNetwork.LastMonthSavings)).Append(" on your own internet")
                  .Append("   <b>ISP deal income:</b> ").Append(Money(IspNetwork.LastMonthRevenue))
                  .Append("   <b>Transit paid:</b> ").Append(Money(IspNetwork.LastMonthTransit)).Append('\n');
                if (hasIspProducts)
                    sb.Append("Your ISP products: ").Append(IspNetwork.Describe(IspNetwork.LastMonthSubscriberCarriedAvg + IspNetwork.LastMonthSubscriberUncarriedAvg))
                      .Append(" average last month (").Append(IspNetwork.Describe(IspNetwork.LastMonthSubscriberCarriedAvg)).Append(" carried by your pools)\n");
                sb.Append("<i>Figures are last month's and update when the month ends. Only ISP pools are listed. If one server breaks, the pool is down and its ISP processes follow its fallback.</i>");
                _summary.text = sb.ToString();
            }

            List<ServerGroup> groups = OwnGroups();
            string key = string.Join("|", groups.Select(g => g.Name).ToArray());
            if (key != _groupsKey)
            {
                object sel = _groups.GetFirstSelected<ServerGroup>();
                _groups.Items.Clear();
                _groups.Items.AddRange(groups.Cast<object>());
                if (sel != null && groups.Contains((ServerGroup)sel)) _groups.Select(sel, false);
                _groupsKey = key;
            }
            RefreshProcesses();
        }

        private static void RefreshProcesses()
        {
            if (_procs == null) return;
            ServerGroup sel = _groups != null ? _groups.GetFirstSelected<ServerGroup>() : null;
            List<IspProcess> list;
            if (sel != null && IspNetwork.IsPool(sel))
            {
                list = IspNetwork.Processes.Values.Where(x => x.Pool == sel.Name || x.ServedBy == sel.Name).ToList();
                _procLabel.text = "<b>ISP processes on " + sel.Name + ":</b>";
            }
            else if (sel != null)
            {
                list = new List<IspProcess>();
                _procLabel.text = "<b>" + sel.Name + "</b> is a hosting group. Its processes are in the vanilla Servers window.";
            }
            else
            {
                list = IspNetwork.Processes.Values.ToList();
                _procLabel.text = "<b>All ISP processes</b> (select a pool to filter):";
            }
            string key = (sel != null ? sel.Name : "") + "|" + string.Join("|", list.Select(x => x.Key).ToArray());
            if (key == _procsKey) return;
            _procsKey = key;
            _procs.Items.Clear();
            _procs.Items.AddRange(list.Cast<object>());
        }

        // ---------- column values

        private static double Bandwidth(ServerGroup g)
        {
            if (IspNetwork.IsPool(g) && IspNetwork.State.TryGetValue(g.Name, out PoolState st)) return st.Installed;
            return g.PowerSum;
        }

        private static float LoadOf(ServerGroup g)
        {
            if (IspNetwork.IsPool(g))
            {
                if (!IspNetwork.State.TryGetValue(g.Name, out PoolState st) || st.Installed <= 0) return 0f;
                return (float)Math.Min(1.0, st.Load / st.Installed);
            }
            return 1f - g.Available;
        }

        private static int ProcessCount(ServerGroup g)
        {
            if (IspNetwork.IsPool(g)) return IspNetwork.Processes.Values.Count(x => x.Pool == g.Name);
            return g.Items.Count;
        }

        private static int StatusRank(ServerGroup g)
        {
            if (g.Broken) return g.Fallback != null ? 1 : 3;
            if (IspNetwork.IsPool(g))
                return IspNetwork.State.TryGetValue(g.Name, out PoolState st) && st.Overloaded ? 2 : 0;
            return g.Available == 0f ? 2 : 0;
        }

        private static string StatusLabel(ServerGroup g)
        {
            switch (StatusRank(g))
            {
                case 1: return "Down, using fallback|ArrowRight|#00AAAA";
                case 3: return "Broken|Lightning|#AA0000";
                case 2: return (IspNetwork.IsPool(g) ? "Over ISP capacity" : "Overloaded") + "|Exclamation|#AAAA00";
                default: return "OK|Checkmark|#00AA00";
            }
        }

        // ---------- actions

        /// <summary>Picker of hosting groups that can become ISP pools.</summary>
        private static void AddGroup()
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return;
            List<ServerGroup> all = gs.GetAllServerGroups(false, false).Where(g => g != null && g.CloudProvider == 0 && !IspNetwork.IsPool(g)).ToList();
            List<ServerGroup> ok = all.Where(g => IspNetwork.CannotBePool(g) == null).OrderBy(g => g.Name, StringComparer.Ordinal).ToList();
            if (ok.Count == 0)
            {
                WindowManager.SpawnDialog(all.Count == 0
                    ? "You have no other server groups. Build servers and give them a group first."
                    : "None of your hosting groups can become an ISP pool: a group must have servers and no processes on it. Move its processes off in the Servers window first.",
                    false, DialogWindow.DialogType.Warning);
                return;
            }
            var labels = ok.Select(g => g.Name + " (ISP capacity " + IspNetwork.Describe(g.Servers.OfType<Server>().Sum(s => (double)Config.ForServer(s.Power, s.GetComponent<Furniture>() != null ? s.GetComponent<Furniture>().Wattage : 0f).IspCapacity)) + ")");
            WindowManager.Instance.MultiWindow.Show("Server group for ISP", labels, (int i) =>
            {
                if (i < 0 || i >= ok.Count) return;
                TogglePool(ok[i].Name);
            }, false);
        }

        // ---------- vanilla Servers window: "ISP mode" button column

        private static GUIListView _serverList;

        /// <summary>Adds an "ISP mode" button to each row of the vanilla Servers window, once per window.</summary>
        public static void EnsureServerWindowColumn()
        {
            ServerWindow sw = HUD.Instance != null ? HUD.Instance.serverWindow : null;
            GUIListView list = sw != null ? sw.ServerList : null;
            if (list == null || list == _serverList) return;
            _serverList = list;
            const string key = "IspModeBtn", header = "ISP mode";
            var def = new GUIListView.ColumnDefinition<ServerGroup>(header, (ServerGroup g) => { if (Hooks.Enabled && g != null) TogglePool(g.Name, true); }, 90f);
            list.ColumnDefinitions[key] = def;
            if (list.GUIColumns == null || list.GUIColumns.Count == 0)
            {
                // Not started yet: its Start() adds every key in Columns.
                if (list.Columns != null && !list.Columns.Contains(key)) list.Columns = list.Columns.Concat(new[] { key }).ToArray();
            }
            else if (list[header] == null)
            {
                list.AddColumn(def);
                list.UpdateElements();
            }
        }

        private static void TogglePool(string name, bool confirm = false)
        {
            GameSettings gs = GameSettings.Instance;
            ServerGroup g = gs != null ? gs.GetServerGroup(name) : null;
            bool isp = !IspNetwork.IsPool(g);
            if (!IspNetwork.SetPool(g, isp, out string error))
                WindowManager.SpawnDialog(error, false, DialogWindow.DialogType.Warning);
            else if (confirm)
                WindowManager.SpawnDialog(isp
                    ? "\"" + name + "\" is now an ISP pool. Manage it in the ISP Network window. Press ISP mode again to put it back to hosting."
                    : "\"" + name + "\" is back to hosting.", false, DialogWindow.DialogType.Information);
            _groupsKey = _procsKey = _summaryKey = _townKey = _playersKey = null;
            if (_window != null) Refresh();
        }

        private static void MoveSelected()
        {
            if (_procs == null) return;
            IspProcess[] sel = _procs.GetSelected<IspProcess>();
            if (sel == null || sel.Length == 0) return;
            PickPool(sel.Select(x => x.Key).ToArray());
        }

        /// <summary>Vanilla-style picker: choose a pool for the given processes, or none.</summary>
        private static void PickPool(string[] keys)
        {
            List<string> pools = IspNetwork.Pools.OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (pools.Count == 0)
            {
                WindowManager.SpawnDialog("You have no ISP pools. Switch a server group to ISP mode first.", false, DialogWindow.DialogType.Warning);
                return;
            }
            double needed = keys.Sum(k => IspNetwork.Processes.TryGetValue(k, out IspProcess p) ? p.Demand : 0);
            var labels = pools.Select(name =>
            {
                PoolState st;
                IspNetwork.State.TryGetValue(name, out st);
                double after = st != null && st.Installed > 0 ? (st.Load + needed) / st.Installed : 0;
                return name + " (load after move: " + (after * 100).ToString("0", CultureInfo.InvariantCulture) + "%)";
            });
            WindowManager.Instance.MultiWindow.Show("ISP pool", labels, (int i) =>
            {
                foreach (string k in keys) IspNetwork.SetAssignment(k, i < 0 ? null : pools[i]);
                IspNetwork.Recalculate();
                IspNetwork.ApplyPower();
                _procsKey = null;
                Refresh();
            }, true);
        }

        private static string Money(double v)
        {
            return ((float)v).Currency();
        }
    }
}
