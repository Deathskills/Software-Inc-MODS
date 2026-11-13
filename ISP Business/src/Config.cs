// ISP Business - balancing values (spec v0.7 section 22). One place to change numbers.
using System;

namespace ISPBusiness
{
    public static class Config
    {
        /// <summary>Save schema version written as ISP_Ver.</summary>
        public const int SaveVersion = 1;

        /// <summary>Network protocol version. Players must match to share ISP agreements.</summary>
        public const int ProtocolVersion = 3;
        public const string ModVersion = "0.5.0";

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

        // ---- Corporate product ISP deals (spec 5.2, 5.4, 6, 13)
        public const double CorporateReferencePrice = 11.45;   // $ per Mbps per month before the star multiplier
        public const int ProductOffersPerMonth = 10;
        public const double OfferLifetimeMonths = 24.0;
        public const float MinOfferMbps = 1f;                  // products below this don't ask for ISP service
        public const double DealEndMonths = 6.0;               // a deal ends once its product stays under MinOfferMbps this long

        // ---- Company-wide ISP deals (spec 7)
        public const double CompanyDealsFromYear = 1990.0;
        public const double MinCompanyMbps = 1000.0;           // a company needs at least this much to offer one
        public const int CompanyOffersPerMonth = 4;            // separate from the 10 product offers

        // ---- Municipal ISP (spec 5.3, 8, 9, 12, 14)
        public const double MunicipalReferencePrice = 5.06;    // $ per Mbps per month before the star multiplier
        public static readonly double[] MuniInitialPopulation = { 2500, 10000, 50000 };   // rural, town, city in 1980
        public static readonly double[] MuniTargetYear = { 2080, 2065, 2050 };            // year each reaches the cap
        public const double MuniPopulationCap = 40000000;
        public static readonly double[] MuniInitialCoverage = { 100, 200, 300 };          // first deal size
        public const int MuniStartOffersPerMonth = 2;          // first-deal offers per month (expansions are extra)
        public const double MuniCoverageBase = 1000;           // coverage payment per settlement per month: base +
        public const double MuniCoveragePerSqrt = 25;          //   this x sqrt(residents you cover there)
        public const double MuniRepReferenceResidents = 100000; // reputation weight reaches 1 at this many residents
        public const double MuniBreachRep = -0.1;              // business stars lost on a breach notice
        public const double MuniSuitPerResident = 2.0;         // lawsuit claim: $ per covered resident...
        public const double MuniSuitMin = 20000, MuniSuitMax = 2000000; // ...within these bounds

        /// <summary>Vanilla business-star buckets: stars 0..6 roll a multiplier in [1.05 + s*w, 1.05 + (s+1)*w].</summary>
        public static double StarMultiplier(int stars, double roll01)
        {
            const double lo = 1.05, hi = 1.25;
            double w = (hi - lo) / 7.0;
            int s = Math.Max(0, Math.Min(6, stars));
            return lo + (s + Math.Max(0, Math.Min(1, roll01))) * w;
        }

        /// <summary>Monthly Performance change from the month's demand-weighted fulfilment (spec 13.3).</summary>
        public static double PerformanceChange(double fulfilment)
        {
            if (fulfilment >= 0.9999) return 0.06;
            if (fulfilment >= 0.98) return 0.03;
            if (fulfilment >= 0.96) return 0.0;
            if (fulfilment >= 0.95) return -0.02;
            if (fulfilment >= 0.90) return -0.08;
            return -0.20;
        }

        // Reputation per deal per month, in business stars, scaled by sqrt(Mbps / RepReferenceMbps) capped at 1.
        // Vanilla hosting costs -0.1 for an overload; ISP uses the same order of size.
        public const double RepReferenceMbps = 1000.0;
        public const double RepGoodMonth = 0.01;     // fulfilment >= 98%
        public const double RepBadMonth = -0.05;     // fulfilment < 90%
        public const double RepCancel = -0.1;        // player cancels an active deal

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
