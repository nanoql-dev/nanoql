// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Asian engines on a market whose structures settle after the evaluation date (fixed). The option splits its
/// fixings by the evaluation date, and QuantLib's engines read each fixing to come on the process's time from the
/// structures' date, so a fixing today or tomorrow has a negative time: on a market settling two days on (spot 100, r
/// 5%, q 1%, σ 20%, Act/365 Fixed), a call at 100 fixing today, tomorrow and every 30 days to 360 days prices
/// 5.541332629516704 by the analytic geometric engine and 5.490134556097302 ± 0.055 by its Monte Carlo, the average
/// losing two of its fourteen fixings, 18% high, and 5.687334138697442 ± 0.057 by the arithmetic Monte Carlo, 17% high;
/// Turnbull-Wakeman refuses it with "negative time (-0.00547945) given", Choi with "first fixing date is in the past"
/// and the FD engine with "Fixing dates must not contain past date"; and Levy refuses an averaging from today with
/// "current average required for seasoned option". nanoql takes such a fixing at the clock's origin, the spot with no
/// variance and no discount accrued, so a past fixing at the spot, and Levy's span before the origin at the spot.
/// Check: the seasoned equivalent, the first two fixings past at the spot, which each engine prices as it, and which
/// QuantLib prices 4.694309394952492, 4.6519094182914005, 4.874857833169246, 4.935628853043604, 4.921817927818181 and
/// 4.954431596530007, and Levy with a current average at the spot 5.3936726841449385, each within the tolerance. Where
/// only the volatility, or only the curves, settle two days on, Turnbull-Wakeman and Choi, which read each structure on
/// its own, take the fixings before the latest structure's date at the spot, where QuantLib refuses them ("negative
/// time", "first fixing date is in the past"); the check QuantLib's engines on the seasoned option, 4.945115990140009
/// and 4.9312430644836445, and 4.964671374839958 and 4.951052106848293.
/// </summary>
public class AsianLateSettlementTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly Date Settles = Today + 2;
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Settles, 0.01, A365), new FlatForward(Settles, 0.05, A365), new BlackConstantVol(Settles, new NullCalendar(), 0.20, A365));

    private static readonly Date[] Monthly = [.. Enumerable.Range(1, 12).Select(i => Today + (30 * i))];
    private static readonly PlainVanillaPayoff Call = new(OptionType.Call, 100.0);
    private static readonly EuropeanExercise Exercise = new(Today + 360);

    [Fact]
    public void TheAnalyticGeometricEngineKeepsEveryFixing() =>
        Check(AverageType.Geometric, (o, p) => AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(o, Context, p).NPV!.Value, 4.694309394952492);

    [Fact]
    public void TheGeometricMonteCarloKeepsEveryFixing() =>
        Check(
            AverageType.Geometric,
            (o, p) => MCDiscreteGeometricAPEngine.Calculate<PseudoRandom, GeneralStatistics>(o, Context, p, false, false, 20000, null, null, 42).NPV!.Value,
            4.6519094182914005);

    [Fact]
    public void TheArithmeticMonteCarloKeepsEveryFixing() =>
        Check(
            AverageType.Arithmetic,
            (o, p) => MCDiscreteArithmeticAPEngine.Calculate<PseudoRandom, GeneralStatistics>(o, Context, p, false, false, false, 20000, null, null, 42).NPV!.Value,
            4.874857833169246);

    [Fact]
    public void TurnbullWakemanPricesTodaysFixings() =>
        Check(AverageType.Arithmetic, (o, p) => TurnbullWakemanAsianEngine.Calculate(o, Context, p).NPV!.Value, 4.935628853043604);

    [Fact]
    public void ChoiPricesTodaysFixings() =>
        Check(AverageType.Arithmetic, (o, p) => ChoiAsianEngine.Calculate(o, Context, p).NPV!.Value, 4.921817927818181);

    [Fact]
    public void TurnbullWakemanPricesTodaysFixingsWhereTheStructuresSettleApart()
    {
        Check(AverageType.Arithmetic, (o, p) => TurnbullWakemanAsianEngine.Calculate(o, Context, p).NPV!.Value, 4.945115990140009, Apart(Today, Settles));
        Check(AverageType.Arithmetic, (o, p) => TurnbullWakemanAsianEngine.Calculate(o, Context, p).NPV!.Value, 4.964671374839958, Apart(Settles, Today));
    }

    [Fact]
    public void ChoiPricesTodaysFixingsWhereTheStructuresSettleApart()
    {
        Check(AverageType.Arithmetic, (o, p) => ChoiAsianEngine.Calculate(o, Context, p).NPV!.Value, 4.9312430644836445, Apart(Today, Settles));
        Check(AverageType.Arithmetic, (o, p) => ChoiAsianEngine.Calculate(o, Context, p).NPV!.Value, 4.951052106848293, Apart(Settles, Today));
    }

    [Fact]
    public void TheFiniteDifferenceEnginePricesTodaysFixings() =>
        Check(AverageType.Arithmetic, (o, p) => FdBlackScholesAsianEngine.Calculate(o, Context, p).NPV!.Value, 4.954431596530007);

    [Fact]
    public void LevyPricesAnAveragingFromToday()
    {
        var fromToday = new ContinuousAveragingAsianOption(AverageType.Arithmetic, Today, Call, Exercise);
        double price = ContinuousArithmeticAsianLevyEngine.Calculate(fromToday, Context, Process, null).NPV!.Value;
        Assert.Equal(5.3936726841449385, price, 1e-10 * price);
        Assert.Equal(ContinuousArithmeticAsianLevyEngine.Calculate(fromToday, Context, Process, 100.0).NPV!.Value, price, 1e-14 * price);
    }

    [Fact]
    public void TheHestonGeometricEngineKeepsEveryFixing()
    {
        var heston = new HestonProcess(new FlatForward(Settles, 0.05, A365), new FlatForward(Settles, 0.01, A365), 100.0, 0.04, 2.0, 0.04, 0.5, -0.5);
        double given = AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(Given(AverageType.Geometric), Context, heston).NPV!.Value;
        double seasoned = AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(Seasoned(AverageType.Geometric), Context, heston).NPV!.Value;
        Assert.Equal(seasoned, given, 1e-10 * seasoned);
    }

    private static DiscreteAveragingAsianOption Given(AverageType type) =>
        new(type, type == AverageType.Geometric ? 1.0 : 0.0, 0, [Today, Today + 1, .. Monthly], Call, Exercise);

    private static DiscreteAveragingAsianOption Seasoned(AverageType type) =>
        new(type, type == AverageType.Geometric ? 100.0 * 100.0 : 200.0, 2, Monthly, Call, Exercise);

    // the curves settling on one date and the volatility on another
    private static GeneralizedBlackScholesProcess Apart(Date curves, Date volatility) =>
        new BlackScholesMertonProcess(
            100.0, new FlatForward(curves, 0.01, A365), new FlatForward(curves, 0.05, A365), new BlackConstantVol(volatility, new NullCalendar(), 0.20, A365));

    private static void Check(
        AverageType type, Func<DiscreteAveragingAsianOption, GeneralizedBlackScholesProcess, double> price, double quantLibSeasoned, GeneralizedBlackScholesProcess? process = null)
    {
        process ??= Process;
        double given = price(Given(type), process);
        double seasoned = price(Seasoned(type), process);
        Assert.Equal(seasoned, given, 1e-14 * seasoned);
        Assert.Equal(quantLibSeasoned, given, 1e-10 * Math.Max(quantLibSeasoned, 1.0));
    }
}
