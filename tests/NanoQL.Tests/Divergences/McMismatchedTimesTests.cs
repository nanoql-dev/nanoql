// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo vanilla engines on structures dated or counted differently. QuantLib's engines run their paths on
/// the process's times, the risk-free curve's from its reference date (<c>mcvanillaengine.hpp:152-163</c>,
/// <c>mclongstaffschwartzengine.hpp:213-237</c>), and read the other structures at those times: a dividend curve or a
/// volatility dated before the risk-free curve, or on another day counter, is read off its own clock. nanoql's engines
/// run their paths from the earliest structure's reference date on the risk-free curve's day counter
/// (<c>FdmBlackScholesOp.Clock</c>), each structure read at the instants the grid's times denote, nothing accruing
/// before its own date, and the payoffs discounted there. QuantLib's check: its own engines on the market restated on
/// one clock, from the same seeds, which the fixed engines on the original market reproduce to rounding
/// (QuantLib-Python 1.44).
/// </summary>
public class McMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly Date Expiry = Today + 365;

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock()
    {
        // the risk-free curve at T+2, the dividend curve and the volatility today; QuantLib's restated market: every
        // structure today, the risk-free discount 1 to T+2 and 5% after (a log-linear discount curve)
        GeneralizedBlackScholesProcess mixed = Process(new FlatForward(Today + 2, 0.05, A365), 0.25, A365);

        double european = MCEuropeanEngine.Calculate<PseudoRandom, GeneralStatistics>(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Expiry)),
            Context, mixed, 50, null, false, false, 10000, null, null, 42).NPV!.Value;
        AssertClose(11.265599998824895, european); // QuantLib on the restated market; on this one, 11.245484248883535

        double american = MCAmericanEngine.Calculate<PseudoRandom, GeneralStatistics, PseudoRandom>(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 110.0), new AmericanExercise(Today, Expiry)),
            Context, mixed, 50, null, false, false, 10000, null, null, 42, 3, Methods.LsmBasisSystemPolynomialType.Monomial)
            .NPV!.Value;
        AssertClose(14.30052133693499, american); // QuantLib restated; on this market, 14.271860586782111

        double digital = MCDigitalEngine.Calculate<PseudoRandom, GeneralStatistics>(
            new VanillaOption(new CashOrNothingPayoff(OptionType.Put, 90.0, 10.0), new AmericanExercise(Today, Expiry)),
            Context, mixed, 50, null, false, false, 10000, null, null, 42).NPV!.Value;
        AssertClose(6.644966861440799, digital); // QuantLib restated; on this market, 6.624473343985239
    }

    [Fact]
    public void AVolatilityOnAnotherDayCounterIsReadAtItsOwnTimes()
    {
        // the volatility on Act/360; QuantLib's restated market: 25% √(365/360) on Act/365
        GeneralizedBlackScholesProcess mixed = Process(new FlatForward(Today, 0.05, A365), 0.25, new Actual360());

        double european = MCEuropeanEngine.Calculate<PseudoRandom, GeneralStatistics>(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Expiry)),
            Context, mixed, 50, null, false, false, 10000, null, null, 42).NPV!.Value;
        AssertClose(11.345443418129129, european); // QuantLib restated; on this market, 11.278543963916455
    }

    [Fact]
    public void OnOneClockTheBitsAreQuantLibs()
    {
        // every structure today on Act/365: the process itself, QuantLib's reads
        GeneralizedBlackScholesProcess same = Process(new FlatForward(Today, 0.05, A365), 0.25 * Math.Sqrt(365.0 / 360.0), A365);
        double european = MCEuropeanEngine.Calculate<PseudoRandom, GeneralStatistics>(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Expiry)),
            Context, same, 50, null, false, false, 10000, null, null, 42).NPV!.Value;
        Assert.Equal(11.345443418129129, european);
    }

    private static GeneralizedBlackScholesProcess Process(YieldTermStructure riskFree, double vol, DayCounter volDc) =>
        new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), riskFree, new BlackConstantVol(Today, new NullCalendar(), vol, volDc));

    private static void AssertClose(double expected, double calculated) =>
        Assert.True(
            Math.Abs(calculated - expected) <= 1e-11 * Math.Abs(expected),
            $"expected {expected}, calculated {calculated}");
}
