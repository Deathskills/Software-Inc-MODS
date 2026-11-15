// MegaMod Core detection, cached until the ISP session is reset.
using System;

namespace ISPBusiness
{
    public static class MegaCompat
    {
        private static bool? _coreActive;

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
