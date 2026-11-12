// Server Hosting Business - bandwidth curves and hosting profit rates.
// All bandwidth values are Mbps per 100,000 active users, already including the era
// (they replace vanilla's 0.75 + log10(year - 1969) factor for the parts this mod owns).
using System;

namespace ServerHostingBusiness
{
    public enum ProductKind
    {
        None,          // not changed by this mod
        ComputerOS,
        ConsoleOS,
        PhoneOS,
        Antivirus,     // antivirus with the Network spec
        Office,        // office software with the Network spec
        OfficeCloud,   // office software with Network + Cloud backup
        Multiplayer,   // game with Multiplayer
        MMO            // game with Multiplayer + MMO
    }

    public enum RateClass { Vanilla, Multiplayer, MMO, Enterprise }

    public static class Rates
    {
        // ---- Marc's 5-year chart points (Mbps per 100k users), 1985..2025
        private static readonly int[] Years5 = { 1985, 1990, 1995, 2000, 2005, 2010, 2015, 2020, 2025 };
        private static readonly double[] PC        = { 0.01, 0.05, 0.5, 3, 12, 35, 105, 220, 355 };
        private static readonly double[] Console   = { 0, 0, 0, 0.1, 1, 12, 55, 125, 191 };
        private static readonly double[] Phone     = { 0, 0, 0, 0, 0.1, 5, 35, 95, 142 };
        private static readonly double[] AV        = { 0.01, 0.05, 0.5, 3, 12, 25, 45, 60, 75 };
        private static readonly double[] OfficeOff = { 0.01, 0.03, 0.3, 2, 8, 15, 25, 35, 45 };
        private static readonly double[] OfficeCld = { 0, 0, 0, 0.1, 0.5, 8, 45, 180, 560 };

        // ---- Profit per Mbps per in-game month, at the 100% star midpoint
        public const double ProfitMultiplayer = 2.00;
        public const double ProfitMMO = 6.00;
        public const double ProfitEnterprise = 8.55;
        public const double StarFactorMin = 0.8;  // 0 business stars
        public const double StarFactorMax = 1.2;  // 5 business stars

        /// <summary>Vanilla era factor: 0.75 + log10(year - 1969).</summary>
        public static double VanillaFactor(double year)
        {
            return 0.75 + Math.Log10(Math.Max(1.0, year - 1969.0));
        }

        /// <summary>Linear between 5-year points. A 0 point stays 0 until the next point.
        /// Before 1985 holds the 1985 value; after 2025 keeps the 2020-2025 slope.</summary>
        public static double Baseline(double[] v, double year)
        {
            if (year <= 1985) return v[0];
            if (year >= 2025) return Math.Max(0, v[8] + (v[8] - v[7]) / 5.0 * (year - 2025));
            int i = (int)Math.Floor((year - 1985) / 5.0);
            if (i > 7) i = 7;
            double t = (year - Years5[i]) / 5.0;
            if (t <= 0) return v[i];
            if (v[i] == 0) return 0;
            return v[i] + (v[i + 1] - v[i]) * t;
        }

        public static double MultiplayerAt(double year)
        {
            if (year < 1990) return 0;
            double a = 30.0 * VanillaFactor(1990);              // vanilla 1990 (~62)
            if (year <= 2000) return a + (120 - a) * (year - 1990) / 10.0;
            if (year <= 2010) return 120 + 230 * (year - 2000) / 10.0;
            if (year <= 2020) return 350 + 150 * (year - 2010) / 10.0;
            return 500 * Math.Pow(1.05, (year - 2020) / 5.0);  // +5% every 5 years
        }

        public static double MMOAt(double year)
        {
            if (year < 1990) return 0;
            double a = 230.0 * VanillaFactor(1990);             // vanilla 1990 (~477)
            if (year <= 2020) return a + (900 - a) * (year - 1990) / 30.0;
            return 900 * Math.Pow(1.05, (year - 2020) / 5.0);
        }

        /// <summary>Mbps per 100k users that this mod adds (or, for games, replaces).</summary>
        public static double ModRate(ProductKind kind, double year)
        {
            switch (kind)
            {
                case ProductKind.ComputerOS: return Baseline(PC, year);
                case ProductKind.ConsoleOS: return Baseline(Console, year);
                case ProductKind.PhoneOS: return Baseline(Phone, year);
                case ProductKind.Antivirus: return Baseline(AV, year);
                case ProductKind.Office: return Baseline(OfficeOff, year);
                case ProductKind.OfficeCloud: return Baseline(OfficeOff, year) + Baseline(OfficeCld, year);
                case ProductKind.Multiplayer: return MultiplayerAt(year);
                case ProductKind.MMO: return MMOAt(year);
                default: return 0;
            }
        }

        public static RateClass ClassOf(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Multiplayer: return RateClass.Multiplayer;
                case ProductKind.MMO: return RateClass.MMO;
                case ProductKind.None: return RateClass.Vanilla;
                default: return RateClass.Enterprise;
            }
        }

        public static double Profit(RateClass c)
        {
            switch (c)
            {
                case RateClass.Multiplayer: return ProfitMultiplayer;
                case RateClass.MMO: return ProfitMMO;
                case RateClass.Enterprise: return ProfitEnterprise;
                default: return 0;
            }
        }

        public static double StarFactor(int stars)
        {
            double s = Math.Max(0, Math.Min(5, stars)) / 5.0;
            return StarFactorMin + (StarFactorMax - StarFactorMin) * s;
        }
    }
}
