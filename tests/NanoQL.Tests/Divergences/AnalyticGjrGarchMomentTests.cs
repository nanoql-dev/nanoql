// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="AnalyticGJRGARCHEngine"/>'s moments (C3-B1-1 and C3-B1-2 of the S-hes run record, the user's rulings
/// (b)). Where two of the variance's growth factors m₁, m₂ and m₃, or one of them and 1, are equal, QuantLib's divided
/// differences of their powers are 0/0 and its price NaN: at α = β = γ = 0, all three 0, and in the integrated GARCH
/// β 0.9, α 0.1, γ 0, λ 0, where m₁ is 1. nanoql takes each divided difference at its limit, a polynomial in the factors,
/// continuous with QuantLib's prices with the factors 1e-8 and 1e-10 apart (QuantLib-SWIG 1.44); elsewhere QuantLib's
/// arithmetic. Two of QuantLib's defects are kept, documented on the engine: its fourth moment, whose kurtosis at the
/// deterministic limit (α = γ = 0) is about 3 − Σh where the log return is normal, so a call prices above Black-Scholes
/// at the same total variance; and its Edgeworth price, which leaves the no-arbitrage bounds where the skewness and
/// kurtosis are large, as on QuantLib's own model at λ 0.2 and a year. Both are QuantLib's prices, here bit for bit or
/// within the engine's last bits (the S-hes plan's G3 note on <c>pow</c>).
/// </summary>
public class AnalyticGjrGarchMomentTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AtEqualGrowthFactorsTheMomentsTakeTheirLimit()
    {
        // α = β = γ = 0: QuantLib NaN; 0.7667594859701214 at β 1e-8, 0.7667594844179741 at β 1e-10
        double zero = Price(0.05, 2e-4, 2e-6, 0.0, 0.0, 0.0, 0.1, 90, 50.0);
        AssertClose(0.7667594844179741, zero, 1e-10);

        // β 0.9, α 0.1: m₁ = 1, QuantLib NaN; 3.124146126257185 at β 0.9 − 1e-8, 3.1241024548585905 at β 0.9 − 1e-6
        double integrated = Price(0.05, 2e-4, 2e-6, 0.1, 0.9, 0.0, 0.0, 90, 50.0);
        AssertClose(3.124146126257185, integrated, 2e-7);
        Assert.True(integrated > 3.124146126257185, $"{integrated:R}");
    }

    [Fact]
    public void AtTheDeterministicLimitTheKurtosisIsQuantLibs()
    {
        // α = γ = 0, β 0.93, the daily variance 2e-4 constant: the log return is normal, Black-Scholes at the total
        // variance 0.073 gives 9.379353144708883, 6.5502454217632415 and 4.41542395068625; QuantLib's engine 0.12% to
        // 0.29% above them, a kurtosis of about 3 − Σh
        AssertClose(9.390880776594575, Price(0.05, 2e-4, 1.4e-5, 0.0, 0.93, 0.0, 0.0, 365, 45.0), 1e-10);
        AssertClose(6.565429773284139, Price(0.05, 2e-4, 1.4e-5, 0.0, 0.93, 0.0, 0.0, 365, 50.0), 1e-10);
        AssertClose(4.428222321076473, Price(0.05, 2e-4, 1.4e-5, 0.0, 0.93, 0.0, 0.0, 365, 55.0), 1e-10);
    }

    [Fact]
    public void ThePriceCanLeaveTheNoArbitrageBoundsAsQuantLibs()
    {
        // QuantLib's gjrgarchmodel test model at λ 0.2 from its stationary variance, a year, a flat 4% curve: QuantLib's
        // call at 50 is negative, its put below its intrinsic value (tests/vectors/pricingengines/vanilla/
        // analyticgjrgarchengine.json); a Monte Carlo of the model prices them positive
        double call = Price(0.04, 0.0004096412811524548, 2e-6, 0.024, 0.93, 0.059, 0.2, 365, 50.0);
        double put = Price(0.04, 0.0004096412811524548, 2e-6, 0.024, 0.93, 0.059, 0.2, 365, 50.0, OptionType.Put);
        AssertClose(-4.576787609627524, call, 1e-10);
        AssertClose(-6.537315652011365, put, 1e-10);
    }

    private static double Price(
        double rate, double v0, double omega, double alpha, double beta, double gamma, double lambda, int days, double strike,
        OptionType type = OptionType.Call)
    {
        var process = new GJRGARCHProcess(
            new FlatForward(Today, rate, A365), new FlatForward(Today, 0.0, A365), 50.0, v0, omega, alpha, beta, gamma, lambda, 365.0);
        var option = new VanillaOption(new PlainVanillaPayoff(type, strike), new EuropeanExercise(Today + days));
        return AnalyticGJRGARCHEngine.Calculate(option, Context, new GJRGARCHModel(process)).NPV!.Value;
    }

    private static void AssertClose(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(actual - expected) <= tolerance * Math.Abs(expected), $"{actual:R} for {expected:R}");
}
