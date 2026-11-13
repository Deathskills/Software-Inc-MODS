// ISP Business - the ISP window (spec 18.2 Network tab, Milestone A).
// Opened from a button on the bottom bar. Lists every server group of the player's own company,
// shows ISP capacity, load and power, and switches groups between hosting and ISP mode.
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
        private static Text _summary;
        private static readonly List<GameObject> _rows = new List<GameObject>();
        private static string _rowsKey;
        private static MainBottomButton _button;
        private static float _nextRefresh;

        private const float Width = 640f, Height = 520f, RowHeight = 34f, Top = 150f;

        /// <summary>Adds the bottom-bar button once per game scene.</summary>
        public static void EnsureButton(Sprite icon)
        {
            if (_button != null || HUD.Instance == null) return;
            try
            {
                _button = HUD.Instance.AddBottomButton("ISP", "ISP Network", "Switch server groups to ISP mode and see your ISP capacity and savings.", icon);
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
            _summary = null;
            _rows.Clear();
            _rowsKey = null;
            _button = null;
        }

        public static bool IsOpen { get { return _window != null && _window.gameObject.activeSelf; } }

        public static void Toggle()
        {
            try
            {
                if (IsOpen) { _window.Close(); return; }
                if (_window == null) Build();
                _rowsKey = null;
                Refresh();
                _window.Show();
            }
            catch (Exception e)
            {
                Debug.LogError("[ISP Business] ISP window error: " + e);
            }
        }

        private static void Build()
        {
            _window = WindowManager.SpawnWindow();
            _window.Title = "ISP Network";
            RectTransform rt = _window.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(Width, Height);

            _summary = WindowManager.SpawnLabel();
            _summary.supportRichText = true;
            _summary.alignment = TextAnchor.UpperLeft;
            WindowManager.AddElementToWindow(_summary.gameObject, _window, new Rect(10f, 10f, -20f, Top - 20f), new Rect(0f, 0f, 1f, 0f));
        }

        /// <summary>Called every frame by the behaviour; refreshes the open window twice a second.</summary>
        public static void Tick()
        {
            if (!IsOpen || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;
            try { Refresh(); }
            catch (Exception e) { Debug.LogError("[ISP Business] ISP window refresh error: " + e); _nextRefresh = Time.unscaledTime + 10f; }
        }

        private static List<ServerGroup> OwnGroups()
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return new List<ServerGroup>();
            return gs.GetAllServerGroups(false, false).Where(g => g != null && g.CloudProvider == 0).ToList();
        }

        private static void Refresh()
        {
            if (_window == null) return;
            List<ServerGroup> groups = OwnGroups();

            double installed = 0, capacity = 0, assigned = 0, watts = 0;
            foreach (PoolState st in IspNetwork.State.Values)
            {
                installed += st.Installed; capacity += st.Capacity; assigned += st.Assigned; watts += st.Watts;
            }
            var sb = new StringBuilder();
            sb.Append("<b>ISP pools:</b> ").Append(IspNetwork.Pools.Count)
              .Append("   <b>Capacity:</b> ").Append(IspNetwork.Describe(capacity)).Append(" of ").Append(IspNetwork.Describe(installed)).Append(" installed")
              .Append("   <b>In use:</b> ").Append(IspNetwork.Describe(assigned)).Append('\n');
            sb.Append("<b>Your company's internet:</b> ").Append(IspNetwork.Describe(IspNetwork.LastDemandMbps))
              .Append(", ").Append(IspNetwork.Describe(IspNetwork.LastServedMbps)).Append(" through your own ISP\n");
            sb.Append("<b>Saved this month:</b> ").Append(Money(IspNetwork.MonthSavings))
              .Append(" (transit ").Append(Money(IspNetwork.MonthTransit)).Append(")")
              .Append("   <b>Last month:</b> ").Append(Money(IspNetwork.LastMonthSavings)).Append('\n');
            sb.Append("<b>ISP electricity:</b> ").Append(watts.ToString("N0", CultureInfo.InvariantCulture)).Append(" W now")
              .Append("   <b>Transit rate:</b> ").Append(Money(Config.TransitRate(GameClock.Year))).Append(" per Mbps per month\n");
            sb.Append("<i>A group in ISP mode can't host products. If one of its servers breaks, the whole group is down.</i>");
            _summary.text = sb.ToString();

            string key = string.Join("|", groups.Select(g => g.Name + (IspNetwork.IsPool(g) ? "*" : "")).ToArray());
            if (key != _rowsKey) { RebuildRows(groups); _rowsKey = key; }
            else UpdateRowTexts(groups);
        }

        private static void RebuildRows(List<ServerGroup> groups)
        {
            foreach (GameObject go in _rows) if (go != null) UnityEngine.Object.Destroy(go);
            _rows.Clear();
            float y = Top;
            foreach (ServerGroup g in groups)
            {
                string name = g.Name;
                Text label = WindowManager.SpawnLabel();
                label.supportRichText = true;
                label.alignment = TextAnchor.MiddleLeft;
                WindowManager.AddElementToWindow(label.gameObject, _window, new Rect(10f, y, Width - 190f, RowHeight - 4f), new Rect(0f, 0f, 0f, 0f));
                _rows.Add(label.gameObject);

                Button btn = WindowManager.SpawnButton();
                Text bt = btn.GetComponentInChildren<Text>();
                if (bt != null) bt.text = IspNetwork.IsPool(g) ? "Back to hosting" : "Use as ISP pool";
                btn.onClick.AddListener(() => TogglePool(name));
                WindowManager.AddElementToWindow(btn.gameObject, _window, new Rect(Width - 170f, y, 150f, RowHeight - 4f), new Rect(0f, 0f, 0f, 0f));
                _rows.Add(btn.gameObject);
                y += RowHeight;
            }
            if (groups.Count == 0)
            {
                Text label = WindowManager.SpawnLabel();
                label.text = "You have no server groups yet. Place server furniture and wire it into a group first.";
                WindowManager.AddElementToWindow(label.gameObject, _window, new Rect(10f, y, Width - 20f, RowHeight * 2), new Rect(0f, 0f, 0f, 0f));
                _rows.Add(label.gameObject);
            }
            UpdateRowTexts(groups);
        }

        private static void UpdateRowTexts(List<ServerGroup> groups)
        {
            int i = 0;
            foreach (ServerGroup g in groups)
            {
                if (i >= _rows.Count) break;
                Text label = _rows[i] != null ? _rows[i].GetComponent<Text>() : null;
                if (label != null) label.text = RowText(g);
                i += 2;
            }
        }

        private static string RowText(ServerGroup g)
        {
            if (IspNetwork.IsPool(g) && IspNetwork.State.TryGetValue(g.Name, out PoolState st))
            {
                string status = st.Up ? "<color=#3a3>ISP up</color>" : "<color=#c33>ISP down: " + st.DownReason + "</color>";
                return "<b>" + g.Name + "</b>  " + status + "  " + st.ServerCount + " servers, " +
                       IspNetwork.Describe(st.Assigned) + " / " + IspNetwork.Describe(st.Installed) +
                       " (" + (st.Utilisation * 100).ToString("0", CultureInfo.InvariantCulture) + "%), " +
                       st.Watts.ToString("N0", CultureInfo.InvariantCulture) + " W";
            }
            if (IspNetwork.IsPool(g)) return "<b>" + g.Name + "</b>  ISP (waiting for the next hour)";
            int servers = g.Servers.OfType<Server>().Count();
            return "<b>" + g.Name + "</b>  Hosting, " + servers + " servers, " + g.Items.Count + " items";
        }

        private static void TogglePool(string name)
        {
            GameSettings gs = GameSettings.Instance;
            ServerGroup g = gs != null ? gs.GetServerGroup(name) : null;
            bool isp = !IspNetwork.IsPool(g);
            if (!IspNetwork.SetPool(g, isp, out string error))
                WindowManager.SpawnDialog(error, false, DialogWindow.DialogType.Warning);
            _rowsKey = null;
            Refresh();
        }

        private static string Money(double v)
        {
            return ((float)v).Currency();
        }
    }
}
