// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic digital American engine on structures dated differently. QuantLib's engine reads the dividend curve and
/// the volatility at the exercise date through their own clocks (<c>analyticdigitalamericanengine.cpp:53-58</c>), so an
/// expiry before a structure's reference date throws. nanoql reads each structure by date, nothing accrued before its
/// own reference date. QuantLib's check: its engine on the market restated from the earliest date, every structure
/// accruing from today exactly what the original accrues from its own date (QuantLib-Python 1.44).
/// </summary>
public class AnalyticDigitalAmericanMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void LaterDatedStructuresAreReadFromTheirOwnDates()
    {
        // the dividend curve at T+2 and the volatility at T+30; QuantLib restated: from today, q = 2%·363/365 and
        // σ = 25%·√(335/365) on Act/365 to the 1-year expiry. (type, strike, at expiry, QuantLib's NPV, delta, gamma, rho)
        (OptionType Type, double Strike, bool AtExpiry, double[] QuantLib)[] cases =
        [
            (OptionType.Put, 90.0, false, [3.2443361120147687, -0.15308101061277862, 0.004807462831863784, -6.911062670325024]),
            (OptionType.Put, 90.0, true, [3.130835612343653]),
            (OptionType.Call, 110.0, false, [3.415985256098786, 0.1553274196990623, 0.0014028237182982502, 4.75304276289575]),
            (OptionType.Call, 110.0, true, [3.2927189093453286]),
        ];
        foreach (var c in cases)
        {
            OneAssetOptionResults results = Price(c.Type, c.Strike, c.AtExpiry, Today + 365);
            double?[] calculated = [results.NPV, results.Delta, results.Gamma, results.Rho];
            for (int i = 0; i < c.QuantLib.Length; i++)
            {
                double value = calculated[i]!.Value;
                Assert.True(
                    Math.Abs(value - c.QuantLib[i]) <= 1e-13 * Math.Abs(c.QuantLib[i]),
                    $"{c.Type} {c.Strike} result {i}: {value} against QuantLib's {c.QuantLib[i]}");
            }
        }
    }

    [Fact]
    public void AnExpiryBeforeTheStructuresDatesAccruesNothingFromThem()
    {
        // a 10-day down-and-in put at 90 with the dividend curve and the volatility at T+30, where QuantLib's reads
        // throw: no variance and no dividend accrue, so the underlying follows the risk-free forward, 100·e^(5%·10/365),
        // and never reaches the strike (the zero-variance limit)
        OneAssetOptionResults results = Price(OptionType.Put, 90.0, false, Today + 10, Today + 30);
        Assert.Equal(0.0, results.NPV!.Value);
        Assert.Equal(0.0, results.Delta!.Value);
        Assert.Equal(0.0, results.Gamma!.Value);
        Assert.Equal(0.0, results.Rho!.Value);
    }

    private static OneAssetOptionResults Price(
        OptionType type, double strike, bool atExpiry, Date expiry, Date? dividendDate = null)
    {
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(dividendDate ?? Today + 2, 0.02, A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today + 30, new NullCalendar(), 0.25, A365));
        var option = new VanillaOption(
            new CashOrNothingPayoff(type, strike, 5.0), new AmericanExercise(Today, expiry, atExpiry));
        return AnalyticDigitalAmericanEngine.Calculate(option, Context, process);
    }
}
