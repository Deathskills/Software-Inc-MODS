// ISP Business - Harmony patches. Postfix/prefix only, attribute-declared so this DLL never
// references System.Reflection (blocked by the game's mod security check).
//
// Patched methods (none of them is patched by Server Hosting Business):
//  - TimeOfDay.UpdateHour (private): prefix records ServerCost, postfix runs the ISP hour.
//  - ServerGroup.RefreshUsage: postfix puts back ISP power use after vanilla resets it.
//  - GameSettings.ChangeServerName: postfix keeps a renamed ISP pool in ISP mode.
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
            if (!Enabled) return;
            try { _serverCostBefore = GameSettings.Instance != null ? GameSettings.Instance.ServerCost : 0f; }
            catch (Exception) { _serverCostBefore = 0f; }
        }

        public static void AfterHour()
        {
            if (!Enabled) return;
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
            if (!Enabled || g == null) return;
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
            if (!Enabled || args == null || args.Length < 2) return;
            try { IspNetwork.Rename(args[0] as string, args[1] as string); }
            catch (Exception e) { ReportError("pool rename", e); }
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
