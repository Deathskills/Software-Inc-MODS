// Server Hosting Business - per-product classification, game clock and per-save state.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ServerHostingBusiness
{
    public sealed class ProductProfile
    {
        public ProductKind Kind;
        /// <summary>Vanilla per-user server value (before the era factor) that this mod keeps,
        /// e.g. Auto-update or Cloud backup. For online games it excludes covered online-play features.</summary>
        public double KeptPerUser;
    }

    public static class Profiles
    {
        private static readonly Dictionary<uint, ProductProfile> Cache = new Dictionary<uint, ProductProfile>();
        private static readonly ProductProfile NoneProfile = new ProductProfile { Kind = ProductKind.None };
        private static readonly HashSet<string> MmoTerms = new HashSet<string>(StringComparer.Ordinal)
        {
            "MMO", "MMORPG", "Persistent Servers", "Persistent world servers", "PvPvE servers"
        };
        private static readonly HashSet<string> MultiplayerTerms = new HashSet<string>(StringComparer.Ordinal)
        {
            "Multiplayer", "Online Multiplayer", "Dedicated Server"
        };
        private static readonly HashSet<string> OnlinePlayTerms = new HashSet<string>(StringComparer.Ordinal)
        {
            "Online Lobby", "Live Chat", "Reconnection at Match", "Automatic Balancing", "Lag Compensation",
            "Crossplay", "LAN Matchmaking", "NAT Traversal", "Encrypted Multiplayer", "DDoS Protection",
            "Session Persistence", "Server Browser", "Matchmaking with MMR", "Anti-Cheat Multiplayer",
            "Cross-Region Play", "Voice Chat", "Spectator Mode", "PvP", "Battle Royale Mode",
            "Ranked Matchmaking", "Online tables", "Role queue", "Asynchronous messages",
            // Marc, 15 Nov 2026: the remaining MegaMod online game features follow the curves too.
            "Cloud Saving", "Live Service Platform", "Global Ranking", "Clan & Guild System", "Guild system",
            "Multi Panel", "Multiplatform", "Remote Play", "Remote Play Together"
        };
        public static bool MegaModTermsSeen;

        public static void Clear() { Cache.Clear(); MegaModTermsSeen = false; }

        public static ProductProfile Get(SoftwareProduct p)
        {
            if (p == null) return NoneProfile;
            if (Cache.TryGetValue(p.ID, out ProductProfile prof)) return prof;
            prof = Build(p);
            Cache[p.ID] = prof;
            return prof;
        }

        private static bool HasNetworkSpec(FeatureBase[] feats)
        {
            for (int i = 0; i < feats.Length; i++)
            {
                if (feats[i] is SpecFeature sf && sf.Spec == "Network") return true;
            }
            return false;
        }

        private static bool HasFeature(FeatureBase[] feats, string name)
        {
            for (int i = 0; i < feats.Length; i++)
            {
                if (feats[i] != null && feats[i].Name == name) return true;
            }
            return false;
        }

        private static ProductProfile Build(SoftwareProduct p)
        {
            FeatureBase[] feats = p.Features ?? new FeatureBase[0];
            string type = p.Type != null ? p.Type.Name : null;
            string cat = p.Category != null ? p.Category.Name : null;
            var prof = new ProductProfile { Kind = ProductKind.None, KeptPerUser = p.ServerReq };

            switch (type)
            {
                case "Operating System":
                    // MegaMod Data device types reuse the three existing curves (Marc, 15 Nov 2026).
                    if (cat == "Computer" || cat == "Server" || cat == "Supercomputer") prof.Kind = ProductKind.ComputerOS;
                    else if (cat == "Console" || cat == "Smart TV" || cat == "VR Headset" || cat == "Emulator") prof.Kind = ProductKind.ConsoleOS;
                    else if (cat == "Phone" || cat == "Tablet" || cat == "Watch" || cat == "Car") prof.Kind = ProductKind.PhoneOS;
                    break;
                case "Antivirus":
                    if (HasNetworkSpec(feats)) prof.Kind = ProductKind.Antivirus;
                    break;
                case "Office Software":
                    if (HasNetworkSpec(feats))
                        prof.Kind = HasFeature(feats, "Cloud backup") ? ProductKind.OfficeCloud : ProductKind.Office;
                    break;
                case "Game":
                    if (feats.Any(f => f != null && MmoTerms.Contains(f.Name))) prof.Kind = ProductKind.MMO;
                    else if (feats.Any(f => f != null && MultiplayerTerms.Contains(f.Name))) prof.Kind = ProductKind.Multiplayer;
                    if (prof.Kind != ProductKind.None)
                    {
                        if (!MegaModTermsSeen && feats.Any(f => f != null && f.Name != "MMO" && f.Name != "Multiplayer" && IsCoveredOnlineTerm(f.Name)))
                        {
                            MegaModTermsSeen = true;
                            UnityEngine.Debug.Log("[Server Hosting Business] MegaMod Data game features detected; online features use this mod's multiplayer/MMO curves.");
                        }
                        var rest = feats.Where(f => f != null && !IsCoveredOnlineTerm(f.Name)).ToList();
                        prof.KeptPerUser = SoftwareType.GetServerRequirement(rest);
                    }
                    break;
            }
            return prof;
        }

        private static bool IsCoveredOnlineTerm(string name)
        {
            return MmoTerms.Contains(name) || MultiplayerTerms.Contains(name) || OnlinePlayTerms.Contains(name);
        }
    }

    /// <summary>Cached in-game date values, refreshed by the behaviour every frame.</summary>
    public static class Clock
    {
        public static int Stamp = int.MinValue;   // SDateTime.ToInt() of "now"
        public static double Year = 1980;         // fractional year
        public static double Factor = 1;          // vanilla era factor at Year

        public static int MinutesPerMonth { get { return GameSettings.DaysPerMonth * 24 * 60; } }
        public static int MinutesPerDay { get { return 24 * 60; } }

        public static void Refresh()
        {
            SDateTime now = SDateTime.Now();
            int stamp = now.ToInt();
            if (stamp == Stamp) return;
            Stamp = stamp;
            // Same conversion vanilla uses in Utilities.BandwidthFactor.
            Year = SDateTime.BaseYear + stamp / 12.0 / Math.Max(1, GameSettings.DaysPerMonth) / 24.0 / 60.0;
            Factor = Rates.VanillaFactor(Year);
        }
    }

    /// <summary>State stored in the save file (through ModBehaviour.Serialize).</summary>
    public static class SaveState
    {
        /// <summary>Hosting offers this mod created: deal ID -> date offered.</summary>
        public static readonly Dictionary<uint, int> Offers = new Dictionary<uint, int>();
        /// <summary>Hosting deals already given this mod's price: deal ID -> date priced.
        /// The price is set once, when the offer is made, and never changed after.</summary>
        public static readonly Dictionary<uint, int> Priced = new Dictionary<uint, int>();
        /// <summary>Products this mod made an offer for: product ID -> date offered.</summary>
        public static readonly Dictionary<uint, int> Cooldown = new Dictionary<uint, int>();

        public static void Reset()
        {
            Offers.Clear();
            Priced.Clear();
            Cooldown.Clear();
        }

        public static string Pack(Dictionary<uint, int> d)
        {
            var sb = new StringBuilder();
            foreach (var kv in d)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key.ToString(CultureInfo.InvariantCulture)).Append(':').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static void Unpack(string s, Dictionary<uint, int> d)
        {
            d.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (string part in s.Split(';'))
            {
                int i = part.IndexOf(':');
                if (i <= 0) continue;
                if (uint.TryParse(part.Substring(0, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint k) &&
                    int.TryParse(part.Substring(i + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                    d[k] = v;
            }
        }
    }
}
