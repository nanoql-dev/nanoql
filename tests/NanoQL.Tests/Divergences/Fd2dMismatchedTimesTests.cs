// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The two-dimensional finite-difference basket engine on structures that measure time differently. QuantLib's engine
/// runs on the first process's risk-free times (<c>fd2dblackscholesvanillaengine.cpp:58-67</c>) and reads the second
/// process's curves and volatility at them (<c>fdm2dblackscholesop.cpp:53-54</c>, <c>:96-107</c>), each of which
/// measures time from its own reference date in its own day counter: with asset 2's curves and volatility on Act/360,
/// their discounts and variances by date unchanged, a spread call prices 0.22% below the same market restated on
/// Act/365, and on a risk-free curve settling two business days after the other structures 0.53% below. nanoql's grid
/// runs on the clock spanning both processes' structures, from their earliest reference date on the first process's
/// risk-free day counter, each structure read at the instants its times denote and accruing nothing before its own
/// date (master §2.2's mismatched-dates row, the S-exo plan's ER5 (i)), so it prices the restated market: QuantLib's
/// engine on that market is the check. A market on one clock keeps QuantLib's arithmetic.
/// </summary>
public class Fd2dMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly DayCounter A360 = new Actual360();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheSecondAssetsCurvesOnAnotherDayCounterPriceTheRestatedMarket()
    {
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(
            95.0, Flat(0.03 * 360.0 / 365.0, A360), Flat(0.05 * 360.0 / 365.0, A360), Vol(0.3, A360));

        // QuantLib's engine on the market restated on Act/365 (asset 2's volatility 0.3·√(365/360)):
        // 11.037470713251855 and 9.388508893150627; on the market as given, 11.012665753336456 and 9.36324004394188
        Assert.Equal(11.037470713251855, Price(p1, p2, SpreadCall(), European()), 1e-9);
        Assert.Equal(9.388508893150627, Price(p1, p2, AveragePut(), American()), 1e-9);
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsPricedFromTheEarliestDate()
    {
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, Context);
        Assert.Equal(Today + 4, settling.ReferenceDate);
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), settling, Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(95.0, Flat(0.03), settling, Vol(0.3));

        // QuantLib's engine on the market restated from today, the risk-free curve discounting nothing before
        // 19 October (a log-linear discount curve through 1, 1, and the settling curve's discounts a year and two
        // after): 10.992015309367027 and 9.385134445415616; on the market as given, 10.933364588005364 and
        // 9.318013217806918
        Assert.Equal(10.992015309367027, Price(p1, p2, SpreadCall(), European()), 1e-8);
        Assert.Equal(9.385134445415616, Price(p1, p2, AveragePut(), American()), 1e-8);
    }

    [Fact]
    public void TheSecondAssetsVolatilityOnItsOwnClockPricesTheRestatedMarket()
    {
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(95.0, Flat(0.03), Flat(0.05), Vol(0.3, A360));

        // QuantLib's engine on the restated market, 11.037470713251855; on the market as given, 10.993490007459034
        Assert.Equal(11.037470713251855, Price(p1, p2, SpreadCall(), European()), 1e-9);
    }

    internal static GeneralizedBlackScholesProcess Process(
        double s, YieldTermStructure q, YieldTermStructure r, BlackVolTermStructure v) => new BlackScholesMertonProcess(s, q, r, v);

    internal static YieldTermStructure Flat(double r, DayCounter? dc = null) => new FlatForward(Today, r, dc ?? A365);

    internal static BlackVolTermStructure Vol(double s, DayCounter? dc = null) =>
        new BlackConstantVol(Today, new NullCalendar(), s, dc ?? A365);

    internal static BasketPayoff SpreadCall() => new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 5.0));

    internal static BasketPayoff AveragePut() => new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Put, 100.0), [0.5, 0.5]);

    internal static Exercise European() => new EuropeanExercise(Today + 365);

    internal static Exercise American() => new AmericanExercise(Today, Today + 365);

    // QuantLib's engine at 41 × 41 × 50, Hundsdorfer's scheme, correlation 0.5
    internal static double Price(
        GeneralizedBlackScholesProcess p1, GeneralizedBlackScholesProcess p2, BasketPayoff payoff, Exercise exercise) =>
        Fd2dBlackScholesVanillaEngine.Calculate(new BasketOption(payoff, exercise), Context, p1, p2, 0.5, 41, 41, 50).NPV!.Value;
}
