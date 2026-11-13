// ISP Business - corporate product ISP deals (spec 5.2, 5.4, 6, 13, Milestone B).
//
//  - Each month, up to 10 AI products that need internet (>= 1 Mbps) offer an ISP deal, ranked by value.
//    You need a receptionist, as for vanilla deals, and deals must be on in the difficulty settings.
//  - The price per Mbps is locked when the offer is made: $11.45 x the vanilla business-star multiplier.
//  - An offer stays open for 24 months. Accept or decline it in the ISP window.
//  - An accepted deal becomes an ISP process on one of your pools. You're paid every hour for the Mbps
//    you actually deliver, and you pay wholesale transit on it. Electricity comes from your servers.
//  - Performance starts at 1.0 and changes monthly with delivered/requested bandwidth (spec 13.3).
//    At 0 the customer cancels. Good months raise business reputation, bad months lower it.
//  - A deal ends without penalty when the product leaves the market or stays under 1 Mbps for 6 months.
//  - Hosting and ISP are separate: a product can have a hosting deal and an ISP deal at the same time.
//  - Multiplayer: offers are made on each player's own machine. When a player accepts, they tell the
//    others (claim), so nobody else is offered or keeps that product. Ties go to the lower player id.
//    Claims are re-sent to players who join, and dropped when a player leaves.
// Nothing here goes into the vanilla deal list, so saves stay loadable without the mod.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ISPBusiness
{
    public sealed class IspDeal
    {
        public int Id;
        public uint ProductId;
        public string ProductName;
        public string CompanyName;
        public double Rate;            // $ per Mbps per month, locked at offer time
        public int OfferedAt;          // SDateTime stamp
        public int AcceptedAt;         // 0 while it's an offer
        public double Performance = 1.0;
        public double MonthDemand;     // Mbps-hours requested this month
        public double MonthDelivered;  // Mbps-hours delivered this month
        public int LowSince = -1;      // stamp since the product needs less than MinOfferMbps, or -1
        public double TotalRevenue;
        public double LastFulfilment = 1.0;
        public double EstimatedMbps;   // demand when offered (offers) / last hour (deals)

        public bool Active { get { return AcceptedAt != 0; } }
        public string Key { get { return "deal:" + Id; } }
    }

    public static class Deals
    {
        public static readonly List<IspDeal> Offers = new List<IspDeal>();
        public static readonly List<IspDeal> Active = new List<IspDeal>();

        /// <summary>Products you declined: product id -> stamp. Not offered again for 6 months.</summary>
        private static readonly Dictionary<uint, int> Declined = new Dictionary<uint, int>();

        /// <summary>Products other players hold an ISP deal for: product id -> player id. Not saved.</summary>
        public static readonly Dictionary<uint, byte> OtherClaims = new Dictionary<uint, byte>();

        private static int _nextId = 1;
        private static int _lastMonth = int.MinValue;
        private static double _pendingRevenue;

        private static readonly Dictionary<uint, SoftwareProduct> _products = new Dictionary<uint, SoftwareProduct>();
        private static int _productsDay = int.MinValue;

        public static void Reset()
        {
            Offers.Clear();
            Active.Clear();
            Declined.Clear();
            OtherClaims.Clear();
            _products.Clear();
            _productsDay = int.MinValue;
            _nextId = 1;
            _lastMonth = int.MinValue;
            _pendingRevenue = 0;
        }

        // ---------- products

        private static void RefreshProducts(bool force)
        {
            int day = GameClock.Stamp / (24 * 60);
            if (!force && day == _productsDay) return;
            _productsDay = day;
            _products.Clear();
            GameSettings gs = GameSettings.Instance;
            if (gs == null || gs.simulation == null) return;
            foreach (SoftwareProduct p in gs.simulation.GetAllProducts(false))
                if (p != null) _products[p.ID] = p;
        }

        private static SoftwareProduct Product(uint id)
        {
            RefreshProducts(false);
            return _products.TryGetValue(id, out SoftwareProduct p) ? p : null;
        }

        /// <summary>Mbps the product needs right now (vanilla load x era factor; includes other mods' load changes).</summary>
        public static double DemandOf(SoftwareProduct p)
        {
            if (p == null || p.Userbase <= 0) return 0;
            return p.GetLoadRequirement() * 1f.BandwidthFactor(SDateTime.Now());
        }

        private static bool IsAiCustomer(SoftwareProduct p)
        {
            if (!(p.DevCompany is SimulatedCompany sc)) return false;
            if (sc.IsPlayerOwned()) return false;
            if (sc.IsSubsidiary() && sc.OwnerCompany != null && sc.OwnerCompany.Player) return false;
            return true;
        }

        // ---------- hourly

        /// <summary>Sets each deal's process demand for this hour. Called before routing.</summary>
        public static void RefreshDemand()
        {
            foreach (IspDeal d in Active)
            {
                SoftwareProduct p = Product(d.ProductId);
                double demand = DemandOf(p);
                d.EstimatedMbps = demand;
                if (!IspNetwork.Processes.TryGetValue(d.Key, out IspProcess proc))
                {
                    proc = new IspProcess { Key = d.Key };
                    IspNetwork.Processes[d.Key] = proc;
                }
                proc.Label = d.ProductName + " (" + d.CompanyName + ")";
                proc.Demand = demand;
            }
        }

        /// <summary>Pays deal revenue and transit for the hour, and handles the month boundary.</summary>
        public static void OnHour(double hoursPerMonth, double transitRate)
        {
            GameSettings gs = GameSettings.Instance;
            int now = GameClock.Stamp;
            double transit = 0;
            var ended = new List<KeyValuePair<IspDeal, string>>();

            foreach (IspDeal d in Active)
            {
                IspProcess proc;
                IspNetwork.Processes.TryGetValue(d.Key, out proc);
                double demand = proc != null ? proc.Demand : 0;
                double delivered = proc != null ? proc.Delivered : 0;

                double revenue = delivered * d.Rate / hoursPerMonth;
                _pendingRevenue += revenue;
                d.TotalRevenue += revenue;
                IspNetwork.MonthRevenue += revenue;
                transit += delivered * transitRate / hoursPerMonth;
                d.MonthDemand += demand;
                d.MonthDelivered += delivered;

                if (Product(d.ProductId) == null) { ended.Add(new KeyValuePair<IspDeal, string>(d, "the product left the market")); continue; }
                if (demand < Config.MinOfferMbps)
                {
                    if (d.LowSince < 0) d.LowSince = now;
                    else if (GameClock.MonthsBetween(d.LowSince, now) >= Config.DealEndMonths)
                        ended.Add(new KeyValuePair<IspDeal, string>(d, "it has needed less than " + Config.MinOfferMbps + " Mbps for " + Config.DealEndMonths + " months"));
                }
                else d.LowSince = -1;
            }

            if (transit > 0) gs.ServerCost += (float)transit;
            IspNetwork.MonthTransit += transit;
            if (_pendingRevenue >= 1.0)
            {
                gs.MyCompany.MakeTransaction(_pendingRevenue, Company.TransactionCategory.Contracts, true, "ISP service", true);
                _pendingRevenue = 0;
            }

            foreach (var kv in ended) End(kv.Key, "Your ISP deal for " + kv.Key.ProductName + " ended because " + kv.Value + ". No penalty.", false);

            int month = GameClock.MonthIndex;
            if (month != _lastMonth)
            {
                bool first = _lastMonth == int.MinValue;
                _lastMonth = month;
                if (!first)
                {
                    MonthlyReview();
                    MonthlyOffers();
                }
                ExpireOffers();
            }
        }

        // ---------- monthly

        private static void MonthlyReview()
        {
            Company me = GameSettings.Instance.MyCompany;
            foreach (IspDeal d in Active.ToList())
            {
                if (d.MonthDemand <= 0)
                {
                    // No demand this month: nothing was owed, so Performance and reputation don't move.
                    d.LastFulfilment = 1.0;
                }
                else
                {
                    double f = Math.Min(1.0, d.MonthDelivered / d.MonthDemand);
                    d.LastFulfilment = f;
                    d.Performance += Config.PerformanceChange(f);
                    double hours = Math.Max(1.0, GameClock.HoursPerMonth);
                    double weight = Math.Min(1.0, Math.Sqrt(d.MonthDemand / hours / Config.RepReferenceMbps));
                    if (f >= 0.98) me.ChangeBusinessRep((float)(Config.RepGoodMonth * weight), "ISP");
                    else if (f < 0.90) me.ChangeBusinessRep((float)(Config.RepBadMonth * weight), "ISP");
                }
                d.MonthDemand = d.MonthDelivered = 0;
                if (d.Performance <= 0)
                    End(d, "The customer cancelled your ISP deal for " + d.ProductName + " because of poor service (Performance reached 0).", true);
            }
        }

        private static void MonthlyOffers()
        {
            GameSettings gs = GameSettings.Instance;
            if (gs.simulation == null) return;
            if (gs.Difficulty.Deals < 0.5f) return; // deals are off in this game's difficulty settings
            RefreshProducts(true);
            int now = GameClock.Stamp;

            var taken = new HashSet<uint>(Active.Select(x => x.ProductId).Concat(Offers.Select(x => x.ProductId)));
            int stars = gs.MyCompany.BusinessStars;
            var candidates = new List<KeyValuePair<SoftwareProduct, double>>();
            foreach (SoftwareProduct p in _products.Values)
            {
                if (taken.Contains(p.ID) || OtherClaims.ContainsKey(p.ID)) continue;
                if (Declined.TryGetValue(p.ID, out int when) && GameClock.MonthsBetween(when, now) < 6.0) continue;
                if (!IsAiCustomer(p)) continue;
                double mbps = DemandOf(p);
                if (mbps < Config.MinOfferMbps) continue;
                candidates.Add(new KeyValuePair<SoftwareProduct, double>(p, mbps * UnityEngine.Random.Range(0.85f, 1f)));
            }

            int sent = 0;
            foreach (var c in candidates.OrderByDescending(x => x.Value))
            {
                if (sent >= Config.ProductOffersPerMonth) break;
                if (!DealWindow.FindReceptionTime().HasValue) break; // no receptionist with free time
                SoftwareProduct p = c.Key;
                Offers.Add(new IspDeal
                {
                    Id = _nextId++,
                    ProductId = p.ID,
                    ProductName = p.Name,
                    CompanyName = p.DevCompany != null ? p.DevCompany.Name : "?",
                    Rate = Config.CorporateReferencePrice * Config.StarMultiplier(stars, UnityEngine.Random.value),
                    OfferedAt = now,
                    EstimatedMbps = DemandOf(p),
                });
                sent++;
            }
            foreach (uint k in Declined.Where(kv => GameClock.MonthsBetween(kv.Value, now) >= 6.0).Select(kv => kv.Key).ToList()) Declined.Remove(k);
            if (sent > 0) Debug.Log("[ISP Business] " + sent + " new ISP offers.");
        }

        private static void ExpireOffers()
        {
            int now = GameClock.Stamp;
            Offers.RemoveAll(o => GameClock.MonthsBetween(o.OfferedAt, now) > Config.OfferLifetimeMonths
                                  || Product(o.ProductId) == null || OtherClaims.ContainsKey(o.ProductId));
        }

        // ---------- player actions

        public static bool Accept(IspDeal offer, out string error)
        {
            error = null;
            if (!Offers.Contains(offer)) { error = "This offer is no longer available."; return false; }
            if (IspNetwork.Pools.Count == 0) { error = "You need an ISP pool first. Switch a server group to ISP mode on the Network tab."; return false; }
            if (Product(offer.ProductId) == null || OtherClaims.ContainsKey(offer.ProductId))
            {
                Offers.Remove(offer);
                error = "This product is no longer available for an ISP deal.";
                return false;
            }
            Offers.Remove(offer);
            offer.AcceptedAt = Math.Max(1, GameClock.Stamp);
            Active.Add(offer);
            IspNetwork.SetAssignment(offer.Key, IspNetwork.BestPool());
            RefreshDemand();
            IspNetwork.Recalculate();
            IspNetwork.ApplyPower();
            Net.BroadcastClaim(offer.ProductId, true);
            return true;
        }

        public static void Decline(IspDeal offer)
        {
            if (!Offers.Remove(offer)) return;
            Declined[offer.ProductId] = GameClock.Stamp;
        }

        /// <summary>Player cancels an active deal: reputation penalty scaled by deal size.</summary>
        public static void Cancel(IspDeal d)
        {
            if (!Active.Contains(d)) return;
            double weight = Math.Min(1.0, Math.Sqrt(Math.Max(0, d.EstimatedMbps) / Config.RepReferenceMbps));
            GameSettings.Instance.MyCompany.ChangeBusinessRep((float)(Config.RepCancel * Math.Max(0.1, weight)), "ISP");
            End(d, null, false);
        }

        private static void End(IspDeal d, string notice, bool penalty)
        {
            if (!Active.Remove(d)) return;
            IspNetwork.RemoveProcess(d.Key);
            if (penalty)
            {
                double weight = Math.Min(1.0, Math.Sqrt(Math.Max(0, d.EstimatedMbps) / Config.RepReferenceMbps));
                GameSettings.Instance.MyCompany.ChangeBusinessRep((float)(Config.RepBadMonth * Math.Max(0.1, weight)), "ISP");
            }
            Net.BroadcastClaim(d.ProductId, false);
            if (notice != null) IspNetwork.PendingNotices.Add(notice);
        }

        // ---------- multiplayer claims (one side decides: the player who accepted)

        public static void OnRemoteClaim(byte player, uint productId, bool claim)
        {
            if (!claim)
            {
                if (OtherClaims.TryGetValue(productId, out byte holder) && holder == player) OtherClaims.Remove(productId);
                return;
            }
            OtherClaims[productId] = player;
            Offers.RemoveAll(o => o.ProductId == productId);
            IspDeal mine = Active.FirstOrDefault(x => x.ProductId == productId);
            if (mine != null)
            {
                byte me = GameSettings.Instance.MyCompany.NetworkPlayerID;
                if (player < me)
                {
                    // Both accepted at once: the lower player id keeps it, the other ends without penalty.
                    if (Active.Remove(mine)) IspNetwork.RemoveProcess(mine.Key);
                    IspNetwork.PendingNotices.Add("Another player signed the ISP deal for " + mine.ProductName + " at the same time, so yours was withdrawn. No penalty.");
                }
                else
                {
                    OtherClaims.Remove(productId);
                    Net.BroadcastClaim(productId, true); // tell them we keep it
                }
            }
        }

        public static void OnPlayerLeft(byte player)
        {
            foreach (uint k in OtherClaims.Where(kv => kv.Value == player).Select(kv => kv.Key).ToList()) OtherClaims.Remove(k);
        }

        public static IEnumerable<uint> MyClaims() { return Active.Select(x => x.ProductId); }

        // ---------- save data

        public static string PackDeals()
        {
            return Pack.List(Offers.Concat(Active).Select(d => string.Join(Pack.Field.ToString(), new[]
            {
                Pack.L(d.Id), Pack.L(d.ProductId), d.ProductName ?? "", d.CompanyName ?? "", Pack.D(d.Rate),
                Pack.L(d.OfferedAt), Pack.L(d.AcceptedAt), Pack.D(d.Performance), Pack.D(d.MonthDemand),
                Pack.D(d.MonthDelivered), Pack.L(d.LowSince), Pack.D(d.TotalRevenue), Pack.D(d.EstimatedMbps),
                Pack.D(d.LastFulfilment),
            })));
        }

        public static void UnpackDeals(string s, string declined, int nextId)
        {
            Offers.Clear();
            Active.Clear();
            Declined.Clear();
            _nextId = Math.Max(1, nextId);
            foreach (string rec in Pack.Unlist(s))
            {
                string[] f = rec.Split(Pack.Field);
                if (f.Length < 14) continue;
                var d = new IspDeal
                {
                    Id = (int)Pack.L(f[0]), ProductId = (uint)Pack.L(f[1]), ProductName = f[2], CompanyName = f[3],
                    Rate = Pack.D(f[4]), OfferedAt = (int)Pack.L(f[5]), AcceptedAt = (int)Pack.L(f[6]),
                    Performance = Pack.D(f[7]), MonthDemand = Pack.D(f[8]), MonthDelivered = Pack.D(f[9]),
                    LowSince = (int)Pack.L(f[10]), TotalRevenue = Pack.D(f[11]), EstimatedMbps = Pack.D(f[12]),
                    LastFulfilment = Pack.D(f[13]),
                };
                if (d.Id >= _nextId) _nextId = d.Id + 1;
                (d.Active ? Active : Offers).Add(d);
            }
            foreach (string rec in Pack.Unlist(declined))
            {
                string[] f = rec.Split(Pack.Field);
                if (f.Length == 2) Declined[(uint)Pack.L(f[0])] = (int)Pack.L(f[1]);
            }
        }

        public static string PackDeclined()
        {
            return Pack.List(Declined.Select(kv => Pack.L(kv.Key) + Pack.Field + Pack.L(kv.Value)));
        }

        public static int NextId { get { return _nextId; } }
    }
}
