// Server Hosting Business - the two Harmony postfixes and the code that applies them.
// Hooks has no Harmony types in any signature, so the game can still load this DLL
// (and run the parts that need no patch) when 0HarmonyLoader is not installed.
using System;
using System.Reflection;

namespace ServerHostingBusiness
{
    public static class Hooks
    {
        public static bool Enabled;

        /// <summary>How long an offer made by this mod stays open, in in-game months.</summary>
        public const double OfferLifetimeMonths = 24.0;

        /// <summary>Per-user value in vanilla units (multiplied by the era factor afterwards).</summary>
        public static double PerUser(SoftwareProduct p, ProductProfile prof)
        {
            // Vanilla add-ons (KeptPerUser) keep their vanilla value and era factor;
            // the mod's rate already includes the era, so divide out the factor vanilla applies later.
            return prof.KeptPerUser + Rates.ModRate(prof.Kind, Clock.Year) / 100000.0 / Clock.Factor;
        }

        // Postfix for SoftwareProduct.GetLoadRequirement() (vanilla: ServerReq * Userbase).
        public static void LoadRequirementPostfix(SoftwareProduct __instance, ref float __result)
        {
            if (!Enabled || __instance == null) return;
            try
            {
                ProductProfile prof = Profiles.Get(__instance);
                if (prof.Kind == ProductKind.None) return;
                __result = (float)(PerUser(__instance, prof) * __instance.Userbase);
            }
            catch (Exception)
            {
                // never break the game's server simulation
            }
        }

        // Postfix for ServerDeal.StillValid(bool active): offers made by this mod for products
        // older than 24 months stay open for 24 months from the day they were offered.
        public static void StillValidPostfix(ServerDeal __instance, bool active, ref bool __result)
        {
            if (!Enabled || __result || active || __instance == null) return;
            try
            {
                if (!SaveState.Offers.TryGetValue(__instance.ID, out int offered)) return;
                if ((Clock.Stamp - offered) / (double)Clock.MinutesPerMonth > OfferLifetimeMonths) return;
                SoftwareProduct p = __instance.Product;
                if (p == null || p.ExternalHostingActive) return;
                if (__instance.Performance <= 0f || __instance.Client == null) return;
                __result = true;
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>Only this class touches Harmony. Calls into it are wrapped in try/catch.</summary>
    public static class HarmonyBridge
    {
        public const string Id = "deathskills.softwareinc.serverhostingbusiness";
        private static object _harmony;

        public static bool HarmonyAvailable()
        {
            try
            {
                return Type.GetType("HarmonyLib.Harmony, 0Harmony", false) != null;
            }
            catch (Exception)
            {
                return false;
            }
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
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            MethodInfo load = typeof(SoftwareProduct).GetMethod("GetLoadRequirement", all, null, Type.EmptyTypes, null);
            MethodInfo valid = typeof(ServerDeal).GetMethod("StillValid", all, null, new[] { typeof(bool) }, null);
            if (load == null || valid == null)
                throw new MissingMethodException("Server Hosting Business: game methods not found, this game version is not supported.");

            h.Patch(load, postfix: new HarmonyLib.HarmonyMethod(typeof(Hooks).GetMethod(nameof(Hooks.LoadRequirementPostfix))));
            h.Patch(valid, postfix: new HarmonyLib.HarmonyMethod(typeof(Hooks).GetMethod(nameof(Hooks.StillValidPostfix))));
            _harmony = h;
        }

        private static void UnpatchCore()
        {
            ((HarmonyLib.Harmony)_harmony).UnpatchAll(Id);
            _harmony = null;
        }
    }
}
