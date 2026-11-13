// Server Hosting Business - the two Harmony postfixes and the code that applies them.
// Hooks has no Harmony types in any signature, so the game can still load this DLL
// (and run the parts that need no patch) when 0HarmonyLoader is not installed.
using System;

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
                if (p == null || p.ExternalHostingActive || SDateTime.GetMonths(p.Release, SDateTime.Now()) <= 24f) return;
                if (__instance.Performance <= 0f || __instance.Client == null || __instance.Client.Bankrupt) return;
                __result = true;
            }
            catch (Exception)
            {
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(DealWindow), "AddDeal", typeof(Deal), typeof(bool))]
    public static class HostingOfferPricePatch
    {
        public static void Prefix(Deal deal, bool fromNetwork)
        {
            if (!Hooks.Enabled || fromNetwork || !ServerHostingBehaviour.CanPriceLocally()) return;
            if (deal is ServerDeal hosting && hosting.Incoming && hosting.Product != null && !hosting.Active)
                ServerHostingBehaviour.ApplyPrice(hosting);
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(DealWindow), "AddDeal", typeof(Deal), typeof(SDateTime?), typeof(bool))]
    public static class HostingScheduledOfferPricePatch
    {
        public static void Prefix(Deal deal, bool toEveryone)
        {
            if (toEveryone) HostingOfferPricePatch.Prefix(deal, false);
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(ServerDeal), "GetDetailedDescription")]
    public static class HostingOfferDetailsPatch
    {
        public static void Postfix(ServerDeal __instance, System.Collections.Generic.List<string> vars, System.Collections.Generic.List<string> values)
        {
            if (!Hooks.Enabled || __instance.Product == null) return;
            float demand = __instance.Product.GetLoadRequirement().BandwidthFactor(SDateTime.Now());
            float gross = __instance.PerPower * 1440f;
            vars.Add("Projected monthly income");
            values.Add((demand * gross).Currency());
            vars.Add("Estimated monthly margin (ISP + power allowance)");
            values.Add((demand * (gross - Server.GetISPCost() - (float)ServerHostingBehaviour.PowerPerMbpsMonth)).Currency());
        }
    }

    // Patch declarations. Harmony finds these through PatchAll(), so the mod never references
    // System.Reflection (the game's mod security check rejects that namespace).
    [HarmonyLib.HarmonyPatch(typeof(SoftwareProduct), "GetLoadRequirement")]
    public static class LoadRequirementPatch
    {
        public static void Postfix(SoftwareProduct __instance, ref float __result)
        {
            Hooks.LoadRequirementPostfix(__instance, ref __result);
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(ServerDeal), "StillValid")]
    public static class StillValidPatch
    {
        public static void Postfix(ServerDeal __instance, bool active, ref bool __result)
        {
            Hooks.StillValidPostfix(__instance, active, ref __result);
        }
    }

    /// <summary>Only this class calls Harmony. Calls into it are wrapped in try/catch.</summary>
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
            h.PatchAll(); // patches the [HarmonyPatch] classes in this assembly
            _harmony = h;
        }

        private static void UnpatchCore()
        {
            ((HarmonyLib.Harmony)_harmony).UnpatchAll(Id);
            _harmony = null;
        }
    }
}
