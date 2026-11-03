// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The COS Heston engine's cumulants past κt = 100. QuantLib's c₁ to c₄ (<c>coshestonengine.cpp:177-286</c>) take
/// powers of e^{κt} whose products overflow: c₄ is NaN at κt = 200, c₃ from 300, c₂ at 400, and the engine's truncation
/// range with them, so it prices NaN. nanoql evaluates each cumulant past its upper cut in an extended-range double,
/// QuantLib's expression unchanged, and QuantLib's double, and so its bits, up to it. The check: QuantLib's expressions
/// in Boost's 50-digit <c>cpp_bin_float_50</c>, QuantLib's cumulants at κt = 100, and QuantLib's analytic engine.
/// </summary>
public class CosHestonLargeKappaTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void PastTheUpperCutTheCumulantsAreTheFiftyDigitValues()
    {
        // QuantLib 1.44's double values (QuantLib's expressions in double): NaN at each point below; its c1 at kappa t
        // 600, past c1's upper cut of 500, is -1.2000000000000002
        foreach ((double kappa, double t, int i, double digits) in new[]
                 {
                     (2.0, 100.0, 4, 0.6944246024853515189159973),
                     (5.0, 60.0, 3, -0.2352022243199999954323581),
                     (5.0, 60.0, 4, 0.05722097318359199673483831),
                     (4.0, 100.0, 2, 4.155228906250000080546068),
                     (4.0, 100.0, 3, -0.5012235703124999898741911),
                     (4.0, 100.0, 4, 0.1531872581798171908182936),
                     (10.0, 60.0, 1, -1.200000000000000024980018),
                     (10.0, 60.0, 2, 2.436478650000000049349485),
                     (10.0, 60.0, 3, -0.1127398422599999979898453),
                     (10.0, 60.0, 4, 0.01361747604097181174918398),
                 })
        {
            double cumulant = Cumulant(Engine(kappa), i, t);
            Assert.True(
                Math.Abs((cumulant / digits) - 1.0) <= 1e-12,
                $"c{i} at kappa {kappa}, t {t}: {cumulant} against {digits}");
        }

        // QuantLib 1.44's c1 to c4 at kappa t 100, the upper cuts' least (the C++ harness's COSHestonEngine::members)
        COSHestonEngine engine = Engine(1.0);
        Assert.Equal(-2.0, engine.C1(100.0));
        Assert.Equal(4.682649999999999, engine.C2(100.0));
        Assert.Equal(-2.7089909999999997, engine.C3(100.0));
        Assert.Equal(3.5495996681249995, engine.C4(100.0));
    }

    [Fact]
    public void PastTheUpperCutThePriceMatchesTheAnalyticEngine()
    {
        // kappa 40 over ten years, kappa t 400: QuantLib 1.44's COS engine prices NaN at each strike (QuantLib-Python
        // 1.44)
        var context = new EvaluationContext { EvaluationDate = Today };
        HestonModel model = Model(40.0);
        foreach ((double strike, double analytic) in new[] { (90.0, 33.5597981528422), (100.0, 29.607252376646898), (110.0, 26.120627368108316) })
        {
            // QuantLib 1.44's analytic engine at 192 points (QuantLib-Python 1.44)
            var call = new VanillaOption(
                new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Today + new Period(10, TimeUnit.Years)));
            Assert.Equal(analytic, AnalyticHestonEngine.Calculate(call, context, model, 192).NPV!.Value, 1e-12);

            double cos = COSHestonEngine.Calculate(call, context, model).NPV!.Value;
            Assert.True(Math.Abs((cos / analytic) - 1.0) < 1e-12, $"strike {strike}: {cos} against {analytic}");
        }
    }

    private static double Cumulant(COSHestonEngine engine, int i, double t) => i switch
    {
        1 => engine.C1(t),
        2 => engine.C2(t),
        3 => engine.C3(t),
        _ => engine.C4(t),
    };

    private static COSHestonEngine Engine(double kappa) => new(Model(kappa));

    // v0 = theta 0.04, sigma 0.3, rho -0.5, r 3%, q 1%, spot 100
    private static HestonModel Model(double kappa) =>
        new(new HestonProcess(
            new FlatForward(Today, 0.03, new Actual365Fixed()), new FlatForward(Today, 0.01, new Actual365Fixed()), 100.0, 0.04, kappa, 0.04, 0.3, -0.5));
}
