// ISP Business - municipal lawsuits through the vanilla lawsuit system (spec 14).
//  - A settlement sues with GameSettings.LaunchSuit, the same queue vanilla uses: the player gets the
//    vanilla prompt (court action with lawyers, or settle), and LegalWork decides who wins and the damages.
//  - Settlements aren't companies, so the plaintiff is the market's "Public Domain" company, and the
//    subject is vanilla's "Negligence". A notice first says which settlement is suing and why.
//  - Only vanilla types go into the save (Lawsuit, LegalWork, Company), so saves stay loadable without the mod.
using System;
using UnityEngine;

namespace ISPBusiness
{
    public static class Lawsuits
    {
        /// <summary>The company shown as the client of municipal deals and as the plaintiff of their suits.</summary>
        public static Company CityClient()
        {
            GameSettings gs = GameSettings.Instance;
            return gs != null && gs.simulation != null ? gs.simulation.PublicDomain : null;
        }

        public static void FileMunicipal(Settlement s, bool noService, double residents)
        {
            GameSettings gs = GameSettings.Instance;
            Company plaintiff = CityClient();
            double money = Math.Max(Config.MuniSuitMin, Math.Min(Config.MuniSuitMax, residents * Config.MuniSuitPerResident));
            string why = noService
                ? "you gave no ISP service there for a whole month"
                : "your ISP service there has been under 75% for 5 months in a row";
            IspNetwork.PendingNotices.Add(s.Name + " is suing you for negligence because " + why + ". The claim is " +
                                          ((float)money).Currency() + ". The lawsuit arrives within a day: go to court with your lawyers or settle.");
            if (gs == null || plaintiff == null) return;
            try
            {
                gs.LaunchSuit(new GameSettings.Lawsuit(plaintiff, "Negligence", money, 0.5f));
                Debug.Log("[ISP Business] " + s.Name + " filed a lawsuit for " + money + " (" + why + ").");
            }
            catch (Exception e)
            {
                Debug.LogError("[ISP Business] Could not file the municipal lawsuit: " + e.Message);
            }
        }
    }
}
