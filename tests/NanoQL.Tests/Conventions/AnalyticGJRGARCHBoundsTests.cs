// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="AnalyticGJRGARCHEngine.CalculateWithinBounds(VanillaOption, EvaluationContext)"/>, the analytic price
/// refused outside the no-arbitrage bounds beyond rounding, on QuantLib's own model (the GJR-GARCH test's v0, ω, α, β
/// and γ; spot 50, rates 4%), dividend yields 0 and 2%, 1 to 730 days, strikes 1 to 250, calls and puts: 448 prices a
/// λ. Inside the bounds it is <see cref="AnalyticGJRGARCHEngine.Calculate(VanillaOption, EvaluationContext)"/>'s price
/// bit for bit, the 12 a λ outside only by rounding among them; outside it refuses 136, 132, 128, 156 and 196 at λ 0
/// to 0.2: both wings at every λ, where the expansion's expected spot misses the forward, and the runaway prices from
/// 0.15.
/// </summary>
public class AnalyticGJRGARCHBoundsTests
{
    // machine epsilon, QuantLib's QL_EPSILON
    private const double Eps = 2.220446049250313e-16;
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly int[] Days = [1, 2, 7, 30, 90, 180, 365, 730];
    private static readonly double[] Strikes = [1, 5, 10, 20, 30, 40, 45, 50, 55, 60, 75, 100, 150, 250];

    [Theory]
    [InlineData(0.0, 136)]
    [InlineData(0.05, 132)]
    [InlineData(0.1, 128)]
    [InlineData(0.15, 156)]
    [InlineData(0.2, 196)]
    public void InsideTheBoundsItIsTheAnalyticPriceAndOutsideItRefuses(double lambda, int refused)
    {
        int outside = 0, rounding = 0;
        foreach (double q in new[] { 0.0, 0.02 })
        {
            var engine = new AnalyticGJRGARCHEngine(Model(q, lambda));
            foreach (int d in Days)
            {
                double dr = Math.Exp(-0.04 * d / 365.0), dq = Math.Exp(-q * d / 365.0);
                foreach (double k in Strikes)
                {
                    foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                    {
                        var option = new VanillaOption(new PlainVanillaPayoff(type, k), new EuropeanExercise(Today + d));
                        double price = engine.Calculate(option, Context).NPV!.Value;
                        double spot = 50.0 * dq, strike = k * dr;
                        double lo = Math.Max(0.0, type == OptionType.Call ? spot - strike : strike - spot);
                        double hi = type == OptionType.Call ? spot : strike;
                        double excess = Math.Max(lo - price, price - hi);
                        if (excess > 8.0 * Eps * Math.Max(spot, strike))
                        {
                            outside++;
                            var e = Assert.Throws<NanoQLException>(() => engine.CalculateWithinBounds(option, Context));
                            Assert.StartsWith("the analytic price (", e.Message, StringComparison.Ordinal);
                            Assert.EndsWith("] beyond rounding", e.Message, StringComparison.Ordinal);
                        }
                        else
                        {
                            rounding += excess > 0.0 ? 1 : 0;
                            Assert.Equal(price, engine.CalculateWithinBounds(option, Context).NPV!.Value);
                        }
                    }
                }
            }
        }

        Assert.Equal(refused, outside);
        Assert.Equal(12, rounding);
    }

    [Fact]
    public void TheStaticFormIsTheInstancesAndTheMessageNamesTheBounds()
    {
        GJRGARCHModel model = Model(0.0, 0.0);

        // a year's deep in-the-money call at λ 0: 49.023883389526745 below its bound 50 - 1 D_r, 49.03921056084768
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 1.0), new EuropeanExercise(Today + 365));
        var e = Assert.Throws<NanoQLException>(() => AnalyticGJRGARCHEngine.CalculateWithinBounds(call, Context, model));
        Assert.Equal(
            "the analytic price (49.023883389526745) leaves the no-arbitrage bounds [49.03921056084768, 50] "
            + "beyond rounding",
            e.Message);

        var atTheMoney = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Call, 50.0), new EuropeanExercise(Today + 365));
        Assert.Equal(
            new AnalyticGJRGARCHEngine(model).CalculateWithinBounds(atTheMoney, Context).NPV!.Value,
            AnalyticGJRGARCHEngine.CalculateWithinBounds(atTheMoney, Context, model).NPV!.Value);
    }

    // QuantLib's model: the GJR-GARCH test's stationary variance and parameters, spot 50, a flat 4% curve.
    private static GJRGARCHModel Model(double q, double lambda) =>
        new(new GJRGARCHProcess(
            new FlatForward(Today, 0.04, A365),
            new FlatForward(Today, q, A365),
            50.0,
            0.0004096412811524548,
            2e-6,
            0.024,
            0.93,
            0.059,
            lambda,
            365.0));
}
