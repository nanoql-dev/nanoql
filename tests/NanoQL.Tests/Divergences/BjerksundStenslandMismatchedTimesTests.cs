// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Bjerksund-Stensland's Greeks on structures dated differently. QuantLib's American branch takes the dividend,
/// risk-free and volatility times from the risk-free curve's reference date
/// (<c>bjerksundstenslandengine.cpp:248-251</c>, <c>:320</c>, <c>:366</c>), so where a curve settles T+2 the rate
/// sensitivity of the other runs over the wrong time. nanoql takes each structure's time from its own reference date.
/// QuantLib's check: its own NPVs bumped by ±1e-6 in the rate, which nanoql's rho and dividend rho match to the
/// difference's own accuracy (QuantLib-Python 1.44). Where the structures share their dates, QuantLib's bits (the
/// engine's vectors).
/// </summary>
public class BjerksundStenslandMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ADividendCurveSettlingLaterTakesItsOwnTime()
    {
        // q 3% settling T+2, r 5% and volatility 25% today, a year: the put's rho and the call's dividend rho against
        // QuantLib's bumped NPVs; QuantLib's own Greeks read -41.03747950717067 and -56.40344779217264
        GeneralizedBlackScholesProcess process = Process(Settling(0.03), new FlatForward(Today, 0.05, A365));
        AssertClose(-40.587753716181396, Price(OptionType.Put, process).Rho!.Value);
        AssertClose(-55.785327834456666, Price(OptionType.Call, process).DividendRho!.Value);
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterTakesItsOwnTime()
    {
        // r 5% settling T+2, q 3% and volatility 25% today: QuantLib's own Greeks read -39.9583867239943 and
        // -55.611558150940425
        GeneralizedBlackScholesProcess process = Process(new FlatForward(Today, 0.03, A365), Settling(0.05));
        AssertClose(-40.40113893921671, Price(OptionType.Put, process).Rho!.Value);
        AssertClose(-56.22775270452962, Price(OptionType.Call, process).DividendRho!.Value);
    }

    private static YieldTermStructure Settling(double rate) => FlatForward.Create(2, new TARGET(), rate, A365, Context);

    private static GeneralizedBlackScholesProcess Process(YieldTermStructure dividend, YieldTermStructure riskFree) =>
        new BlackScholesMertonProcess(100.0, dividend, riskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static OneAssetOptionResults Price(OptionType type, GeneralizedBlackScholesProcess process) =>
        BjerksundStenslandApproximationEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(type, 100.0), new AmericanExercise(Today, Today + 365)), Context, process);

    private static void AssertClose(double bumped, double calculated) =>
        Assert.True(Math.Abs(calculated - bumped) <= 1e-8 * Math.Abs(bumped), $"{calculated} against QuantLib's bumped {bumped}");
}
