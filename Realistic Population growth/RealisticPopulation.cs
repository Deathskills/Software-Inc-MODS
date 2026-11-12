// Realistic Population growth - Software Inc. code mod
// Sets MarketSimulation.Population from the in-game date so the market grows
// like the real-world largest platform ecosystem (1980 onwards).
// No Harmony, no method patches: only the public static Population field is
// written, so other software/server/distribution mods are not affected.
// Vanilla category multipliers (Computer 0.95, Console 0.75, Phone 1.0) still apply.
using System;
using UnityEngine;
using UnityEngine.UI;

public class RealisticPopulationMeta : ModMeta
{
    public override string Name => "Realistic Population growth";

    public override void ConstructOptionsScreen(RectTransform parent, bool inGame)
    {
        Text label = WindowManager.SpawnLabel();
        label.text = "Population follows the real-world largest platform ecosystem, scaled to the game.\n" +
                     "1980-1995: 145.6M (vanilla), 2025: ~992M, 2050 and later: 1.4B (flat).\n" +
                     "Current population: " + MarketSimulation.Population.ToString("N0");
        WindowManager.AddElementToElement(label.gameObject, parent.gameObject, new Rect(0f, 0f, 400f, 64f), new Rect(0f, 0f, 1f, 0f));
    }
}

public class RealisticPopulationBehaviour : ModBehaviour
{
    public const uint VanillaPopulation = 145613576u;

    // Real-world largest single platform ecosystem, millions of users, 1980..2025
    // (Atari VCS -> MS-DOS -> Windows -> Facebook/Meta). See README for sources.
    private static readonly double[] RealMillions =
    {
            3.5, 6.5, 12.0, 14.0, 15.0, 16.0,
            20.0, 28.0, 38.0, 50.0, 65.0, 80.0,
            95.0, 115.0, 140.0, 170.0, 210.0, 260.0,
            320.0, 400.0, 480.0, 560.0, 640.0, 730.0,
            830.0, 940.0, 1000.0, 1050.0, 1100.0, 1150.0,
            1200.0, 1250.0, 1300.0, 1350.0, 1400.0, 1591.0,
            1860.0, 2129.0, 2600.0, 2890.0, 3300.0, 3590.0,
            3740.0, 3980.0, 4100.0, 4200.0
    };

    private const int FirstYear = 1980;
    private const int LastDataYear = 2025;
    private const int CapYear = 2050;
    // 2025 -> 2084 constant growth toward 10.3B real; the game curve stops at CapYear.
    private static readonly double GrowthRate = Math.Pow(10300.0 / 4200.0, 1.0 / 59.0) - 1.0;
    private const int GrowthStartYear = 1995;     // population held at vanilla value until here
    private const double BaselineGame = VanillaPopulation; // in-game value 1980..1995
    private const double CapGame = 1.4e9;         // in-game value from 2050 on (below ~1.43B uint overflow risk)

    private static double _scale = -1;
    private bool _active;
    private int _lastStamp = int.MinValue;

    private static double RealAt(double year)
    {
        if (year <= FirstYear) return RealMillions[0];
        if (year >= CapYear) year = CapYear;
        if (year <= LastDataYear)
        {
            int i = (int)Math.Floor(year) - FirstYear;
            if (i >= RealMillions.Length - 1) return RealMillions[RealMillions.Length - 1];
            double t = year - Math.Floor(year);
            return RealMillions[i] + (RealMillions[i + 1] - RealMillions[i]) * t;
        }
        return RealMillions[RealMillions.Length - 1] * Math.Pow(1.0 + GrowthRate, year - LastDataYear);
    }

    public static uint PopulationAt(double year)
    {
        if (_scale < 0)
            _scale = (CapGame - BaselineGame) / ((RealAt(CapYear) - RealAt(GrowthStartYear)) * 1e6);
        if (year <= GrowthStartYear) return VanillaPopulation;
        double v = BaselineGame + (RealAt(year) - RealAt(GrowthStartYear)) * 1e6 * _scale;
        if (v > CapGame) v = CapGame;
        if (v < BaselineGame) v = BaselineGame;
        return (uint)Math.Round(v);
    }

    public override void OnActivate()
    {
        _active = true;
        _lastStamp = int.MinValue;
        Apply();
    }

    public override void OnDeactivate()
    {
        _active = false;
        MarketSimulation.Population = VanillaPopulation;
    }

    private void Update()
    {
        if (_active) Apply();
    }

    private void Apply()
    {
        if (TimeOfDay.Instance == null || GameSettings.Instance == null) return;
        SDateTime now = SDateTime.Now();
        int stamp = (now.Year * 12 + now.Month) * 64 + now.Day;
        if (stamp == _lastStamp) return; // update at most once per in-game day
        _lastStamp = stamp;
        double year = SDateTime.BaseYear + now.Year + (now.Month + (double)now.Day / Math.Max(1, GameSettings.DaysPerMonth)) / 12.0;
        MarketSimulation.Population = PopulationAt(year);
    }
}
