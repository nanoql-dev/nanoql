// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Longstaff-Schwartz calibration seed's default. QuantLib's documentation says the calibration seed is the pricing
/// seed plus an offset, "to avoid identical paths in calibration and pricing"
/// (<c>mclongstaffschwartzengine.hpp:63-71</c>), but its constructor tests <c>seedCalibration != Null&lt;Real&gt;()</c>
/// on a <c>BigNatural</c> already narrowed from <c>Null&lt;Size&gt;()</c> (<c>:148</c>), so its default is always that
/// null, 2147483647, since <c>Null&lt;T&gt;()</c> is <c>numeric_limits&lt;int&gt;::max()</c> for every integral type
/// (<c>null.hpp:41-43</c>), through C++ and through the wheel's <c>nullInt()</c> alike. nanoql's default is the
/// documented one, (seed + 1768237423) mod 2³². QuantLib's check: its engine with that seed passed explicitly, whose
/// bits the fixed default reproduces; QuantLib's default's bits through the null passed explicitly; and QuantLib's
/// EquityOption example, whose Longstaff-Schwartz value through C++ (4.456935) the null reproduces and the
/// documented seed moves to its own run's 4.454440.
/// </summary>
public class LongstaffSchwartzCalibrationSeedTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheDefaultCalibrationSeedIsTheDocumentedOne()
    {
        // a put K 40, S 36, r 6%, vol 20%, 1 year; 50 steps, 4096 samples, seed 42 (QuantLib-Python 1.44)
        Assert.Equal(4.46594754120994, Price(null)); // QuantLib with seedCalibration = 42 + 1768237423
        Assert.Equal(Price(42 + 1768237423L), Price(null));

        // QuantLib's own default, the null, through the wheel
        Assert.Equal(4.476277986354302, Price(2147483647));
    }

    [Fact]
    public void QuantLibsDefaultThroughCxxIsTheSameNull()
    {
        // QuantLib 1.44's EquityOption example, its Longstaff-Schwartz row: C++'s default prints 4.456935, the null
        // passed explicitly; the documented seed, passed to QuantLib's example, prints 4.454440, nanoql's default.
        var settlement = new Date(17, May, 1998);
        var context = new EvaluationContext { EvaluationDate = new Date(15, May, 1998) };
        var process = new BlackScholesMertonProcess(
            36.0, new FlatForward(settlement, 0.0, A365), new FlatForward(settlement, 0.06, A365),
            new BlackConstantVol(settlement, new TARGET(), 0.2, A365));
        var option = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Put, 40.0), new AmericanExercise(settlement, new Date(17, May, 1999)));
        var engine = new MakeMCAmericanEngine<PseudoRandom, GeneralStatistics, PseudoRandom>(process, 42)
            .WithSteps(100).WithAntitheticVariate().WithCalibrationSamples(4096).WithAbsoluteTolerance(0.02);
        string Npv(MakeMCAmericanEngine<PseudoRandom, GeneralStatistics, PseudoRandom> make) =>
            make.ToEngine()(option, context).NPV!.Value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("4.456935", Npv(engine.WithSeedCalibration(2147483647)));
        Assert.Equal("4.454440", Npv(engine));
    }

    private static double Price(long? seedCalibration)
    {
        var process = new BlackScholesMertonProcess(
            36.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.06, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 40.0), new AmericanExercise(Today, Today + 365));

        // the wheel's MCAmericanEngine: polynomial order 2, monomials, no antithetic or control variate
        return MCAmericanEngine.Calculate<PseudoRandom, GeneralStatistics, PseudoRandom>(
            option, Context, process, 50, null, false, false, 4096, null, null, 42, 2, LsmBasisSystemPolynomialType.Monomial,
            seedCalibration: seedCalibration).NPV!.Value;
    }
}
