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
/// The analytic double-barrier engine at low volatility (the S-bar plan's BR5 (xvi), fixed under R8). The series' powers
/// (U/L)^(n·μ₁) overflow where μ₁ = 2b/σ² + 1 is large, and the product with a vanishing normal difference is NaN, which
/// QuantLib's <c>std::max(0, ·)</c> turns into 0: a knock-out call whose forward stays inside the corridor prices 0 at
/// σ = 1e-4 and at every σ below about 1.6% at a 4% carry. nanoql takes each term's power and normal difference in logs
/// where the power overflows, QuantLib's technique in the binary barrier engine, each difference of same-signed
/// arguments from the small tail, and follows the forward below machine-epsilon variance. Checks: the deterministic
/// limit D·(F − K), and continuity with QuantLib's finite values above the overflow; and the image series in 50 digits
/// where the small tail keeps the digits a plain difference cancels, a call at 101 on [98, 102] at spot 100.5, r 4%, q
/// 1%, σ 0.7% over a year (the reviewer's r3d case, F3-1): 0.00590299270662953, where QuantLib's five terms give
/// 0.0059033353, 5.8e-5 off, and its 50 terms 0, by overflow (the S-bar run's <c>c1_f31_knockout.py</c>).
/// </summary>
public class DoubleBarrierLowVolatilityTests
{
    private static readonly Date Today = new(1, January, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise Exercise = new(Today + 365);

    [Fact]
    public void AKnockOutWhoseForwardStaysInsideIsWorthItsForwardPayoff()
    {
        // r 5%, q 1%, [80, 125], a call at 100: the limit e^(-0.05)(100e^0.04 - 100) = 3.882041; QuantLib 0 at 1e-4
        double limit = Math.Exp(-0.05) * ((100.0 * Math.Exp(0.04)) - 100.0);
        Assert.Equal(limit, KnockOut(1e-4), 1e-9);
        Assert.Equal(limit, KnockOut(0.0), 1e-12);
    }

    [Fact]
    public void TheValueIsContinuousThroughTheOverflow()
    {
        // QuantLib is finite above about 1.6% and 0 below; the values approach the limit as the volatility vanishes
        double limit = Math.Exp(-0.05) * ((100.0 * Math.Exp(0.04)) - 100.0);
        double previous = Math.Abs(KnockOut(0.03) - limit);
        foreach (double sigma in new[] { 0.02, 0.016, 0.012, 0.008, 0.004, 0.001 })
        {
            double value = KnockOut(sigma);
            Assert.True(double.IsFinite(value) && value > 0.0, $"sigma {sigma}: {value}");
            double distance = Math.Abs(value - limit);
            Assert.True(distance <= previous, $"sigma {sigma}: {value}, {distance} from the limit, after {previous}");
            previous = distance;
        }

        Assert.True(previous <= 1e-8, $"{previous}");
    }

    [Fact]
    public void TheSmallTailKeepsTheDigitsAPlainDifferenceCancels()
    {
        var process = new BlackScholesMertonProcess(
            100.5,
            new FlatForward(Today, 0.01, A365),
            new FlatForward(Today, 0.04, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.007, A365));
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 98.0, 102.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 101.0), Exercise);
        double value = AnalyticDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        Assert.Equal(0.00590299270662953, value, 1e-12);
    }

    private static double KnockOut(double sigma)
    {
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.01, A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), sigma, A365));
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 125.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0), Exercise);
        return AnalyticDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
    }
}
