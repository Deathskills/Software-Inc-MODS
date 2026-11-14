// ISP Business - multiplayer messages over the game's mod message channel
// (ModController.DLLMod.SendNetworkMessage / ModMeta.ReceiveNetworkMessage). No "all access" needed.
//
// Rule for every message this mod will send: it carries a decision made on the sender's machine only
// (one side decides). Money is never moved by a message: the paying player's game makes vanilla
// transactions, which vanilla broadcasts.
//
// Hello carries protocol + mod version, so players learn who runs a matching version. Then: deal claims,
// town coverage, and agreements between players (offer, connect, reply, hourly usage and service, end).
// Messages only reach players already in game, so Hello is re-sent to every player who appears, and
// claims, coverage and an open offer are re-sent to them after it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ISPBusiness
{
    public static class Net
    {
        public const byte MsgHello = 1;
        public const byte MsgClaim = 2;   // product id + 1 = claim / 0 = release, decided by the sender
        public const byte MsgCompanyClaim = 3;   // company id + 1 = claim / 0 = release (company-wide deal)
        public const byte MsgCoverage = 4;       // settlement id + residents the sender covers there (municipal)
        // Agreements between players (Agreements.cs). Each one carries the decision of its sender only.
        public const byte MsgOffer = 5;          // provider -> all: open, standard, premium, spare Mbps, company id
        public const byte MsgConnect = 6;        // customer -> provider: token, tier, price
        public const byte MsgReply = 7;          // provider -> customer: token, accepted, reason
        public const byte MsgUsage = 8;          // customer -> provider, hourly: token, Mbps wanted
        public const byte MsgService = 9;        // provider -> customer, hourly: token, share delivered
        public const byte MsgEnd = 10;           // either side: token, reason

        public static ModController.DLLMod Mod;
        public static bool Registered;

        /// <summary>Names of players in the game, for messages.</summary>
        private static readonly Dictionary<byte, string> Names = new Dictionary<byte, string>();

        /// <summary>Peers that answered: player id -> (protocol, mod version).</summary>
        public static readonly Dictionary<byte, KeyValuePair<int, string>> Peers = new Dictionary<byte, KeyValuePair<int, string>>();
        private static readonly HashSet<byte> Greeted = new HashSet<byte>();
        private static readonly HashSet<byte> Warned = new HashSet<byte>();
        private static readonly Dictionary<byte, float> GreetedAt = new Dictionary<byte, float>();

        private struct Incoming { public byte From; public string Name; public byte[] Data; }
        private static readonly List<Incoming> Inbox = new List<Incoming>();

        public static void Register()
        {
            if (Registered || Mod == null) return;
            try
            {
                Mod.RegisterNetworkID(Config.NetworkId);
                Registered = true;
            }
            catch (Exception e)
            {
                Debug.LogError("[ISP Business] Could not register network id " + Config.NetworkId + ": " + e.Message);
            }
        }

        public static void Unregistered()
        {
            Registered = false;
            ResetSession();
        }

        public static void ResetSession()
        {
            Peers.Clear();
            Greeted.Clear();
            Warned.Clear();
            GreetedAt.Clear();
            Names.Clear();
            lock (Inbox) Inbox.Clear();
            Agreements.ResetSession();
        }

        public static string NameOf(byte id) { return Names.TryGetValue(id, out string n) ? n : "Player " + id; }

        // ---------- receive (may be called by the game's network code; handled on the main thread in Tick)

        public static void Receive(SINetworking.NetworkPlayer player, MemoryStream stream)
        {
            if (player == null || stream == null) return;
            try
            {
                long start = stream.Position;
                if (start >= stream.Length) start = 1; // self-delivery quirk: stream at its end, id byte at 0
                int len = (int)(stream.Length - start);
                if (len <= 0) return;
                byte[] buf = new byte[len];
                stream.Position = start;
                int read = stream.Read(buf, 0, len);
                if (read < len) Array.Resize(ref buf, read);
                lock (Inbox) Inbox.Add(new Incoming { From = player.ID, Name = player.Name, Data = buf });
            }
            catch (Exception e)
            {
                Debug.LogError("[ISP Business] Bad network message: " + e.Message);
            }
        }

        /// <summary>Called every frame from the behaviour (main thread).</summary>
        public static void Tick()
        {
            GameSettings gs = GameSettings.Instance;
            if (!Registered || gs == null || !gs.IsNetworkMode) return;

            List<Incoming> batch;
            lock (Inbox) { batch = Inbox.ToList(); Inbox.Clear(); }
            foreach (Incoming m in batch) Handle(m);
            Agreements.Tick();

            // Greet every player in the game once, and again after they leave and come back.
            var present = new HashSet<byte>();
            foreach (SINetworking.NetworkPlayer p in Mod.GetCurrentPlayers())
            {
                if (p == null || p.Self) continue;
                present.Add(p.ID);
                Names[p.ID] = p.Name;
                if (Greeted.Add(p.ID))
                {
                    SendHello(p.ID, false);
                    GreetedAt[p.ID] = Time.realtimeSinceStartup;
                }
                else if (!Peers.ContainsKey(p.ID) && !Warned.Contains(p.ID) &&
                         GreetedAt.TryGetValue(p.ID, out float at) && Time.realtimeSinceStartup - at > 90f)
                {
                    Warned.Add(p.ID);
                    IspNetwork.PendingNotices.Add(p.Name + " doesn't seem to run ISP Business. They can't make ISP agreements with you, but everything else works normally.");
                }
            }
            foreach (byte id in Greeted.Where(x => !present.Contains(x)).ToList())
            {
                Greeted.Remove(id);
                GreetedAt.Remove(id);
                Peers.Remove(id);
                Warned.Remove(id);
                Deals.OnPlayerLeft(id);
                Agreements.OnPlayerLeft(id, NameOf(id));
            }
        }

        private static void Handle(Incoming m)
        {
            if (m.Data.Length < 1) return;
            int pos = 1;
            switch (m.Data[0])
            {
                case MsgHello:
                    int protocol = ReadInt(m.Data, ref pos);
                    string version = ReadString(m.Data, ref pos);
                    bool isReply = pos < m.Data.Length && m.Data[pos++] == 1;
                    Peers[m.From] = new KeyValuePair<int, string>(protocol, version);
                    if (!string.IsNullOrEmpty(m.Name)) Names[m.From] = m.Name;
                    if (protocol != Config.ProtocolVersion) Agreements.OnPlayerLeft(m.From, m.Name); // can't keep an agreement across versions
                    if (protocol != Config.ProtocolVersion && Warned.Add(m.From))
                    {
                        IspNetwork.PendingNotices.Add(m.Name + " runs ISP Business " + version + ", you run " + Config.ModVersion +
                            ". ISP agreements between you are off until you both use the same version.");
                    }
                    // Always answer a greeting (a reply is never answered), so a player who loads in later
                    // still learns about everyone already in the game.
                    if (!isReply) SendHello(m.From, true);
                    if (protocol == Config.ProtocolVersion) SendClaimsTo(m.From);
                    break;
                case MsgClaim:
                    if (!PeerCompatible(m.From)) break;
                    uint product = (uint)ReadInt(m.Data, ref pos);
                    bool claim = pos < m.Data.Length && m.Data[pos++] == 1;
                    Deals.OnRemoteClaim(m.From, product, claim);
                    break;
                case MsgCompanyClaim:
                    if (!PeerCompatible(m.From)) break;
                    uint company = (uint)ReadInt(m.Data, ref pos);
                    bool cclaim = pos < m.Data.Length && m.Data[pos++] == 1;
                    Deals.OnRemoteCompanyClaim(m.From, company, cclaim);
                    break;
                case MsgCoverage:
                    if (!PeerCompatible(m.From)) break;
                    int settlement = ReadInt(m.Data, ref pos);
                    double residents = ReadDouble(m.Data, ref pos);
                    Municipal.OnRemoteCoverage(m.From, settlement, residents);
                    break;
                case MsgOffer:
                {
                    if (!PeerCompatible(m.From)) break;
                    bool open = pos < m.Data.Length && m.Data[pos++] == 1;
                    double std = ReadDouble(m.Data, ref pos);
                    double prem = ReadDouble(m.Data, ref pos);
                    double spare = ReadDouble(m.Data, ref pos);
                    uint companyId = (uint)ReadInt(m.Data, ref pos);
                    Agreements.OnOffer(m.From, m.Name, open, std, prem, spare, companyId);
                    break;
                }
                case MsgConnect:
                {
                    if (!PeerCompatible(m.From)) break;
                    int token = ReadInt(m.Data, ref pos);
                    byte tier = pos < m.Data.Length ? m.Data[pos++] : (byte)0;
                    double price = ReadDouble(m.Data, ref pos);
                    Agreements.OnConnect(m.From, m.Name, token, tier, price);
                    break;
                }
                case MsgReply:
                {
                    if (!PeerCompatible(m.From)) break;
                    int token = ReadInt(m.Data, ref pos);
                    bool ok = pos < m.Data.Length && m.Data[pos++] == 1;
                    string reason = ReadString(m.Data, ref pos);
                    Agreements.OnReply(m.From, m.Name, token, ok, reason);
                    break;
                }
                case MsgUsage:
                {
                    int token = ReadInt(m.Data, ref pos);
                    Agreements.OnUsage(m.From, token, ReadDouble(m.Data, ref pos));
                    break;
                }
                case MsgService:
                {
                    int token = ReadInt(m.Data, ref pos);
                    Agreements.OnService(m.From, token, ReadDouble(m.Data, ref pos));
                    break;
                }
                case MsgEnd:
                {
                    int token = ReadInt(m.Data, ref pos);
                    byte reason = pos < m.Data.Length ? m.Data[pos++] : (byte)0;
                    Agreements.OnEnd(m.From, m.Name, token, reason);
                    break;
                }
            }
        }

        /// <summary>Tells every other player that we took (or released) the ISP deal for a product.</summary>
        public static void BroadcastClaim(uint productId, bool claim)
        {
            GameSettings gs = GameSettings.Instance;
            if (!Registered || gs == null || !gs.IsNetworkMode) return;
            try { Mod.SendNetworkMessage(ClaimBytes(MsgClaim, productId, claim), ModBehaviour.MessageTarget.EveryoneButMe, 0); }
            catch (Exception e) { Debug.LogError("[ISP Business] Send failed: " + e.Message); }
        }

        /// <summary>Tells every other player that we took (or released) a company-wide ISP deal.</summary>
        public static void BroadcastCompanyClaim(uint companyId, bool claim)
        {
            GameSettings gs = GameSettings.Instance;
            if (!Registered || gs == null || !gs.IsNetworkMode) return;
            try { Mod.SendNetworkMessage(ClaimBytes(MsgCompanyClaim, companyId, claim), ModBehaviour.MessageTarget.EveryoneButMe, 0); }
            catch (Exception e) { Debug.LogError("[ISP Business] Send failed: " + e.Message); }
        }

        /// <summary>Tells every other player how many residents we cover in a settlement.</summary>
        public static void BroadcastCoverage(int settlement, double residents)
        {
            GameSettings gs = GameSettings.Instance;
            if (!Registered || gs == null || !gs.IsNetworkMode) return;
            try { Mod.SendNetworkMessage(CoverageBytes(settlement, residents), ModBehaviour.MessageTarget.EveryoneButMe, 0); }
            catch (Exception e) { Debug.LogError("[ISP Business] Send failed: " + e.Message); }
        }

        private static byte[] CoverageBytes(int settlement, double residents)
        {
            var b = new List<byte> { MsgCoverage };
            WriteInt(b, settlement);
            WriteDouble(b, residents);
            return b.ToArray();
        }

        // ---------- agreements between players

        private static byte[] OfferBytes()
        {
            GameSettings gs = GameSettings.Instance;
            var b = new List<byte> { MsgOffer, Agreements.OfferOpen ? (byte)1 : (byte)0 };
            WriteDouble(b, Agreements.StandardPrice);
            WriteDouble(b, Agreements.PremiumPrice);
            WriteDouble(b, Agreements.Spare());
            WriteInt(b, unchecked((int)(gs != null && gs.MyCompany != null ? gs.MyCompany.ID : 0u)));
            return b.ToArray();
        }

        /// <summary>Sends our ISP offer (or that it's closed) to every other player.</summary>
        public static void BroadcastOffer()
        {
            GameSettings gs = GameSettings.Instance;
            if (!Registered || gs == null || !gs.IsNetworkMode) return;
            try { Mod.SendNetworkMessage(OfferBytes(), ModBehaviour.MessageTarget.EveryoneButMe, 0); }
            catch (Exception e) { Debug.LogError("[ISP Business] Send failed: " + e.Message); }
        }

        public static void SendConnect(byte provider, int token, byte tier, double price)
        {
            var b = new List<byte> { MsgConnect };
            WriteInt(b, token);
            b.Add(tier);
            WriteDouble(b, price);
            Send(b.ToArray(), provider);
        }

        public static void SendReply(byte customer, int token, bool accepted, string reason)
        {
            var b = new List<byte> { MsgReply };
            WriteInt(b, token);
            b.Add(accepted ? (byte)1 : (byte)0);
            WriteString(b, reason);
            Send(b.ToArray(), customer);
        }

        public static void SendUsage(byte provider, int token, double mbps) { SendTokenDouble(MsgUsage, provider, token, mbps); }
        public static void SendService(byte customer, int token, double share) { SendTokenDouble(MsgService, customer, token, share); }

        private static void SendTokenDouble(byte type, byte target, int token, double v)
        {
            if (!Registered) return;
            var b = new List<byte> { type };
            WriteInt(b, token);
            WriteDouble(b, v);
            Send(b.ToArray(), target);
        }

        public static void SendEnd(byte target, int token, byte reason)
        {
            if (!Registered) return;
            var b = new List<byte> { MsgEnd };
            WriteInt(b, token);
            b.Add(reason);
            Send(b.ToArray(), target);
        }

        private static void SendClaimsTo(byte target)
        {
            if (Agreements.OfferOpen) Send(OfferBytes(), target);
            foreach (var kv in Municipal.MyCoverages().ToList()) Send(CoverageBytes(kv.Key, kv.Value), target);
            foreach (uint id in Deals.MyClaims().ToList()) Send(ClaimBytes(MsgClaim, id, true), target);
            foreach (uint id in Deals.MyCompanyClaims().ToList()) Send(ClaimBytes(MsgCompanyClaim, id, true), target);
        }

        private static byte[] ClaimBytes(byte type, uint id, bool claim)
        {
            var b = new List<byte> { type };
            WriteInt(b, unchecked((int)id));
            b.Add(claim ? (byte)1 : (byte)0);
            return b.ToArray();
        }

        public static bool PeerCompatible(byte playerId)
        {
            return Peers.TryGetValue(playerId, out var v) && v.Key == Config.ProtocolVersion;
        }

        private static void SendHello(byte target, bool reply)
        {
            var b = new List<byte> { MsgHello };
            WriteInt(b, Config.ProtocolVersion);
            WriteString(b, Config.ModVersion);
            b.Add(reply ? (byte)1 : (byte)0);
            Send(b.ToArray(), target);
        }

        private static void Send(byte[] data, byte target)
        {
            try { Mod.SendNetworkMessage(data, ModBehaviour.MessageTarget.Specifically, target); }
            catch (Exception e) { Debug.LogError("[ISP Business] Send failed: " + e.Message); }
        }

        // ---------- byte helpers (BinaryWriter is blocked for mods)

        public static void WriteInt(List<byte> b, int v) { b.AddRange(BitConverter.GetBytes(v)); }
        public static void WriteDouble(List<byte> b, double v) { b.AddRange(BitConverter.GetBytes(v)); }
        public static void WriteString(List<byte> b, string s)
        {
            byte[] raw = Encoding.UTF8.GetBytes(s ?? "");
            WriteInt(b, raw.Length);
            b.AddRange(raw);
        }

        public static int ReadInt(byte[] d, ref int pos)
        {
            if (pos + 4 > d.Length) { pos = d.Length; return 0; }
            int v = BitConverter.ToInt32(d, pos); pos += 4; return v;
        }

        public static double ReadDouble(byte[] d, ref int pos)
        {
            if (pos + 8 > d.Length) { pos = d.Length; return 0; }
            double v = BitConverter.ToDouble(d, pos); pos += 8; return v;
        }

        public static string ReadString(byte[] d, ref int pos)
        {
            int n = ReadInt(d, ref pos);
            if (n <= 0 || pos + n > d.Length) return "";
            string s = Encoding.UTF8.GetString(d, pos, n); pos += n; return s;
        }
    }
}
