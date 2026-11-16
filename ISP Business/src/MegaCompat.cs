// MegaMod compatibility; Core detection is cached until the ISP session is reset.
using System;

namespace ISPBusiness
{
    public static class MegaCompat
    {
        public const string IspProductType = "Internet Service Provider";

        private static bool? _coreActive;

        public static bool IsIspProduct(SoftwareProduct p)
        {
            return p != null && p.Type != null && p.Type.Name == IspProductType;
        }

        public static double SubscriberMbps(SoftwareProduct p)
        {
            return p == null || p.Userbase <= 0 ? 0 : p.Userbase * Municipal.UsageMBPerUser(GameClock.Year) * 8.0 / 2592000.0;
        }

        public static bool CoreActive
        {
            get
            {
                if (!_coreActive.HasValue) _coreActive = DetectCore();
                return _coreActive.Value;
            }
        }

        public static void Reset() { _coreActive = null; }

        private static bool DetectCore()
        {
            try
            {
                foreach (ModController.DLLMod m in ModController.Instance.Mods)
                {
                    if (m == null || !m.Active) continue;
                    if ((m.ItemTitle ?? "").IndexOf("MegaMod Core", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (m.ModPath ?? "").IndexOf("3813626791", StringComparison.Ordinal) >= 0) return true;
                }
            }
            catch { return false; }
            return false;
        }
    }
}
