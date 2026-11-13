// ISP Business - balancing values (spec v0.7 section 22). One place to change numbers.
using System;

namespace ISPBusiness
{
    public static class Config
    {
        /// <summary>Save schema version written as ISP_Ver.</summary>
        public const int SaveVersion = 1;

        /// <summary>Network protocol version. Players must match to share ISP agreements.</summary>
        public const int ProtocolVersion = 1;
        public const string ModVersion = "0.1.0";

        /// <summary>Network id registered with the game for this mod's messages (first come, first served).</summary>
        public const byte NetworkId = 0xA7;

        // ---- ISP equipment, matched by the vanilla bandwidth (Server.Power) of each server furniture.
        //      Small 50, Medium 250, Tower 1000, Rack 5000, Big Rack 50000 Mbps in vanilla.
        public struct Equipment
        {
            public string Name;
            public float VanillaPower;    // vanilla hosting Mbps, used to recognise the furniture
            public float IspCapacity;     // Mbps in ISP mode
            public float WattsPerMbps;    // ISP electricity at full load
        }

        public static readonly Equipment[] Servers =
        {
            new Equipment { Name = "Small Server",    VanillaPower = 50f,    IspCapacity = 50f,     WattsPerMbps = 6f },
            new Equipment { Name = "Medium Server",   VanillaPower = 250f,   IspCapacity = 250f,    WattsPerMbps = 4f },
            new Equipment { Name = "Tower Server",    VanillaPower = 1000f,  IspCapacity = 15000f,  WattsPerMbps = 2f },
            new Equipment { Name = "Server Rack",     VanillaPower = 5000f,  IspCapacity = 50000f,  WattsPerMbps = 0.25f },
            new Equipment { Name = "Big Server Rack", VanillaPower = 50000f, IspCapacity = 250000f, WattsPerMbps = 0.053f },
        };

        /// <summary>Wear multiplier for ISP servers: max(IdleWearFloor, utilisation + 0.25).
        /// Vanilla uses max(1, load + 0.25), so idle servers wear at full speed there.</summary>
        public const float IdleWearFloor = 0.25f;

        // ---- Wholesale transit, $ per Mbps per month (spec 4.2), linear between points, held after the last.
        private static readonly double[] TransitYears = { 1980, 1990, 2000, 2010, 2020, 2030, 2050 };
        private static readonly double[] TransitRates = { 0.50, 0.42, 0.34, 0.26, 0.18, 0.10, 0.05 };

        public static double TransitRate(double year)
        {
            if (year <= TransitYears[0]) return TransitRates[0];
            for (int i = 1; i < TransitYears.Length; i++)
            {
                if (year <= TransitYears[i])
                {
                    double t = (year - TransitYears[i - 1]) / (TransitYears[i] - TransitYears[i - 1]);
                    return TransitRates[i - 1] + (TransitRates[i] - TransitRates[i - 1]) * t;
                }
            }
            return TransitRates[TransitRates.Length - 1];
        }

        /// <summary>ISP profile of a vanilla server, or a 1:1 fallback for servers this mod doesn't know
        /// (modded furniture keeps its vanilla bandwidth and vanilla wattage per Mbps).</summary>
        public static Equipment ForServer(float vanillaPower, float wattage)
        {
            for (int i = 0; i < Servers.Length; i++)
            {
                if (Math.Abs(Servers[i].VanillaPower - vanillaPower) < 0.5f) return Servers[i];
            }
            float p = Math.Max(1f, vanillaPower);
            return new Equipment { Name = "Other server", VanillaPower = p, IspCapacity = p, WattsPerMbps = wattage / p };
        }
    }
}
