// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic Heston-Hull-White engine's variance offset at small reversion (the S-hes plan's HR7 (xiv)). QuantLib's
/// m = σ²/(2a²)(t + 2/a e^(−at) − 1/(2a) e^(−2at) − 3/(2a)) (<c>analytichestonhullwhiteengine.cpp:57-67</c>) cancels to
/// order (a·t)³, its relative error about ε/(a·t)³; nanoql takes S-van's series below a·t = 1e-2 (the S-van plan's VR4
/// (xxvii)), QuantLib's closed form and bits above. Checked against the closed form in 50 digits
/// (<c>scratchpad\c3\refs_c3.py</c>), at σ 0.01 and t 2.
/// </summary>
public class AnalyticHestonHullWhiteSmallReversionTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);

    private static readonly HestonModel Model =
        new(new HestonProcess(RiskFree, new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.4, -0.6));

    [Fact]
    public void BelowTheCutTheSeriesHoldsTheClosedForm()
    {
        // a·t just below the cut, 1e-3, 1e-5 and 1e-8, where QuantLib's double is off by about 2e-10, 2e-7, 2e-1 and
        // 2e8 relative
        (double A, string Exact)[] points =
        [
            (0.00499999999995, "1.3233798338242487460172631E-4"),
            (0.0005, "1.3323337998333825826340790E-4"),
            (5e-06, "1.3333233333799998888437423E-4"),
            (5e-09, "1.3333333233333334355111504E-4"),
        ];
        foreach ((double a, string exact) in points)
        {
            double expected = double.Parse(exact, System.Globalization.CultureInfo.InvariantCulture);
            double m = VarianceOffset(a, 2.0);
            // the series' first omitted term, of order (a·t)⁵, is 9e-13 relative just below the cut
            Assert.True(Math.Abs(m - expected) <= 1e-12 * expected, $"a {a}: {m} for {exact}");
        }
    }

    [Fact]
    public void AboveTheCutQuantLibsClosedFormStands()
    {
        foreach ((double a, string exact) in new[] { (0.025, "1.2844794703434112740632816E-4"), (0.0050000000005, "1.3233798338231589576033487E-4") })
        {
            const double sigma = 0.01, t = 2.0;
            double quantLib = sigma * sigma / (2 * a * a)
                * (t + (2 / a * Math.Exp(-a * t)) - (1 / (2 * a) * Math.Exp(-2 * a * t)) - (3 / (2 * a)));
            double m = VarianceOffset(a, t);
            Assert.Equal(quantLib, m);
            double expected = double.Parse(exact, System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(Math.Abs(m - expected) <= 5e-10 * expected, $"a {a}: {m} for {exact}");
        }
    }

    // the engine's offset at the maturity t, its private VarianceOffset
    private static double VarianceOffset(double a, double t)
    {
        var engine = new AnalyticHestonHullWhiteEngine(Model, new HullWhite(RiskFree, a, 0.01), 144);
        MethodInfo offset = typeof(AnalyticHestonHullWhiteEngine).GetMethod("VarianceOffset", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (double)offset.Invoke(engine, [t])!;
    }
}
