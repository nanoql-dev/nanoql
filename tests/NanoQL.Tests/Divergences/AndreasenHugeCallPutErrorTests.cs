// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Andreasen-Huge's call-put calibration error by strike (the S-van plan's VR4 (xvii)). QuantLib takes each option's
/// put or call error by comparing the forward with the j-th grid point, <c>fwd &gt; gridInFwd_[j]</c>
/// (<c>andreasenhugevolatilityinterpl.cpp:418</c>), about an eighth of the smallest strike, so it always takes the puts':
/// on calls struck 101.5 to 130 from 15 October 2026, all above the forward, it gives (1.6410985795898742e-08,
/// 1.148950832474151e-06, 2.1308681968043031e-07), where each option's vega-scaled error on its out-of-the-money side,
/// from the calibrated prices, gives (1.6414905070833644e-08, 1.1491274031493572e-06, 2.1310003360549513e-07)
/// [executed: the wheel, <c>scratchpad\c5\probe_ah_callput.py</c>]. nanoql compares the forward with the strike, the
/// out-of-the-money side; QuantLib's check, the per-option errors from the calibrated prices, gives nanoql's values.
/// Where every strike is below the forward the two choices agree, and QuantLib's values stand within 1e-6 relative:
/// the calibration's errors are ill-conditioned, and the C runtime and processor it runs on move them, by 1.8e-8 relative
/// on GitHub's windows-2022 runner, 5.6e-8 on macOS and 1.7e-8 on Linux (CI run 41146085965).
/// </summary>
public class AndreasenHugeCallPutErrorTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();
    private static readonly Date[] Expiries = [Today + 91, Today + 182, Today + 365];

    [Fact]
    public void TheErrorIsEachOptionsOutOfTheMoneySide()
    {
        // every strike above the forward: the calls' errors, QuantLib's check
        double[] above = [101.5, 105.0, 110.0, 120.0, 130.0];
        (AndreasenHugeVolatilityInterpl ah, AndreasenHugeCalibrationError e) = Calibrated(above);
        (double min, double max, double avg) = ByStrike(ah, above);
        AssertClose(min, e.Min, 1e-9);
        AssertClose(max, e.Max, 1e-9);
        AssertClose(avg, e.Avg, 1e-9);
        Assert.True(Math.Abs(e.Min - 1.6410985795898742e-08) > 1e-4 * e.Min);

        // the plan's five strikes about the forward: the smallest error, an out-of-the-money call's, by its call price;
        // the largest, a put's, QuantLib's
        double[] straddling = [80.0, 90.0, 100.0, 110.0, 120.0];
        (ah, e) = Calibrated(straddling);
        (min, _, _) = ByStrike(ah, straddling);
        AssertClose(min, e.Min, 1e-9);
        Assert.True(Math.Abs(e.Min - 2.04584952970147e-08) > 1e-3 * e.Min);
        AssertClose(1.7303849107531028e-06, e.Max, 1e-6);

        // every strike below the forward: the puts' errors, QuantLib's bits
        (_, e) = Calibrated([70.0, 80.0, 90.0, 95.0, 99.0]);
        AssertClose(2.898637688237428e-09, e.Min, 1e-6);
        AssertClose(3.920585301157897e-06, e.Max, 1e-6);
        AssertClose(9.976617913126538e-07, e.Avg, 1e-6);
    }

    private static (AndreasenHugeVolatilityInterpl Interpolation, AndreasenHugeCalibrationError Error) Calibrated(double[] strikes)
    {
        (VanillaOption, double)[] options =
        [
            .. Expiries.SelectMany(e => strikes.Select(k => (
                new VanillaOption(new PlainVanillaPayoff(OptionType.Call, k), new EuropeanExercise(e)), Volatility(k)))),
        ];
        var ah = new AndreasenHugeVolatilityInterpl(
            options,
            100.0,
            new FlatForward(Today, 0.02, Dc),
            new FlatForward(Today, 0.01, Dc),
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
            AndreasenHugeVolatilityInterplCalibrationType.CallPut);
        return (ah, ah.CalibrationError());
    }

    // each option's vega-scaled error from the calibrated prices, on its out-of-the-money side
    private static (double Min, double Max, double Avg) ByStrike(AndreasenHugeVolatilityInterpl ah, double[] strikes)
    {
        var diffs = new List<double>();
        foreach (Date e in Expiries)
        {
            double t = Dc.YearFraction(Today, e);
            double fwd = ah.Fwd(t), df = ah.RiskFreeRate.Discount(t);
            foreach (double k in strikes)
            {
                OptionType type = fwd > k ? OptionType.Put : OptionType.Call;
                var black = new BlackCalculator(new PlainVanillaPayoff(type, k), fwd, Volatility(k) * Math.Sqrt(t), df);
                diffs.Add(Math.Abs(ah.OptionPrice(t, k, type) - black.Value) / black.Vega(t));
            }
        }

        return (diffs.Min(), diffs.Max(), diffs.Average());
    }

    private static double Volatility(double strike) => 0.25 + (0.002 * (100.0 - strike));

    private static void AssertClose(double expected, double actual, double relative) =>
        Assert.True(
            Math.Abs(actual - expected) <= relative * Math.Abs(expected),
            $"{actual:R} differs from {expected:R} by more than {relative} relative");
}
