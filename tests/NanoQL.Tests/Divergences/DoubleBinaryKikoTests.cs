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
/// The double-barrier binary's KIKO and KOKI options (fixed). QuantLib sums their series to i = 999 and checks only the
/// last term, |term| &lt; 1e-8 (<c>analyticdoublebarrierbinaryengine.cpp:156-169</c>), while the terms decay as 1/i³:
/// at small variance it passes its check unconverged (a KOKI 8.4% low on [95, 105] at a volatility of 1% over two
/// years) or refuses ("serie did not converge sufficiently fast"). nanoql keeps QuantLib's sum, its value and bits,
/// wherever Abel's bound on the dropped tail is within 1e-11 of the sum, and elsewhere integrates the absorption flux
/// of QuantLib's own image-series killed density at the barrier, each image paired with its reflection, in log time
/// between the roots that bound its mass, on the adaptive Gauss-Kronrod rule scaled by a first estimate. Checks:
/// Kummer's transformation of QuantLib's series in 40 or 50 digits, the survival route on QuantLib's image series, and
/// the flux in 30 digits.
/// </summary>
public class DoubleBinaryKikoTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void QuantLibsTruncatedKokiIsTheSeriesSum()
    {
        // [95, 105] at a volatility of 1% over two years: QuantLib 0.7197737996 (-8.4%); the series summed in closed
        // form in 50 digits
        Assert.Equal(0.7858020892999, Value(DoubleBarrierType.KOKI, 100.0, 95.0, 105.0, 0.03, 0.0, 0.01, 730), 1e-12);
    }

    [Fact]
    public void TheCaseQuantLibRefusesIsPriced()
    {
        // [98, 102] at 0.7% over a year: QuantLib refuses; Kummer in 50 digits, the survival route 0.9224059135
        Assert.Equal(0.9224059135036, Value(DoubleBarrierType.KOKI, 100.0, 98.0, 102.0, 0.04, 0.01, 0.007, 365), 1e-12);
        Assert.Equal(1.685774624673e-11, Value(DoubleBarrierType.KIKO, 100.0, 98.0, 102.0, 0.04, 0.01, 0.007, 365), 1e-21);
    }

    [Fact]
    public void AtTheKummerFormsResonanceTheFluxMatchesTheSurvivalRoute()
    {
        // r = q = -0.6%, a volatility of 0.5% over a year, √β·Z = π(1 + 1e-8): the Kummer form's pole, the survival
        // route on QuantLib's image series 0.28524287506
        const double r = -0.006, sigma = 0.005;
        double beta = (-0.25 * Math.Pow((2 * (r - r) / (sigma * sigma)) - 1, 2)) - (2 * r / (sigma * sigma));
        double upper = 93.0 * Math.Exp(Math.PI / Math.Sqrt(beta) * (1 + 1e-8));
        Assert.Equal(0.28524287506, Value(DoubleBarrierType.KIKO, 93.5, 93.0, upper, r, r, sigma, 365), 1e-11);
    }

    [Fact]
    public void OnAPegLikeMarketTheFluxMatchesThe50DigitValue()
    {
        // [93, 107] at a volatility of 0.1% over 182 days, the spot near the upper barrier: 5.7081035321027e-17 in 50
        // digits by both the flux and the survival route; the tolerance's absolute floor would assert nothing, so the
        // check is relative
        double value = Value(DoubleBarrierType.KOKI, 106.9, 93.0, 107.0, 0.01, 0.03, 0.001, 182);
        Assert.True(Math.Abs(value - 5.7081035321027e-17) <= 1e-10 * 5.7081035321027e-17, $"{value:R}");
    }

    [Fact]
    public void TheSpotJustAboveTheLowerBarrierMatchesKummer()
    {
        // ln(S/L) = 1e-6 σ√T: 0.9999991111594 in 40 digits
        double spot = 80.0 * Math.Exp(2e-7);
        Assert.Equal(0.9999991111594, Value(DoubleBarrierType.KIKO, spot, 80.0, 120.0, 0.05, 0.02, 0.20, 365), 1e-12);
    }

    [Fact]
    public void ACorridorNarrowAgainstItsVarianceMatchesKummer()
    {
        // [99.9, 100.1] at 20% over five years: 0.4996243752218 in 40 digits; a fixed |n| <= 30 images gives 0.874
        Assert.Equal(0.4996243752218, Value(DoubleBarrierType.KIKO, 100.0, 99.9, 100.1, 0.05, 0.02, 0.20, 1825), 1e-12);
    }

    [Fact]
    public void QuantLibsOwnCaseOutsideAbelsBoundMovesByItsTruncation()
    {
        // testHaugValues' [80, 120] at 10% over 0.25 years, KIKO at unit cash: 4.543650637955e-06 in 40 digits, where
        // QuantLib's 999 terms are 1.4e-5 of the value off and its tolerance at cash 10 is 1e-4
        Assert.Equal(
            4.5436506379552e-06, Value(DoubleBarrierType.KIKO, 100.0, 80.0, 120.0, 0.05, 0.02, 0.10, 90, new Actual360()), 1e-17);
    }

    [Fact]
    public void ADriftDominatedMarketMatchesTheFluxIn30Digits()
    {
        // [93, 107] at 0.1%, r 0, q 5%, over two years: the drift carries the spot to the lower barrier in 1.45 years
        // (0.9999999...); the 30-digit flux 1, which an integral starting where the diffusion alone reaches the barrier
        // (3.76 years) misses
        Assert.Equal(1.0, Value(DoubleBarrierType.KIKO, 100.0, 93.0, 107.0, 0.0, 0.05, 0.001, 730), 1e-12);
        Assert.Equal(
            0.9345794392523, Value(DoubleBarrierType.KOKI, 100.0, 93.0, 107.0, 0.05, 0.0, 0.001, 730), 1e-12);
    }

    // The value at unit cash of a KIKO or KOKI option paying at the hit, its window open from the start
    private static double Value(
        DoubleBarrierType type,
        double spot,
        double lo,
        double hi,
        double r,
        double q,
        double sigma,
        int days,
        DayCounter? dayCounter = null)
    {
        DayCounter dc = dayCounter ?? A365;
        var process = new BlackScholesMertonProcess(
            spot,
            new FlatForward(Today, q, dc),
            new FlatForward(Today, r, dc),
            new BlackConstantVol(Today, new NullCalendar(), sigma, dc));
        var option = new DoubleBarrierOption(
            type, lo, hi, 0.0, new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0), new AmericanExercise(Today, Today + days));
        return AnalyticDoubleBarrierBinaryEngine.Calculate(option, Context, process).NPV!.Value;
    }
}
