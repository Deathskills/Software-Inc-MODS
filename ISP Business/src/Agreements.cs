// ISP Business - ISP agreements between players (spec 15, 16.2, 16.3, 16.5; Milestone E).
//
// Provider (the player who sells ISP service):
//  - Opens an offer in the ISP window (Players tab) with a standard and a premium price, set as a share of
//    the vanilla ISP rate. The offer, with the spare capacity of the pool that will carry it, is sent to
//    every player running the same ISP Business version, and again once a day while it's open.
//  - Each customer becomes an ISP process ("p2p:<player id>") on one of the provider's pools, like a deal.
//    The provider pays transit on it, its servers draw power for it, and it shares the pool's capacity.
//  - Every hour the provider tells each customer what share of their traffic it delivered.
//  - Good and bad months change the provider's business reputation, as for deals.
// Customer (the player who buys it):
//  - Picks a provider and a tier. The provider's game accepts it if the offer and price still stand.
//  - Every hour, the internet traffic your own ISP pools don't carry goes to the provider. What the
//    provider delivers comes off the vanilla "Internet" bill, and you pay the provider for it at the
//    agreed price, once a day ("Player ISP": your Bills, their Deals income). The game can't cut a
//    company's internet, so the part the provider doesn't deliver stays on the vanilla ISP bill.
//  - Leaving costs a reconnection fee, paid to the provider (spec 15.3).
//  - Premium: if a day's delivery falls under Config.PremiumMinService, the agreement ends at once, with
//    no fee, and you're back on the vanilla ISP (spec 15.4). The provider loses reputation.
// Ending without a fee or penalty: either player leaves the game, the provider's company closes, or the
// provider drops the customer (that costs the provider reputation). Agreements are not saved: loading a
// game starts a new session, so everyone starts on the vanilla ISP (spec 16.3).
// One side decides: the customer's game decides billing, fees and the premium cutoff; the provider's game
// decides acceptance, capacity and its own reputation. Money only moves as vanilla transactions made by
// the paying player's game, which vanilla sends to everyone.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ISPBusiness
{
    /// <summary>Another player's open ISP offer, as they last sent it.</summary>
    public sealed class PlayerOffer
    {
        public byte Player;
        public string Name;
        public uint CompanyId;
        public double Standard;   // share of the vanilla ISP rate
        public double Premium;
        public double Spare;      // Mbps the provider's pool has free
    }

    /// <summary>Provider side: one player you serve.</summary>
    public sealed class PlayerCustomer
    {
        public byte Player;
        public string Name;
        public int Token;
        public byte Tier;
        public double Price;       // share of the vanilla ISP rate, fixed when it started
        public double Usage;       // Mbps they asked for in their last hour
        public double Share = 1;   // share delivered in the last hour
        public double MonthDemand, MonthDelivered, MonthHours, MonthEarned;
        public double LastFulfilment = 1, LastEarned;
        public string Key { get { return Agreements.KeyPrefix + Player.ToString(CultureInfo.InvariantCulture); } }
    }

    /// <summary>Customer side: your agreement with a provider.</summary>
    public sealed class PlayerAgreement
    {
        public byte Provider;
        public string Name;
        public uint CompanyId;
        public int Token;
        public byte Tier;
        public double Price;       // share of the vanilla ISP rate
        public double Share = 1;   // share the provider delivered in its last hour
        public double Need, Got;   // last hour, Mbps
        public double Unpaid;
        public double DayDemand, DayDelivered;
        public double MonthDemand, MonthDelivered, MonthHours, MonthCost, MonthSaved;
        public double LastFulfilment = 1, LastCost, LastSaved, LastAvgMbps;
    }

    public static class Agreements
    {
        public const byte Standard = 0, Premium = 1;
        public const string KeyPrefix = "p2p:";
        public const string Bill = "Player ISP";
        public const string FeeBill = "ISP reconnection fee";

        // End reasons (sent with MsgEnd)
        public const byte EndCustomerLeft = 1, EndPremiumFailed = 2, EndProviderDropped = 3, EndProviderClosed = 4, EndNotWanted = 5;

        // ---- provider settings (saved)
        public static bool OfferOpen;
        public static double StandardPrice = Config.PlayerStandardDefault;
        public static double PremiumPrice = Config.PlayerPremiumDefault;
        public static string OfferPool;       // null: the pool with the most spare capacity

        // ---- session state (not saved)
        public static readonly Dictionary<byte, PlayerOffer> Offers = new Dictionary<byte, PlayerOffer>();
        public static readonly Dictionary<byte, PlayerCustomer> Customers = new Dictionary<byte, PlayerCustomer>();
        public static PlayerAgreement Current;
        private static byte _pendingProvider;
        private static int _pendingToken;
        private static byte _pendingTier;
        private static double _pendingPrice;
        private static float _pendingSince = -1f;

        /// <summary>Changes whenever an agreement, an offer or a month changes: the window rebuilds on it.</summary>
        public static int Version;
        private static int _custDay = int.MinValue, _custMonth = int.MinValue, _provMonth = int.MinValue, _offerDay = int.MinValue;
        private static readonly System.Random Rng = new System.Random();

        public static bool Pending { get { return _pendingSince >= 0f; } }
        public static byte PendingProvider { get { return _pendingProvider; } }

        public static void Reset()
        {
            OfferOpen = false;
            StandardPrice = Config.PlayerStandardDefault;
            PremiumPrice = Config.PlayerPremiumDefault;
            OfferPool = null;
            ResetSession();
        }

        /// <summary>A new multiplayer session (or none): every agreement is gone, nobody is charged.</summary>
        public static void ResetSession()
        {
            foreach (PlayerCustomer c in Customers.Values) IspNetwork.RemoveProcess(c.Key);
            Offers.Clear();
            Customers.Clear();
            Current = null;
            _pendingSince = -1f;
            _custDay = _custMonth = _provMonth = _offerDay = int.MinValue;
            Version++;
        }

        public static bool InSession
        {
            get { GameSettings gs = GameSettings.Instance; return gs != null && gs.IsNetworkMode && Net.Registered; }
        }

        public static string TierName(byte tier) { return tier == Premium ? "Premium" : "Standard"; }

        /// <summary>$ per Mbps per month for a share of the vanilla ISP rate.</summary>
        public static double Dollars(double share) { return share * Server.GetISPCost(); }

        private static Company CompanyById(uint id)
        {
            GameSettings gs = GameSettings.Instance;
            return gs != null && gs.simulation != null ? gs.simulation.GetCompany(id) : null;
        }

        // ---------- provider: offer

        public static string ResolvedPool()
        {
            if (OfferPool != null && IspNetwork.Pools.Contains(OfferPool)) return OfferPool;
            return IspNetwork.BestPool();
        }

        public static double Spare()
        {
            string pool = ResolvedPool();
            if (pool == null || !IspNetwork.State.TryGetValue(pool, out PoolState st) || !st.Up) return 0;
            return Math.Max(0, st.Capacity - st.Load);
        }

        public static bool SetOfferOpen(bool open, out string error)
        {
            error = null;
            if (open && IspNetwork.Pools.Count == 0) { error = "You have no ISP pool. Switch a server group to ISP mode first."; return false; }
            OfferOpen = open;
            Version++;
            Net.BroadcastOffer();
            return true;
        }

        public static void SetPrices(double standard, double premium)
        {
            StandardPrice = Math.Max(0.05, standard);
            PremiumPrice = Math.Max(0.05, premium);
            Version++;
            if (OfferOpen) Net.BroadcastOffer();
        }

        public static void SetPool(string pool)
        {
            OfferPool = pool;
            string resolved = ResolvedPool();
            if (resolved != null) foreach (PlayerCustomer c in Customers.Values) IspNetwork.SetAssignment(c.Key, resolved);
            Version++;
        }

        // ---------- customer: connect and leave

        public static bool Connect(PlayerOffer o, byte tier, out string error)
        {
            error = null;
            if (!InSession) { error = "Agreements between players only work in a multiplayer game."; return false; }
            if (Current != null) { error = "You already have an ISP agreement with " + Current.Name + ". Leave it first."; return false; }
            if (Pending) { error = "You are already waiting for an answer from another provider."; return false; }
            if (!Net.PeerCompatible(o.Player)) { error = o.Name + " doesn't run the same ISP Business version as you."; return false; }
            if (Customers.ContainsKey(o.Player)) { error = o.Name + " is your customer. Two players can't be each other's ISP."; return false; }
            _pendingProvider = o.Player;
            _pendingToken = Rng.Next(1, int.MaxValue);
            _pendingTier = tier;
            _pendingPrice = tier == Premium ? o.Premium : o.Standard;
            _pendingSince = Time.realtimeSinceStartup;
            Net.SendConnect(o.Player, _pendingToken, tier, _pendingPrice);
            Version++;
            return true;
        }

        /// <summary>The fee for leaving now: Config.ReconnectFeeMonths of service at your recent use, at least Config.ReconnectFeeMin.</summary>
        public static double LeaveFee()
        {
            PlayerAgreement a = Current;
            if (a == null) return 0;
            double avg = Math.Max(a.LastAvgMbps, a.MonthHours > 0 ? a.MonthDelivered / a.MonthHours : 0);
            return Math.Max(Config.ReconnectFeeMin, Config.ReconnectFeeMonths * avg * Dollars(a.Price));
        }

        /// <summary>The customer leaves on purpose and pays the reconnection fee to the provider.</summary>
        public static void Leave()
        {
            PlayerAgreement a = Current;
            if (a == null) return;
            double fee = LeaveFee();
            Company prov = CompanyById(a.CompanyId);
            Company me = GameSettings.Instance.MyCompany;
            if (fee > 0 && prov != null && !prov.Bankrupt)
            {
                me.MakeTransaction(-(float)fee, Company.TransactionCategory.Bills, true, FeeBill);
                prov.MakeTransaction((float)fee, Company.TransactionCategory.Deals, true, FeeBill, true);
            }
            EndAsCustomer(EndCustomerLeft, "You left your ISP agreement with " + a.Name + " and paid a reconnection fee of " + ((float)fee).Currency() + ". Your company is back on the vanilla ISP.");
        }

        /// <summary>Ends the customer's agreement on this machine and tells the provider. Pays what's owed.</summary>
        private static void EndAsCustomer(byte reason, string notice)
        {
            PlayerAgreement a = Current;
            if (a == null) return;
            PayProvider();
            Current = null;
            Version++;
            Net.SendEnd(a.Provider, a.Token, reason);
            Debug.Log("[ISP Business] Player ISP agreement with " + a.Name + " ended (reason " + reason + ").");
            if (notice != null) IspNetwork.PendingNotices.Add(notice);
        }

        private static void PayProvider()
        {
            PlayerAgreement a = Current;
            if (a == null || a.Unpaid < 0.01) return;
            Company prov = CompanyById(a.CompanyId);
            float amount = (float)a.Unpaid;
            a.Unpaid = 0;
            if (prov == null) return; // provider gone: nothing to pay
            GameSettings.Instance.MyCompany.MakeTransaction(-amount, Company.TransactionCategory.Bills, true, Bill);
            prov.MakeTransaction(amount, Company.TransactionCategory.Deals, true, Bill, true);
        }

        // ---------- provider: drop a customer

        public static void Drop(PlayerCustomer c)
        {
            if (c == null || !Customers.Remove(c.Player)) return;
            IspNetwork.RemoveProcess(c.Key);
            Net.SendEnd(c.Player, c.Token, EndProviderDropped);
            double weight = Math.Min(1.0, Math.Sqrt(Math.Max(0, c.Usage) / Config.RepReferenceMbps));
            GameSettings.Instance.MyCompany.ChangeBusinessRep((float)(Config.RepCancel * Math.Max(0.1, weight)), "ISP");
            Version++;
            Net.BroadcastOffer();
        }

        /// <summary>Every frame (Net.Tick): stop waiting for a provider that doesn't answer.</summary>
        public static void Tick()
        {
            if (_pendingSince >= 0f && Time.realtimeSinceStartup - _pendingSince > Config.ConnectTimeoutSeconds)
            {
                _pendingSince = -1f;
                Version++;
                IspNetwork.PendingNotices.Add("The provider didn't answer your ISP request. You stay on the vanilla ISP.");
            }
        }

        // ---------- hourly (from IspNetwork.OnHour)

        /// <summary>Provider: sets each customer's process demand before routing.</summary>
        public static void RefreshDemand()
        {
            if (Customers.Count == 0) return;
            string pool = ResolvedPool();
            foreach (PlayerCustomer c in Customers.Values)
            {
                if (!IspNetwork.Processes.TryGetValue(c.Key, out IspProcess p))
                {
                    p = new IspProcess { Key = c.Key };
                    IspNetwork.Processes[c.Key] = p;
                }
                p.Label = "Player " + c.Name + " (" + TierName(c.Tier).ToLowerInvariant() + ")";
                p.Demand = Math.Max(0, c.Usage);
                if (IspNetwork.GetAssignment(c.Key) == null && pool != null) IspNetwork.SetAssignment(c.Key, pool);
            }
        }

        /// <summary>Customer: moves the traffic your pools don't carry to the provider and books what it costs.</summary>
        public static void CustomerHour(IspProcess own, double ispRate, double hoursPerMonth)
        {
            PlayerAgreement a = Current;
            if (a == null) return;
            GameSettings gs = GameSettings.Instance;
            int now = GameClock.Stamp;
            int day = now / (24 * 60);
            int month = GameClock.MonthIndex;

            Company prov = CompanyById(a.CompanyId);
            if (prov == null || prov.Bankrupt)
            {
                a.Unpaid = 0;
                EndAsCustomer(EndProviderClosed, a.Name + "'s company closed, so your ISP agreement ended without a fee. Your company is back on the vanilla ISP.");
                return;
            }

            // Day and month boundaries first, with the totals of the day / month that just ended.
            if (_custDay != day)
            {
                if (_custDay != int.MinValue)
                {
                    PayProvider();
                    if (a.Tier == Premium && a.DayDemand > 0 && a.DayDelivered / a.DayDemand < Config.PremiumMinService)
                    {
                        double f = a.DayDelivered / a.DayDemand;
                        EndAsCustomer(EndPremiumFailed, a.Name + " delivered only " + (f * 100).ToString("0", CultureInfo.InvariantCulture) +
                            "% of your internet yesterday, under the premium guarantee of " + (Config.PremiumMinService * 100).ToString("0", CultureInfo.InvariantCulture) +
                            "%. The agreement ended without a fee, and your company is back on the vanilla ISP at the normal rate.");
                        return;
                    }
                }
                a.DayDemand = a.DayDelivered = 0;
                _custDay = day;
            }
            if (_custMonth != month)
            {
                if (_custMonth != int.MinValue)
                {
                    a.LastFulfilment = a.MonthDemand > 0 ? Math.Min(1.0, a.MonthDelivered / a.MonthDemand) : 1.0;
                    a.LastCost = a.MonthCost;
                    a.LastSaved = a.MonthSaved;
                    a.LastAvgMbps = a.MonthHours > 0 ? a.MonthDelivered / a.MonthHours : 0;
                    Version++;
                }
                a.MonthDemand = a.MonthDelivered = a.MonthHours = a.MonthCost = a.MonthSaved = 0;
                _custMonth = month;
            }

            double need = Math.Max(0, own.Demand - own.Delivered);
            double got = need * Math.Max(0, Math.Min(1, a.Share));
            Net.SendUsage(a.Provider, a.Token, need);
            a.Need = need;
            a.Got = got;
            if (got > 0)
            {
                double refund = got * ispRate / hoursPerMonth;
                double cost = got * Dollars(a.Price) / hoursPerMonth;
                gs.ServerCost = (float)Math.Max(0.0, gs.ServerCost - refund);
                a.Unpaid += cost;
                a.MonthCost += cost;
                a.MonthSaved += refund - cost;
            }
            a.DayDemand += need; a.DayDelivered += got;
            a.MonthDemand += need; a.MonthDelivered += got; a.MonthHours += 1;
        }

        /// <summary>Provider: transit, month results and reputation for player customers; tells each one
        /// what share it delivered. Called after routing.</summary>
        public static void ProviderHour(double hoursPerMonth, double transitRate)
        {
            GameSettings gs = GameSettings.Instance;
            int now = GameClock.Stamp;
            int day = now / (24 * 60);
            int month = GameClock.MonthIndex;

            if (_provMonth != month)
            {
                if (_provMonth != int.MinValue)
                {
                    foreach (PlayerCustomer c in Customers.Values)
                    {
                        c.LastEarned = c.MonthEarned;
                        if (c.MonthDemand > 0)
                        {
                            double f = Math.Min(1.0, c.MonthDelivered / c.MonthDemand);
                            c.LastFulfilment = f;
                            double weight = Math.Min(1.0, Math.Sqrt(c.MonthDemand / Math.Max(1.0, c.MonthHours) / Config.RepReferenceMbps));
                            if (f >= 0.98) gs.MyCompany.ChangeBusinessRep((float)(Config.RepGoodMonth * weight), "ISP");
                            else if (f < 0.90) gs.MyCompany.ChangeBusinessRep((float)(Config.RepBadMonth * weight), "ISP");
                        }
                        else c.LastFulfilment = 1.0;
                        c.MonthDemand = c.MonthDelivered = c.MonthHours = c.MonthEarned = 0;
                    }
                    Version++;
                }
                _provMonth = month;
            }

            double transit = 0;
            foreach (PlayerCustomer c in Customers.Values)
            {
                IspNetwork.Processes.TryGetValue(c.Key, out IspProcess p);
                double demand = p != null ? p.Demand : 0;
                double delivered = p != null ? p.Delivered : 0;
                c.Share = demand > 0 ? delivered / demand : (p != null && p.ServedBy != null ? 1.0 : 0.0);
                transit += delivered * transitRate / hoursPerMonth;
                double earned = delivered * Dollars(c.Price) / hoursPerMonth;   // what the customer's game pays for it
                c.MonthEarned += earned;
                IspNetwork.MonthRevenue += earned;
                c.MonthDemand += demand; c.MonthDelivered += delivered; c.MonthHours += 1;
                Net.SendService(c.Player, c.Token, c.Share);
            }
            if (transit > 0)
            {
                gs.ServerCost += (float)transit;
                IspNetwork.MonthTransit += transit;
            }

            if (OfferOpen && _offerDay != day)
            {
                _offerDay = day;
                Net.BroadcastOffer(); // fresh spare capacity once a day
            }
        }

        // ---------- messages (Net.cs decodes them; only called for players with the same protocol)

        public static void OnOffer(byte from, string name, bool open, double standard, double premium, double spare, uint companyId)
        {
            if (!open) { if (Offers.Remove(from)) Version++; return; }
            if (!Offers.TryGetValue(from, out PlayerOffer o)) { o = new PlayerOffer { Player = from }; Offers[from] = o; Version++; }
            o.Name = name; o.CompanyId = companyId; o.Standard = standard; o.Premium = premium; o.Spare = spare;
        }

        /// <summary>Provider: a player asks to become a customer. This game decides.</summary>
        public static void OnConnect(byte from, string name, int token, byte tier, double price)
        {
            string reason = null;
            double expected = tier == Premium ? PremiumPrice : StandardPrice;
            if (!OfferOpen) reason = "isn't offering ISP service any more";
            else if (IspNetwork.Pools.Count == 0) reason = "has no ISP pool";
            else if (Math.Abs(expected - price) > 1e-6) reason = "changed the price. Check the new price and try again";
            else if (Current != null && Current.Provider == from) reason = "is your customer. Two players can't be each other's ISP";
            else if (Customers.ContainsKey(from)) reason = "already serves you";
            if (reason != null)
            {
                Net.SendReply(from, token, false, reason);
                return;
            }
            Customers[from] = new PlayerCustomer { Player = from, Name = name, Token = token, Tier = tier, Price = price };
            Net.SendReply(from, token, true, "");
            Version++;
            IspNetwork.PendingNotices.Add(name + " is now your ISP customer (" + TierName(tier).ToLowerInvariant() + ", " + ((float)Dollars(price)).Currency() +
                " per Mbps a month). Their traffic is on " + (ResolvedPool() ?? "your ISP pool") + ".");
            Net.BroadcastOffer();
        }

        /// <summary>Customer: the provider's answer.</summary>
        public static void OnReply(byte from, string name, int token, bool accepted, string reason)
        {
            bool mine = _pendingSince >= 0f && _pendingProvider == from && _pendingToken == token;
            if (!mine)
            {
                // We stopped waiting (timeout): undo it on their side, without a fee.
                if (accepted) Net.SendEnd(from, token, EndNotWanted);
                return;
            }
            _pendingSince = -1f;
            Version++;
            if (!accepted)
            {
                IspNetwork.PendingNotices.Add(name + " " + reason + ". You stay on the vanilla ISP.");
                return;
            }
            Offers.TryGetValue(from, out PlayerOffer o);
            Current = new PlayerAgreement
            {
                Provider = from, Name = name, CompanyId = o != null ? o.CompanyId : 0u, Token = token,
                Tier = _pendingTier, Price = _pendingPrice,
            };
            if (CompanyById(Current.CompanyId) == null)
            {
                Current = null;
                Net.SendEnd(from, token, EndNotWanted);
                IspNetwork.PendingNotices.Add("Could not find " + name + "'s company, so the agreement was not made. You stay on the vanilla ISP.");
                return;
            }
            _custDay = _custMonth = int.MinValue;
            IspNetwork.PendingNotices.Add("You are now connected to " + name + "'s ISP (" + TierName(Current.Tier).ToLowerInvariant() + ", " +
                ((float)Dollars(Current.Price)).Currency() + " per Mbps a month). Internet traffic your own ISP pools don't carry now goes through them.");
        }

        public static void OnUsage(byte from, int token, double mbps)
        {
            if (Customers.TryGetValue(from, out PlayerCustomer c) && c.Token == token) c.Usage = Math.Max(0, mbps);
        }

        public static void OnService(byte from, int token, double share)
        {
            if (Current != null && Current.Provider == from && Current.Token == token) Current.Share = Math.Max(0, Math.Min(1, share));
        }

        public static void OnEnd(byte from, string name, int token, byte reason)
        {
            // We are the customer: the provider ended it.
            if (Current != null && Current.Provider == from && Current.Token == token)
            {
                PayProvider();
                Current = null;
                Version++;
                IspNetwork.PendingNotices.Add(name + " stopped providing your ISP service. The agreement ended without a fee, and your company is back on the vanilla ISP.");
                return;
            }
            // We are the provider: the customer ended it.
            if (Customers.TryGetValue(from, out PlayerCustomer c) && c.Token == token)
            {
                Customers.Remove(from);
                IspNetwork.RemoveProcess(c.Key);
                Version++;
                if (reason == EndPremiumFailed)
                {
                    double weight = Math.Min(1.0, Math.Sqrt(Math.Max(0, c.Usage) / Config.RepReferenceMbps));
                    GameSettings.Instance.MyCompany.ChangeBusinessRep((float)(Config.RepBadMonth * Math.Max(0.1, weight)), "ISP");
                    IspNetwork.PendingNotices.Add(name + " left your ISP: you broke the premium guarantee yesterday. You lost some business reputation.");
                }
                else if (reason == EndCustomerLeft)
                    IspNetwork.PendingNotices.Add(name + " left your ISP and paid you a reconnection fee.");
                else if (reason != EndNotWanted)
                    IspNetwork.PendingNotices.Add(name + " is no longer your ISP customer.");
                Net.BroadcastOffer();
            }
        }

        /// <summary>A player left the game (or stopped being compatible): their agreements end with no fee or penalty.</summary>
        public static void OnPlayerLeft(byte player, string name)
        {
            if (Offers.Remove(player)) Version++;
            if (_pendingSince >= 0f && _pendingProvider == player) { _pendingSince = -1f; Version++; }
            if (Current != null && Current.Provider == player)
            {
                string who = Current.Name;
                PayProvider();   // the hours already used are still owed
                Current = null;
                Version++;
                IspNetwork.PendingNotices.Add(who + " left the game, so your ISP agreement ended without a fee. Your company is back on the vanilla ISP.");
            }
            if (Customers.TryGetValue(player, out PlayerCustomer c))
            {
                Customers.Remove(player);
                IspNetwork.RemoveProcess(c.Key);
                Version++;
                IspNetwork.PendingNotices.Add(c.Name + " left the game, so their ISP agreement with you ended. No penalty.");
            }
        }

        /// <summary>The mod is turning off: end every agreement, without fees, and tell the other players.</summary>
        public static void EndAll()
        {
            if (Current != null)
            {
                PayProvider();
                Net.SendEnd(Current.Provider, Current.Token, EndNotWanted);
                Current = null;
            }
            foreach (PlayerCustomer c in Customers.Values.ToList())
            {
                Net.SendEnd(c.Player, c.Token, EndProviderClosed);
                IspNetwork.RemoveProcess(c.Key);
            }
            Customers.Clear();
            if (OfferOpen) { OfferOpen = false; Net.BroadcastOffer(); }
            Version++;
        }

        // ---------- save data (the provider's offer settings only)

        public static string PackSettings()
        {
            return string.Join(Pack.Field.ToString(), new[] { OfferOpen ? "1" : "0", Pack.D(StandardPrice), Pack.D(PremiumPrice), OfferPool ?? "" });
        }

        public static void UnpackSettings(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            string[] f = s.Split(Pack.Field);
            if (f.Length < 4) return;
            OfferOpen = f[0] == "1";
            StandardPrice = Pack.D(f[1]) > 0 ? Pack.D(f[1]) : Config.PlayerStandardDefault;
            PremiumPrice = Pack.D(f[2]) > 0 ? Pack.D(f[2]) : Config.PlayerPremiumDefault;
            OfferPool = f[3].Length > 0 ? f[3] : null;
            Version++;
        }
    }
}
