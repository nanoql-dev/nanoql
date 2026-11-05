// SPDX-FileCopyrightText: Copyright (C) 2005, 2007, 2009, 2010, 2012, 2014, 2017 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2022 Ignacio Anguita
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/hestonmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Equity;

/// <summary>
/// A volatility surface and Heston's calibration, on the DAX market of QuantLib's testDAXCalibration (from A. Sepp,
/// "Pricing European-Style Options under Jump Diffusion Processes with Stochastic Volatility").
/// </summary>
public static class VolatilityAndHeston
{
    /// <summary>Gets the market's date, 5 July 2002.</summary>
    public static EvaluationContext Context { get; } = new() { EvaluationDate = new Date(5, Month.July, 2002) };

    /// <summary>Gets the DAX's spot.</summary>
    public static double Spot => 4468.17;

    /// <summary>Gets the expiries, in days from the market's date.</summary>
    public static IReadOnlyList<int> Days { get; } = [13, 41, 75, 165, 256, 345, 524, 703];

    /// <summary>Gets the strikes.</summary>
    public static IReadOnlyList<double> Strikes { get; } =
        [3400, 3600, 3800, 4000, 4200, 4400, 4500, 4600, 4800, 5000, 5200, 5400, 5600];

    /// <summary>Gets the quoted Black volatilities, a row per strike and a column per expiry.</summary>
    public static double[,] Vols { get; } =
    {
        { 0.6625, 0.4875, 0.4204, 0.3667, 0.3431, 0.3267, 0.3121, 0.3121 },
        { 0.6007, 0.4543, 0.3967, 0.3511, 0.3279, 0.3154, 0.2984, 0.2921 },
        { 0.5084, 0.4221, 0.3718, 0.3327, 0.3155, 0.3027, 0.2919, 0.2889 },
        { 0.4541, 0.3869, 0.3492, 0.3149, 0.2963, 0.2926, 0.2819, 0.2800 },
        { 0.4060, 0.3607, 0.3330, 0.2999, 0.2887, 0.2811, 0.2751, 0.2775 },
        { 0.3726, 0.3396, 0.3108, 0.2781, 0.2788, 0.2722, 0.2661, 0.2686 },
        { 0.3550, 0.3277, 0.3012, 0.2781, 0.2781, 0.2661, 0.2661, 0.2681 },
        { 0.3428, 0.3209, 0.2958, 0.2740, 0.2688, 0.2627, 0.2580, 0.2620 },
        { 0.3302, 0.3062, 0.2799, 0.2631, 0.2573, 0.2533, 0.2504, 0.2544 },
        { 0.3343, 0.2959, 0.2705, 0.2540, 0.2504, 0.2464, 0.2448, 0.2462 },
        { 0.3460, 0.2845, 0.2624, 0.2463, 0.2425, 0.2385, 0.2373, 0.2422 },
        { 0.3857, 0.2860, 0.2578, 0.2399, 0.2357, 0.2327, 0.2312, 0.2351 },
        { 0.3976, 0.2860, 0.2607, 0.2356, 0.2297, 0.2268, 0.2241, 0.2320 },
    };

    /// <summary>The example's outputs.</summary>
    /// <returns>The surface's vols read back at three strikes, and Heston's calibrated parameters.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        BlackVarianceSurface surface = BuildSurface();
        var nodes = new Table(">Expiry, days", ">Vol at 4,000", ">Vol at 4,400", ">Vol at 5,000");
        for (int m = 0; m < Days.Count; ++m)
        {
            Date expiry = Context.EvaluationDate + Days[m];
            nodes.Add(Days[m].ToString(CultureInfo.InvariantCulture),
                Table.Percent(surface.BlackVol(expiry, 4000), 2), Table.Percent(surface.BlackVol(expiry, 4400), 2),
                Table.Percent(surface.BlackVol(expiry, 5000), 2));
        }

        var (model, errorSum) = Calibrate();
        var heston = new Table("Parameter", ">Calibrated");
        heston.Add("θ, the long-run variance", G(model.Theta));
        heston.Add("κ, the mean reversion", G(model.Kappa));
        heston.Add("σ, the volatility of variance", G(model.Sigma));
        heston.Add("ρ, the correlation", G(model.Rho));
        heston.Add("v₀, the initial variance", G(model.V0));
        heston.Add("Sum of squared vol errors, in vol points²", G(errorSum));

        yield return ("dax-surface", nodes);
        yield return ("heston-calibration", heston);
    }

    /// <summary>The quotes as a Black variance surface.</summary>
    /// <returns>The surface.</returns>
    public static BlackVarianceSurface BuildSurface()
    {
        // <surface>
        Date today = Context.EvaluationDate;
        Date[] expiries = [.. Days.Select(d => today + d)];
        var vols = new Matrix(Strikes.Count, Days.Count);
        for (int s = 0; s < Strikes.Count; ++s)
        {
            for (int m = 0; m < Days.Count; ++m)
            {
                vols[s, m] = Vols[s, m];
            }
        }

        var surface = new BlackVarianceSurface(today, new TARGET(), expiries, Strikes, vols, new Actual365Fixed());
        // </surface>
        return surface;
    }

    /// <summary>Heston calibrated to the quotes, as QuantLib's testDAXCalibration does.</summary>
    /// <returns>The calibrated model and the sum of its squared vol errors, in vol points.</returns>
    public static (HestonModel Model, double ErrorSum) Calibrate()
    {
        // <calibrate>
        Date today = Context.EvaluationDate;
        DayCounter dayCounter = new Actual365Fixed();
        var dates = new List<Date> { today };
        var rates = new List<double> { 0.0357 };
        double[] zeroRates = [0.0357, 0.0349, 0.0341, 0.0355, 0.0359, 0.0368, 0.0386, 0.0401];
        for (int i = 0; i < Days.Count; ++i)
        {
            dates.Add(today + Days[i]);
            rates.Add(zeroRates[i]);
        }

        var riskFree = new ZeroCurve(dates, rates, dayCounter);
        var dividend = new FlatForward(today, 0.0, dayCounter);

        var helpers = new List<HestonModelHelper>();
        for (int s = 0; s < Strikes.Count; ++s)
        {
            for (int m = 0; m < Days.Count; ++m)
            {
                var maturity = new Period((int)((Days[m] + 3) / 7.0), TimeUnit.Weeks); // to the nearest week
                helpers.Add(new HestonModelHelper(maturity, new TARGET(), Spot, Strikes[s], Vols[s, m], riskFree, dividend,
                    Context, BlackCalibrationHelperCalibrationErrorType.ImpliedVolError)
                    .SetPricingEngine<HestonModel>((option, context, model) =>
                        AnalyticHestonEngine.Calculate(option, context, model, 64)));
            }
        }

        var start = new HestonModel(new HestonProcess(riskFree, dividend, Spot, 0.1, 1.0, 0.1, 0.5, -0.5));
        HestonModel calibrated = start.Calibrate(helpers, new LevenbergMarquardt(1e-8, 1e-8, 1e-8),
            new EndCriteria(400, 40, 1.0e-8, 1.0e-8, 1.0e-8), Context).Model;
        // </calibrate>
        double errorSum = 0.0;
        foreach (HestonModelHelper h in helpers)
        {
            double error = h.CalibrationError(calibrated, Context) * 100.0;
            errorSum += error * error;
        }

        return (calibrated, errorSum);
    }

    private static string G(double value) => value.ToString("G6", CultureInfo.InvariantCulture);
}
