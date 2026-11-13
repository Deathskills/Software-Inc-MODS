// ISP Business - Software Inc. code mod: run your own ISP (spec v0.7). Milestone A:
//  - switch vanilla server groups to ISP mode (ISP pools) with their own capacity and power use
//  - your company's own internet traffic goes through your pools: vanilla ISP rate replaced by transit
//  - idle ISP capacity draws little power and wears slowly
//  - multiplayer: each player runs their own ISP; a version handshake prepares player agreements
// Requires 0HarmonyLoader (Harmony 2.3.3). Without it the mod stays off.
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ISPBusiness
{
    public class IspMeta : ModMeta
    {
        public override string Name => "ISP Business";

        public override void Initialize(ModController.DLLMod parentMod)
        {
            Net.Mod = parentMod;
        }

        public override void ConstructOptionsScreen(RectTransform parent, bool inGame)
        {
            Text label = WindowManager.SpawnLabel();
            label.text = IspBehaviour.Patched
                ? "ISP Business " + Config.ModVersion + " is active. Open the ISP Network window from the bottom bar in game.\n" +
                  "ISP capacity: Small 50 Mbps, Medium 250 Mbps, Tower 15 Gbps, Rack 50 Gbps, Big Rack 250 Gbps.\n" +
                  "ISP power: 6 / 4 / 2 / 0.25 / 0.053 W per Mbps at full load."
                : "0HarmonyLoader was not found, so ISP Business is off. Install 0HarmonyLoader and restart the game.";
            WindowManager.AddElementToElement(label.gameObject, parent.gameObject, new Rect(0f, 0f, 460f, 96f), new Rect(0f, 0f, 1f, 0f));
        }

        public override void ReceiveNetworkMessage(SINetworking.NetworkPlayer player, MemoryStream stream)
        {
            Net.Receive(player, stream);
        }
    }

    public class IspBehaviour : ModBehaviour
    {
        public static bool Patched;

        private bool _active;
        private GameSettings _seenGame;
        private Sprite _icon;
        private bool _iconTried;

        private string _pendingPools;
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
                    Debug.LogError("[ISP Business] Harmony patch failed, the mod stays off: " + e);
                }
            }
            if (!Patched) Debug.LogWarning("[ISP Business] 0HarmonyLoader not found. The mod stays off.");
            Hooks.Enabled = Patched;
            if (Patched) Net.Register();
        }

        public override void OnDeactivate()
        {
            _active = false;
            Hooks.Enabled = false;
            // Put every ISP server back to vanilla power and wear before unpatching.
            foreach (string name in IspNetwork.Pools.ToArray())
            {
                ServerGroup g = GameSettings.Instance != null ? GameSettings.Instance.GetServerGroup(name) : null;
                if (g != null) IspNetwork.SetPool(g, false, out string _);
            }
            if (Patched)
            {
                try { HarmonyBridge.Unpatch(); } catch (Exception e) { Debug.LogError("[ISP Business] Unpatch failed: " + e); }
                Patched = false;
            }
            Net.Unregistered();
        }

        // ---------- save data (each player's own local save; vanilla never sends it to other players)

        public override void Serialize(WriteDictionary data, GameReader.LoadMode mode)
        {
            if (!Patched) return;
            data["ISP_Ver"] = Config.SaveVersion;
            data["ISP_Pools"] = IspNetwork.PackPools();
        }

        public override void Deserialize(WriteDictionary data, GameReader.LoadMode mode)
        {
            int ver = data.Get("ISP_Ver", 0);
            if (ver > Config.SaveVersion)
            {
                Debug.LogError("[ISP Business] This save was made by a newer ISP Business (save version " + ver + "). ISP data was not loaded, and saving now will drop it.");
                return;
            }
            _pendingPools = data.Get<string>("ISP_Pools", null);
            _hasPending = true;
            if (_seenGame != null && _seenGame == GameSettings.Instance) ApplyPending();
        }

        private void ApplyPending()
        {
            if (!_hasPending) return;
            _hasPending = false;
            IspNetwork.UnpackPools(_pendingPools);
            _pendingPools = null;
        }

        // ---------- main loop

        private void Update()
        {
            if (!_active || !Patched) return;
            if (GameSettings.Instance.IsReferenceNull() || TimeOfDay.Instance.IsReferenceNull() || HUD.Instance.IsReferenceNull())
            {
                if (_seenGame != null) { _seenGame = null; IspWindow.Forget(); }
                return;
            }
            try
            {
                if (_seenGame != GameSettings.Instance)
                {
                    _seenGame = GameSettings.Instance;
                    IspNetwork.Reset();
                    Net.ResetSession();
                    IspWindow.Forget();
                    ApplyPending();
                }

                if (!_iconTried) { _iconTried = true; _icon = LoadIcon(); }
                IspWindow.EnsureButton(_icon);
                IspWindow.Tick();
                Net.Tick();

                if (IspNetwork.PendingNotices.Count > 0)
                {
                    string msg = string.Join("\n\n", IspNetwork.PendingNotices.ToArray());
                    IspNetwork.PendingNotices.Clear();
                    WindowManager.SpawnDialog(msg, false, DialogWindow.DialogType.Warning);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ISP Business] " + e);
            }
        }

        private Sprite LoadIcon()
        {
            try
            {
                Texture2D tex = ParentMod != null ? ParentMod.LoadTexture("icon.png") : null;
                if (tex == null) return null;
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
