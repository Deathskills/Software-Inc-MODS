// ISP Business - Harmony patches. Postfix/prefix only, attribute-declared so this DLL never
// references System.Reflection (blocked by the game's mod security check).
//
// Patched methods (none of them is patched by Server Hosting Business):
//  - TimeOfDay.UpdateHour (private): prefix records ServerCost, postfix runs the ISP hour.
//  - ServerGroup.RefreshUsage: postfix puts back ISP power use after vanilla resets it.
//  - GameSettings.ChangeServerName: postfix keeps a renamed ISP pool in ISP mode.
//  - DealWindow.Serialize: postfix leaves ISP deals out of the vanilla save (they're in the mod's data).
//  - BuilderDetailPanel.SetFurniture: postfix adds the ISP bandwidth to a server's Bandwidth line in the shop.
using System;
using UnityEngine;

namespace ISPBusiness
{
    public static class Hooks
    {
        public static bool Enabled;
        private static float _serverCostBefore;
        private static int _errors;

        public static void BeforeHour()
        {
            if (!Enabled || IspBehaviour.IsReadOnlySave) return;
            try { _serverCostBefore = GameSettings.Instance != null ? GameSettings.Instance.ServerCost : 0f; }
            catch (Exception) { _serverCostBefore = 0f; }
        }

        public static void AfterHour()
        {
            if (!Enabled || IspBehaviour.IsReadOnlySave) return;
            try
            {
                GameSettings gs = GameSettings.Instance;
                if (gs == null) return;
                double delta = gs.ServerCost - _serverCostBefore;
                IspNetwork.OnHour(delta > 0 ? delta : 0);
            }
            catch (Exception e)
            {
                ReportError("hourly ISP update", e);
            }
        }

        public static void AfterRefreshUsage(ServerGroup g)
        {
            if (!Enabled || IspBehaviour.IsReadOnlySave || g == null) return;
            try
            {
                if (IspNetwork.IsPool(g) && IspNetwork.State.TryGetValue(g.Name, out PoolState st))
                    IspNetwork.ApplyPower(g, st);
            }
            catch (Exception e)
            {
                ReportError("ISP power refresh", e);
            }
        }

        public static void AfterRename(object[] args)
        {
            if (!Enabled || IspBehaviour.IsReadOnlySave || args == null || args.Length < 2) return;
            try { IspNetwork.Rename(args[0] as string, args[1] as string); }
            catch (Exception e) { ReportError("pool rename", e); }
        }

        /// <summary>Appends "ISP: x" to the vanilla Bandwidth bar of a server in the furniture shop panel.</summary>
        public static void ShopPanel(BuilderDetailPanel panel, Furniture furn)
        {
            if (furn.Type == null || !furn.Type.Equals("Server")) return;
            Server s = furn.GetComponent<Server>();
            if (s == null || panel.ProgressBars == null) return;
            Config.Equipment eq = Config.ForServer(s.Power, furn.Wattage);
            string prefix = "Bandwidth".Loc();
            foreach (GameObject bar in panel.ProgressBars)
            {
                if (bar == null) continue;
                UnityEngine.UI.Text t = bar.GetComponentInChildren<UnityEngine.UI.Text>();
                if (t == null || t.text == null || !t.text.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (t.text.Contains("ISP")) return;
                t.text += "   ISP: " + IspNetwork.Describe(eq.IspCapacity);
                return;
            }
        }

        private static void ReportError(string what, Exception e)
        {
            _errors++;
            if (_errors <= 5) Debug.LogError("[ISP Business] Error in " + what + ": " + e);
            if (_errors == 5) Debug.LogError("[ISP Business] Further errors are not logged.");
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(TimeOfDay), "UpdateHour")]
    public static class UpdateHourPatch
    {
        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Low)]
        public static void Prefix() { Hooks.BeforeHour(); }

        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Low)]
        public static void Postfix() { Hooks.AfterHour(); }
    }

    [HarmonyLib.HarmonyPatch(typeof(ServerGroup), "RefreshUsage")]
    public static class RefreshUsagePatch
    {
        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Low)]
        public static void Postfix(ServerGroup __instance) { Hooks.AfterRefreshUsage(__instance); }
    }

    [HarmonyLib.HarmonyPatch(typeof(GameSettings), "ChangeServerName")]
    public static class ChangeServerNamePatch
    {
        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Low)]
        public static void Postfix(object[] __args) { Hooks.AfterRename(__args); }
    }

    [HarmonyLib.HarmonyPatch(typeof(DealWindow), "Serialize")]
    public static class DealSavePatch
    {
        // Runs even when Hooks.Enabled is off: ISP deals must never reach a save.
        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Last)]
        public static void Postfix(DealWindow __instance, WriteDictionary dict)
        {
            try { VanillaDeals.FilterSave(__instance, dict); }
            catch (Exception e) { Debug.LogError("[ISP Business] Could not filter ISP deals out of the save: " + e); }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(BuilderDetailPanel), "SetFurniture")]
    public static class ShopPanelPatch
    {
        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Low)]
        public static void Postfix(BuilderDetailPanel __instance, Furniture furn)
        {
            if (!Hooks.Enabled || __instance == null || furn == null) return;
            try { Hooks.ShopPanel(__instance, furn); }
            catch (Exception e) { Debug.LogError("[ISP Business] Shop panel: " + e.Message); }
        }
    }

    /// <summary>Only this class calls Harmony directly. Calls into it are wrapped in try/catch.</summary>
    public static class HarmonyBridge
    {
        public const string Id = "deathskills.softwareinc.ispbusiness";
        private static object _harmony;

        public static bool HarmonyAvailable()
        {
            try { return Type.GetType("HarmonyLib.Harmony, 0Harmony", false) != null; }
            catch (Exception) { return false; }
        }

        public static void Patch()
        {
            if (_harmony != null) return;
            PatchCore();
        }

        public static void Unpatch()
        {
            if (_harmony == null) return;
            UnpatchCore();
        }

        private static void PatchCore()
        {
            var h = new HarmonyLib.Harmony(Id);
            h.PatchAll();
            _harmony = h;
        }

        private static void UnpatchCore()
        {
            ((HarmonyLib.Harmony)_harmony).UnpatchAll(Id);
            _harmony = null;
        }
    }
}
