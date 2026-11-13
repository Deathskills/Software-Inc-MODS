// ISP Business - corporate ISP deals (spec 5.2, 5.4, 6, 7, 13; Milestones B and C).
//
// Product deals (Milestone B):
//  - Each month, up to 10 AI products that need internet (>= 1 Mbps) offer an ISP deal, ranked by value.
//    You need a receptionist, as for vanilla deals, and deals must be on in the difficulty settings.
//  - The price per Mbps is locked when the offer is made: $11.45 x the vanilla business-star multiplier.
// Company-wide deals (Milestone C):
//  - From 1990, up to 4 AI companies a month whose total need is >= 1000 Mbps offer a company-wide deal.
//  - It covers every product the company owns now or later (products change hands with trades and
//    buyouts, so the coverage follows), plus its development traffic, from vanilla's own formula:
//    team size x DevTime / 24 per project (SoftwareAlpha.GetLoadRequirement), with the AI's optimal team
//    size (SoftwareType.GetOptimalEmployeeCount, as in SimulatedCompany.PayExpenses), times the era's
//    bandwidth factor. Vanilla UsesISP is never changed: this is counted only inside the ISP deal.
//  - Accepting it folds your product deals for that company into it (no penalty). Products another
//    player holds a product deal for stay theirs and are left out of your company deal, so no traffic
//    is billed twice. No new product offers are made for a company under a company-wide deal.
//  - It ends without penalty when the company closes, is bought by a player, or needs under 1 Mbps for
//    6 months. Being bought by an AI company keeps it (the company still runs as a subsidiary).
// Both kinds:
//  - Offers and deals live in the vanilla Deals window (VanillaDeals.cs). Offers stay open 24 months.
//  - An accepted deal becomes an ISP process on one of your pools. You earn for the Mbps you actually
//    deliver, every hour; the customer pays it through vanilla's daily deal payment (Deals bills, "ISP").
//    You pay wholesale transit on it. Electricity comes from your servers.
//  - Performance starts at 1.0 and changes monthly with delivered/requested bandwidth (spec 13.3).
//    At 0 the customer cancels. Good months raise business reputation, bad months lower it.
//  - Multiplayer: offers are made on each player's own machine. When a player accepts, they tell the
//    others (claim), so nobody else is offered or keeps it. Ties go to the lower player id.
// The vanilla Deals window shows these deals, but they are left out of the vanilla save (VanillaDeals.cs).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ISPBusiness
{
    public sealed class IspDeal
    {
        public const byte KindProduct = 0, KindCompany = 1, KindMunicipal = 2;

        public int Id;
        public byte Kind;              // KindProduct or KindCompany
        public uint ProductId;         // product deals
        public uint CompanyId;         // company deals (the customer)
        public string ProductName;     // product name, or the company name for company deals
        public string CompanyName;
        public double Rate;            // $ per Mbps per month, locked at offer time
        public int OfferedAt;          // SDateTime stamp
        public int AcceptedAt;         // 0 while it's an offer
        public double Performance = 1.0;
        public double MonthDemand;     // Mbps-hours requested this month
        public double MonthDelivered;  // Mbps-hours delivered this month
        public int LowSince = -1;      // stamp since the need is under MinOfferMbps, or -1
        public double TotalRevenue;
        public double LastFulfilment = 1.0;
        public double EstimatedMbps;   // need when offered (offers) / last hour (deals)
        public double DevMbps;         // company deals: development part of EstimatedMbps
        public int ShowAt;             // stamp when the offer appears in the Deals window (receptionist time)
        public double Unpaid;          // earned but not yet paid by the customer
        public int Settlement;         // municipal deals: settlement id
        public double Residents;       // municipal deals: residents covered

        public bool Active { get { return AcceptedAt != 0; } }
        public bool IsCompany { get { return Kind == KindCompany; } }
        public bool IsMunicipal { get { return Kind == KindMunicipal; } }
        public bool IsProduct { get { return Kind == KindProduct; } }
        public string Key { get { return "deal:" + Id; } }
        public string Label
        {
            get
            {
                if (IsCompany) return CompanyName + " (company-wide)";
                if (IsMunicipal) return ProductName + ": " + Residents.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " residents";
                return ProductName + " (" + CompanyName + ")";
            }
        }

        /// <summary>Performance change this month would give at the current fulfilment (vanilla Performance column).</summary>
        public double PerfTrend
        {
            get
            {
                double f = MonthDemand > 0 ? Math.Min(1.0, MonthDelivered / MonthDemand) : LastFulfilment;
                return Config.PerformanceChange(f);
            }
        }
    }

    public static class Deals
    {
        public const string Bill = "ISP";
        public const string BillMunicipal = "ISP municipal";

        public static readonly List<IspDeal> Offers = new List<IspDeal>();
        public static readonly List<IspDeal> Active = new List<IspDeal>();

        /// <summary>Declined offers: "p:id" / "c:id" -> stamp. Not offered again for 6 months.</summary>
        private static readonly Dictionary<string, int> Declined = new Dictionary<string, int>();

        /// <summary>Products / companies other players hold an ISP deal for: id -> player id. Not saved.</summary>
        public static readonly Dictionary<uint, byte> OtherClaims = new Dictionary<uint, byte>();
        public static readonly Dictionary<uint, byte> OtherCompanyClaims = new Dictionary<uint, byte>();

        private static int _nextId = 1;
        private static int _lastMonth = int.MinValue;
        private static int _lastDay = int.MinValue;

        private static readonly Dictionary<uint, SoftwareProduct> _products = new Dictionary<uint, SoftwareProduct>();
        private static int _productsDay = int.MinValue;
        private static object _productsSim;            // the MarketSimulation the cache was built from
        private static float _lastMissRefresh = -999f;  // realtime of the last refresh caused by a cache miss

        /// <summary>Stamp of the last load (or start). For one in-game day after it, missing products or
        /// companies never end a deal or offer: the game may still be binding its market data.</summary>
        private static int _loadedAt = int.MinValue;

        public static void Reset()
        {
            Offers.Clear();
            Active.Clear();
            Declined.Clear();
            OtherClaims.Clear();
            OtherCompanyClaims.Clear();
            _products.Clear();
            _productsDay = int.MinValue;
            _productsSim = null;
            _lastMissRefresh = -999f;
            _loadedAt = int.MinValue;
            _nextId = 1;
            _lastMonth = int.MinValue;
            _lastDay = int.MinValue;
        }

        // ---------- products and companies

        private static void RefreshProducts(bool force)
        {
            GameSettings gs = GameSettings.Instance;
            object sim = gs != null ? gs.simulation : null;
            int day = GameClock.Stamp / (24 * 60);
            // Rebuilt daily, when the market changes (a load replaces it), and never kept empty.
            if (!force && day == _productsDay && sim == _productsSim && _products.Count > 0) return;
            _productsDay = day;
            _productsSim = sim;
            _products.Clear();
            if (gs == null || gs.simulation == null) return;
            foreach (SoftwareProduct p in gs.simulation.GetAllProducts(false))
                if (p != null) _products[p.ID] = p;
        }

        private static SoftwareProduct Product(uint id)
        {
            RefreshProducts(false);
            if (_products.TryGetValue(id, out SoftwareProduct p)) return p;
            // A miss may just be a stale cache (new release, trade): rebuild it, at most every 2 real seconds.
            if (Time.realtimeSinceStartup - _lastMissRefresh > 2f)
            {
                _lastMissRefresh = Time.realtimeSinceStartup;
                RefreshProducts(true);
                if (_products.TryGetValue(id, out p)) return p;
            }
            return null;
        }

        /// <summary>True for one in-game day after a load: nothing is ended for a missing product or company.</summary>
        private static bool InLoadGrace
        {
            get { return _loadedAt != int.MinValue && GameClock.Stamp - _loadedAt < 24 * 60; }
        }

        /// <summary>Called after mod data is restored (load) or a new game starts.</summary>
        public static void MarkLoaded()
        {
            _loadedAt = GameClock.Stamp;
            _lastMonth = GameClock.MonthIndex;
            _productsDay = int.MinValue;
            _products.Clear();
            Debug.Log("[ISP Business] Restored " + Active.Count + " ISP deals and " + Offers.Count + " offers.");
        }

        private static Company CompanyById(uint id)
        {
            GameSettings gs = GameSettings.Instance;
            return gs != null && gs.simulation != null ? gs.simulation.GetCompany(id) : null;
        }

        /// <summary>Mbps the product needs right now (vanilla load x era factor; includes other mods' load changes).</summary>
        public static double DemandOf(SoftwareProduct p)
        {
            if (p == null || p.Userbase <= 0) return 0;
            return p.GetLoadRequirement() * 1f.BandwidthFactor(SDateTime.Now());
        }

        /// <summary>An AI company that can buy ISP service: not a player, not owned by a player, still running.</summary>
        public static bool IsAiCompany(Company c)
        {
            if (!(c is SimulatedCompany sc)) return false;
            if (sc.Bankrupt || sc.IsPlayerOwned()) return false;
            if (sc.IsSubsidiary() && sc.OwnerCompany != null && sc.OwnerCompany.Player) return false;
            return true;
        }

        /// <summary>Mbps a company needs: its products (minus those another player serves) plus development.</summary>
        private static readonly HashSet<uint> LocalProductClaims = new HashSet<uint>();

        public static double CompanyDemand(Company c, out double dev)
        {
            dev = 0;
            if (c == null) return 0;
            double sum = 0;
            if (c.Products != null)
            {
                foreach (SoftwareProduct p in c.Products)
                {
                    if (p == null || OtherClaims.ContainsKey(p.ID) || LocalProductClaims.Contains(p.ID)) continue;
                    sum += DemandOf(p);
                }
            }
            if (c is SimulatedCompany sc && sc.ProjectQueue != null)
            {
                double factor = 1f.BandwidthFactor(SDateTime.Now());
                foreach (var proj in sc.ProjectQueue)
                {
                    if (proj == null || !proj.DevStarted || proj.Released) continue;
                    int team = SoftwareType.GetOptimalEmployeeCount(proj.DevTime).Sum();
                    dev += team * (proj.DevTime / 24.0) * factor;
                }
            }
            return sum + dev;
        }

        /// <summary>The deal's customer company, or null.</summary>
        public static Company ClientOf(IspDeal d)
        {
            if (d.IsCompany) return CompanyById(d.CompanyId);
            if (d.IsMunicipal) return Lawsuits.CityClient();
            SoftwareProduct p = Product(d.ProductId);
            return p != null ? p.DevCompany : null;
        }

        /// <summary>True when one of my company-wide deals (or another player's) covers this company.</summary>
        private static bool CompanyCovered(uint companyId)
        {
            return OtherCompanyClaims.ContainsKey(companyId) || Active.Any(x => x.IsCompany && x.CompanyId == companyId);
        }

        /// <summary>True while an offer can still be accepted.</summary>
        public static bool IsOpenOffer(IspDeal d)
        {
            if (!Offers.Contains(d)) return false;
            if (d.IsCompany) return !OtherCompanyClaims.ContainsKey(d.CompanyId) && IsAiCompany(CompanyById(d.CompanyId));
            if (d.IsMunicipal) return Municipal.Available(d.Settlement) >= Math.Max(1, d.Residents * 0.5);
            SoftwareProduct p = Product(d.ProductId);
            if (p == null || OtherClaims.ContainsKey(d.ProductId)) return false;
            return IsAiCompany(p.DevCompany) && !CompanyCovered(p.DevCompany.ID);
        }

        private static bool IsAiCustomer(SoftwareProduct p)
        {
            return IsAiCompany(p.DevCompany);
        }

        // ---------- hourly

        /// <summary>Sets each deal's process demand for this hour. Called before routing.</summary>
        public static void RefreshDemand()
        {
            LocalProductClaims.Clear();
            foreach (IspDeal deal in Active)
                if (deal.IsProduct) LocalProductClaims.Add(deal.ProductId);
            foreach (IspDeal d in Active)
            {
                double demand;
                if (d.IsCompany)
                {
                    Company customer = CompanyById(d.CompanyId);
                    double dev = 0;
                    demand = IsAiCompany(customer) ? CompanyDemand(customer, out dev) : 0;
                    d.DevMbps = dev;
                }
                else if (d.IsMunicipal) demand = Municipal.DemandNow(d.Residents);
                else
                {
                    SoftwareProduct product = Product(d.ProductId);
                    demand = product != null && IsAiCompany(product.DevCompany) ? DemandOf(product) : 0;
                }
                d.EstimatedMbps = demand;
                if (!IspNetwork.Processes.TryGetValue(d.Key, out IspProcess proc))
                {
                    proc = new IspProcess { Key = d.Key };
                    IspNetwork.Processes[d.Key] = proc;
                }
                proc.Label = d.Label;
                proc.Demand = demand;
            }
        }

        private static string EndReason(IspDeal d)
        {
            if (d.IsMunicipal) return null;
            if (d.IsCompany)
            {
                Company c = CompanyById(d.CompanyId);
                if (c == null || c.Bankrupt) return d.CompanyName + " closed down";
                if (!IsAiCompany(c)) return d.CompanyName + " is now owned by a player";
                return null;
            }
            SoftwareProduct product = Product(d.ProductId);
            if (product != null)
                return IsAiCompany(product.DevCompany) ? null : "its owner is no longer an eligible AI customer";
            RefreshProducts(true);
            if (_products.Count == 0) return null; // market data not available: never treat that as "left the market"
            return Product(d.ProductId) == null ? "the product left the market" : null;
        }

        /// <summary>Books deal revenue and transit for the hour, and handles the month boundary.</summary>
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

                double revenue = d.IsMunicipal ? Municipal.HourlyRevenue(d, demand, delivered, hoursPerMonth)
                                               : delivered * d.Rate / hoursPerMonth;
                d.Unpaid += revenue;
                d.TotalRevenue += revenue;
                IspNetwork.MonthRevenue += revenue;
                transit += delivered * transitRate / hoursPerMonth;
                d.MonthDemand += demand;
                d.MonthDelivered += delivered;

                // Not in the vanilla Deals window (deals off, or no customer): pay it directly.
                // Municipal deals have no paying company, so the mod always pays them directly.
                if (d.Unpaid >= 1.0 && (d.IsMunicipal || !VanillaDeals.IsShown(d))) PayDirect(d);

                string reason = InLoadGrace ? null : EndReason(d);
                if (reason != null) { ended.Add(new KeyValuePair<IspDeal, string>(d, reason)); continue; }
                // Town traffic is tiny by design (it's paid mostly by coverage), so the "needs under 1 Mbps"
                // rule only ends product and company deals.
                if (!d.IsMunicipal && demand < Config.MinOfferMbps)
                {
                    if (d.LowSince < 0) d.LowSince = now;
                    else if (GameClock.MonthsBetween(d.LowSince, now) >= Config.DealEndMonths)
                        ended.Add(new KeyValuePair<IspDeal, string>(d, "it has needed less than " + Config.MinOfferMbps + " Mbps for " + Config.DealEndMonths + " months"));
                }
                else d.LowSince = -1;
            }

            if (transit > 0) gs.ServerCost += (float)transit;
            IspNetwork.MonthTransit += transit;

            foreach (var kv in ended) End(kv.Key, "Your ISP deal for " + kv.Key.ProductName + " ended because " + kv.Value + ". No penalty.", false);

            int day = now / (24 * 60);
            if (day != _lastDay) { _lastDay = day; RefreshOfferEstimates(); }

        }

        public static void BeforeHour()
        {
            GameSettings gs = GameSettings.Instance;
            int now = GameClock.Stamp;
            int month = GameClock.MonthIndex;
            if (month != _lastMonth)
            {
                bool first = _lastMonth == int.MinValue;
                _lastMonth = month;
                if (!first)
                {
                    MonthlyReview();
                    Municipal.MonthlyBreaches();
                    MonthlyOffers();
                    MonthlyCompanyOffers();
                    Municipal.Snapshot(); // population, adoption and use only change here
                    if (OffersAllowed())
                        Municipal.MonthlyOffers(gs.MyCompany.BusinessStars, now, RecentlyDeclined, DealWindow.FindReceptionTime, () => _nextId++);
                }
                ExpireOffers();
            }
        }

        /// <summary>Pays what the deal earned straight to the player (Deals bills, "ISP").</summary>
        private static void PayDirect(IspDeal d)
        {
            if (d.Unpaid <= 0) return;
            Company customer = d.IsMunicipal ? null : ClientOf(d);
            if (!d.IsMunicipal && (customer == null || customer.Bankrupt || customer.Player))
            {
                d.Unpaid = 0;
                return;
            }
            float amount = (float)d.Unpaid;
            d.Unpaid = 0;
            if (customer != null) customer.MakeTransaction(-amount, Company.TransactionCategory.Deals, true, Bill);
            GameSettings.Instance.MyCompany.MakeTransaction(amount, Company.TransactionCategory.Deals, true, d.IsMunicipal ? BillMunicipal : Bill);
        }

        /// <summary>vanilla Payout(): hands over what the deal earned since the last daily payment.</summary>
        public static float TakeUnpaid(IspDeal d)
        {
            if (d.IsMunicipal || !Active.Contains(d) || d.Unpaid <= 0) return 0f;
            float amount = (float)d.Unpaid;
            d.Unpaid = 0;
            return amount;
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
                    double weight = d.IsMunicipal ? Math.Min(1.0, Math.Sqrt(d.Residents / Config.MuniRepReferenceResidents))
                                                  : Math.Min(1.0, Math.Sqrt(d.MonthDemand / hours / Config.RepReferenceMbps));
                    if (f >= 0.98) me.ChangeBusinessRep((float)(Config.RepGoodMonth * weight), "ISP");
                    else if (f < 0.90) me.ChangeBusinessRep((float)(Config.RepBadMonth * weight), "ISP");
                }
                d.MonthDemand = d.MonthDelivered = 0;
                if (d.Performance <= 0)
                    End(d, "The customer cancelled your ISP deal for " + d.ProductName + " because of poor service (Performance reached 0).", true);
            }
        }

        private static bool OffersAllowed()
        {
            GameSettings gs = GameSettings.Instance;
            return gs.simulation != null && gs.Difficulty.Deals >= 0.5f; // deals can be off in the difficulty settings
        }

        private static bool RecentlyDeclined(string key, int now)
        {
            return Declined.TryGetValue(key, out int when) && GameClock.MonthsBetween(when, now) < 6.0;
        }

        private static void MonthlyOffers()
        {
            if (!OffersAllowed()) return;
            GameSettings gs = GameSettings.Instance;
            RefreshProducts(true);
            int now = GameClock.Stamp;

            var taken = new HashSet<uint>(Active.Where(x => x.IsProduct).Select(x => x.ProductId)
                                          .Concat(Offers.Where(x => x.IsProduct).Select(x => x.ProductId)));
            int stars = gs.MyCompany.BusinessStars;
            var candidates = new List<KeyValuePair<SoftwareProduct, double>>();
            foreach (SoftwareProduct p in _products.Values)
            {
                if (taken.Contains(p.ID) || OtherClaims.ContainsKey(p.ID)) continue;
                if (RecentlyDeclined("p:" + p.ID, now)) continue;
                if (!IsAiCustomer(p) || CompanyCovered(p.DevCompany.ID)) continue;
                double mbps = DemandOf(p);
                if (mbps < Config.MinOfferMbps) continue;
                candidates.Add(new KeyValuePair<SoftwareProduct, double>(p, mbps * UnityEngine.Random.Range(0.85f, 1f)));
            }

            int sent = 0;
            foreach (var c in candidates.OrderByDescending(x => x.Value))
            {
                if (sent >= Config.ProductOffersPerMonth) break;
                SDateTime? reception = DealWindow.FindReceptionTime();
                if (!reception.HasValue) break; // no receptionist
                SoftwareProduct p = c.Key;
                Offers.Add(new IspDeal
                {
                    Id = _nextId++,
                    Kind = IspDeal.KindProduct,
                    ProductId = p.ID,
                    CompanyId = p.DevCompany.ID,
                    ProductName = p.Name,
                    CompanyName = p.DevCompany.Name,
                    Rate = Config.CorporateReferencePrice * Config.StarMultiplier(stars, UnityEngine.Random.value),
                    OfferedAt = now,
                    EstimatedMbps = DemandOf(p),
                    ShowAt = reception.Value.ToInt(),
                });
                sent++;
            }
            foreach (string k in Declined.Where(kv => GameClock.MonthsBetween(kv.Value, now) >= 6.0).Select(kv => kv.Key).ToList()) Declined.Remove(k);
            if (sent > 0) Debug.Log("[ISP Business] " + sent + " new product ISP offers.");
        }

        private static void MonthlyCompanyOffers()
        {
            if (!OffersAllowed()) return;
            if (GameClock.Year < Config.CompanyDealsFromYear) return;
            GameSettings gs = GameSettings.Instance;
            int now = GameClock.Stamp;
            var taken = new HashSet<uint>(Active.Concat(Offers).Where(x => x.IsCompany).Select(x => x.CompanyId));
            int stars = gs.MyCompany.BusinessStars;

            var candidates = new List<KeyValuePair<Company, double>>();
            foreach (Company c in gs.simulation.GetAllCompanies())
            {
                if (c == null || taken.Contains(c.ID) || OtherCompanyClaims.ContainsKey(c.ID)) continue;
                if (!IsAiCompany(c) || RecentlyDeclined("c:" + c.ID, now)) continue;
                double mbps = CompanyDemand(c, out double _);
                if (mbps < Config.MinCompanyMbps) continue;
                candidates.Add(new KeyValuePair<Company, double>(c, mbps * UnityEngine.Random.Range(0.85f, 1f)));
            }

            int sent = 0;
            foreach (var kv in candidates.OrderByDescending(x => x.Value))
            {
                if (sent >= Config.CompanyOffersPerMonth) break;
                SDateTime? reception = DealWindow.FindReceptionTime();
                if (!reception.HasValue) break;
                Company c = kv.Key;
                double mbps = CompanyDemand(c, out double dev);
                Offers.Add(new IspDeal
                {
                    Id = _nextId++,
                    Kind = IspDeal.KindCompany,
                    CompanyId = c.ID,
                    ProductName = c.Name,
                    CompanyName = c.Name,
                    Rate = Config.CorporateReferencePrice * Config.StarMultiplier(stars, UnityEngine.Random.value),
                    OfferedAt = now,
                    EstimatedMbps = mbps,
                    DevMbps = dev,
                    ShowAt = reception.Value.ToInt(),
                });
                sent++;
            }
            if (sent > 0) Debug.Log("[ISP Business] " + sent + " new company-wide ISP offers.");
        }

        private static void ExpireOffers()
        {
            int now = GameClock.Stamp;
            if (InLoadGrace) { Offers.RemoveAll(o => GameClock.MonthsBetween(o.OfferedAt, now) > Config.OfferLifetimeMonths); return; }
            Offers.RemoveAll(o => GameClock.MonthsBetween(o.OfferedAt, now) > Config.OfferLifetimeMonths || !IsOpenOffer(o));
        }

        /// <summary>Refreshes the need shown on open offers (daily is plenty).</summary>
        public static void RefreshOfferEstimates()
        {
            foreach (IspDeal o in Offers)
            {
                if (o.IsCompany) { o.EstimatedMbps = CompanyDemand(CompanyById(o.CompanyId), out double dev); o.DevMbps = dev; }
                else if (o.IsMunicipal) o.EstimatedMbps = Municipal.DemandNow(o.Residents);
                else o.EstimatedMbps = DemandOf(Product(o.ProductId));
            }
        }

        // ---------- player actions (from the vanilla Deals window)

        /// <summary>Vanilla Accept on an ISP offer. Vanilla has already checked StillValid(false).</summary>
        public static void AcceptFromVanilla(IspDeal offer)
        {
            if (!Offers.Remove(offer)) return;
            offer.AcceptedAt = Math.Max(1, GameClock.Stamp);
            if (offer.IsMunicipal) offer.Residents = Math.Max(1, Math.Min(offer.Residents, Municipal.Available(offer.Settlement)));
            Active.Add(offer);

            if (offer.IsCompany)
            {
                // Fold my product deals and offers for this company into the company-wide deal.
                foreach (IspDeal d in Active.Where(x => x.IsProduct && (x.CompanyId == offer.CompanyId || ProductOwner(x) == offer.CompanyId)).ToList())
                {
                    Debug.Log("[ISP Business] Folding product deal " + d.Id + " into the company-wide deal with " + offer.CompanyName + ".");
                    End(d, null, false);
                }
                Offers.RemoveAll(x => x.IsProduct && (x.CompanyId == offer.CompanyId || ProductOwner(x) == offer.CompanyId));
                IspNetwork.PendingNotices.Add("Your company-wide ISP deal with " + offer.CompanyName + " now covers all its products and development. " +
                                              "Your product ISP deals with them were folded into it, without penalty.");
            }

            string pool = IspNetwork.BestPool();
            IspNetwork.SetAssignment(offer.Key, pool);
            RefreshDemand();
            IspNetwork.Recalculate();
            IspNetwork.ApplyPower();
            if (offer.IsCompany) Net.BroadcastCompanyClaim(offer.CompanyId, true);
            else if (offer.IsMunicipal) Municipal.OnAccepted(offer);
            else Net.BroadcastClaim(offer.ProductId, true);
            if (pool == null)
                IspNetwork.PendingNotices.Add("You accepted the ISP deal for " + offer.ProductName + ", but you have no ISP pool, so it isn't served yet. " +
                                              "Switch a server group to ISP mode, or its Performance will drop.");
        }

        private static uint ProductOwner(IspDeal d)
        {
            SoftwareProduct p = Product(d.ProductId);
            return p != null && p.DevCompany != null ? p.DevCompany.ID : 0u;
        }

        /// <summary>Vanilla Reject (wasActive false) or Cancel (wasActive true). For a cancel, vanilla has
        /// already applied ReputationEffect(true), which is CancelPenalty, and taken Payout().</summary>
        public static void CancelFromVanilla(IspDeal d, bool wasActive)
        {
            if (wasActive) End(d, null, false);
            else Decline(d);
        }

        public static void Decline(IspDeal offer)
        {
            if (!Offers.Remove(offer)) return;
            Declined[offer.IsCompany ? "c:" + offer.CompanyId : offer.IsMunicipal ? "m:" + offer.Settlement : "p:" + offer.ProductId] = GameClock.Stamp;
        }

        /// <summary>Reputation change (business stars) when the player cancels an active deal, scaled by size.</summary>
        public static float CancelPenalty(IspDeal d)
        {
            double weight = d.IsMunicipal ? Math.Min(1.0, Math.Sqrt(d.Residents / Config.MuniRepReferenceResidents))
                                          : Math.Min(1.0, Math.Sqrt(Math.Max(0, d.EstimatedMbps) / Config.RepReferenceMbps));
            return (float)(Config.RepCancel * Math.Max(0.1, weight));
        }

        private static void End(IspDeal d, string notice, bool penalty)
        {
            if (!Active.Remove(d)) return;
            Debug.Log("[ISP Business] ISP deal " + d.Id + " (" + d.Label + ") ended. " + (notice ?? "Cancelled or folded."));
            PayDirect(d); // whatever the customer hadn't paid yet
            IspNetwork.RemoveProcess(d.Key);
            if (penalty)
            {
                double weight = Math.Min(1.0, Math.Sqrt(Math.Max(0, d.EstimatedMbps) / Config.RepReferenceMbps));
                GameSettings.Instance.MyCompany.ChangeBusinessRep((float)(Config.RepBadMonth * Math.Max(0.1, weight)), "ISP");
            }
            if (d.IsCompany) Net.BroadcastCompanyClaim(d.CompanyId, false);
            else if (d.IsMunicipal) Municipal.OnEnded(d);
            else Net.BroadcastClaim(d.ProductId, false);
            if (notice != null) IspNetwork.PendingNotices.Add(notice);
        }

        /// <summary>Removes a deal merged into another one (consolidation): no penalty, no payment lost.</summary>
        public static void Retire(IspDeal d)
        {
            if (!Active.Remove(d)) return;
            IspNetwork.RemoveProcess(d.Key);
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
            Offers.RemoveAll(o => o.IsProduct && o.ProductId == productId);
            IspDeal mine = Active.FirstOrDefault(x => x.IsProduct && x.ProductId == productId);
            if (mine != null) ResolveTie(player, mine, () => OtherClaims.Remove(productId), () => Net.BroadcastClaim(productId, true));
        }

        public static void OnRemoteCompanyClaim(byte player, uint companyId, bool claim)
        {
            if (!claim)
            {
                if (OtherCompanyClaims.TryGetValue(companyId, out byte holder) && holder == player) OtherCompanyClaims.Remove(companyId);
                return;
            }
            OtherCompanyClaims[companyId] = player;
            // Their company deal covers the company: my offers for it go. My existing product deals stay mine
            // (they've been told about them, so their deal leaves those products out).
            Offers.RemoveAll(o => o.IsCompany ? o.CompanyId == companyId : (o.CompanyId == companyId || ProductOwner(o) == companyId));
            IspDeal mine = Active.FirstOrDefault(x => x.IsCompany && x.CompanyId == companyId);
            if (mine != null) ResolveTie(player, mine, () => OtherCompanyClaims.Remove(companyId), () => Net.BroadcastCompanyClaim(companyId, true));
        }

        /// <summary>Both players hold the same deal: the lower player id keeps it, the other ends without penalty.</summary>
        private static void ResolveTie(byte player, IspDeal mine, Action forgetTheirs, Action reassert)
        {
            byte me = GameSettings.Instance.MyCompany.NetworkPlayerID;
            if (player < me)
            {
                if (Active.Remove(mine)) { PayDirect(mine); IspNetwork.RemoveProcess(mine.Key); }
                IspNetwork.PendingNotices.Add("Another player signed the ISP deal for " + mine.ProductName + " at the same time, so yours was withdrawn. No penalty.");
            }
            else
            {
                forgetTheirs();
                reassert(); // tell them we keep it
            }
        }

        public static void OnPlayerLeft(byte player)
        {
            foreach (uint k in OtherClaims.Where(kv => kv.Value == player).Select(kv => kv.Key).ToList()) OtherClaims.Remove(k);
            foreach (uint k in OtherCompanyClaims.Where(kv => kv.Value == player).Select(kv => kv.Key).ToList()) OtherCompanyClaims.Remove(k);
            Municipal.OnPlayerLeft(player);
        }

        public static IEnumerable<uint> MyClaims() { return Active.Where(x => x.IsProduct).Select(x => x.ProductId); }
        public static IEnumerable<uint> MyCompanyClaims() { return Active.Where(x => x.IsCompany).Select(x => x.CompanyId); }

        // ---------- save data

        public static string PackDeals()
        {
            return Pack.List(Offers.Concat(Active).Select(d => string.Join(Pack.Field.ToString(), new[]
            {
                Pack.L(d.Id), Pack.L(d.ProductId), d.ProductName ?? "", d.CompanyName ?? "", Pack.D(d.Rate),
                Pack.L(d.OfferedAt), Pack.L(d.AcceptedAt), Pack.D(d.Performance), Pack.D(d.MonthDemand),
                Pack.D(d.MonthDelivered), Pack.L(d.LowSince), Pack.D(d.TotalRevenue), Pack.D(d.EstimatedMbps),
                Pack.D(d.LastFulfilment), Pack.L(d.ShowAt), Pack.L(d.Kind), Pack.L(d.CompanyId), Pack.D(d.Unpaid),
                Pack.D(d.DevMbps), Pack.L(d.Settlement), Pack.D(d.Residents),
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
                    ShowAt = f.Length > 14 ? (int)Pack.L(f[14]) : 0,
                    Kind = f.Length > 15 ? (byte)Pack.L(f[15]) : IspDeal.KindProduct,
                    CompanyId = f.Length > 16 ? (uint)Pack.L(f[16]) : 0u,
                    Unpaid = f.Length > 17 ? Pack.D(f[17]) : 0,
                    DevMbps = f.Length > 18 ? Pack.D(f[18]) : 0,
                    Settlement = f.Length > 19 ? (int)Pack.L(f[19]) : 0,
                    Residents = f.Length > 20 ? Pack.D(f[20]) : 0,
                };
                if (d.Id >= _nextId) _nextId = d.Id + 1;
                (d.Active ? Active : Offers).Add(d);
            }
            foreach (string rec in Pack.Unlist(declined))
            {
                string[] f = rec.Split(Pack.Field);
                if (f.Length != 2) continue;
                string key = f[0].Length > 1 && f[0][1] == ':' ? f[0] : "p:" + f[0];
                Declined[key] = (int)Pack.L(f[1]);
            }
        }

        public static string PackDeclined()
        {
            return Pack.List(Declined.Select(kv => kv.Key + Pack.Field + Pack.L(kv.Value)));
        }

        public static int NextId { get { return _nextId; } }
    }
}
