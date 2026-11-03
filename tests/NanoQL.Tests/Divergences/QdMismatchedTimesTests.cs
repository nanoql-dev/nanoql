// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The QD+ and QD fixed-point engines on structures counted differently. QuantLib reads the volatility as
/// <c>blackVol(T, K)</c> at the risk-free curve's time (<c>qdplusamericanengine.cpp:179-184</c>), so a volatility on
/// Act/360 beside curves on Act/365 is read at a time its variance doesn't run over: about −0.6% on a one-year option.
/// nanoql reads σ² as the volatility's variance at the maturity date over the engine's time, and r and q from each
/// curve's discount over it. QuantLib's check: its own engines on the market restated on one day counter, the
/// volatility scaled by √(365/360) so its variance to the maturity is the original's (QuantLib-Python 1.44). Where the
/// structures share their counts, QuantLib's bits (the engines' vectors).
/// </summary>
public class QdMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(OptionType.Call, 10.61642480526379)] // QuantLib on Act/360: 10.550762855464942
    [InlineData(OptionType.Put, 8.954669416232484)] // 8.888598043856788
    public void QdPlusReadsTheVolatilitysOwnVariance(OptionType type, double restated) =>
        AssertClose(restated, QdPlusAmericanEngine.Calculate(Option(type), Context, Process()).NPV!.Value);

    [Theory]
    [InlineData(OptionType.Call, 10.616416142616028)] // QuantLib on Act/360: 10.550754589780796
    [InlineData(OptionType.Put, 8.948760799786124)] // 8.882700000818152
    public void QdFixedPointReadsTheVolatilitysOwnVariance(OptionType type, double restated) =>
        AssertClose(restated, QdFpAmericanEngine.Calculate(Option(type), Context, Process()).NPV!.Value);

    // q 3% and r 5% on Act/365, volatility 25% on Act/360, all dated today
    private static GeneralizedBlackScholesProcess Process() =>
        new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.03, A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.25, new Actual360()));

    private static VanillaOption Option(OptionType type) =>
        new(new PlainVanillaPayoff(type, 100.0), new AmericanExercise(Today, Today + 365));

    private static void AssertClose(double restated, double calculated) =>
        Assert.True(
            Math.Abs(calculated - restated) <= 1e-13 * restated, $"{calculated} against QuantLib's restated {restated}");
}
