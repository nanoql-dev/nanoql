// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Kahale's left wing on a shifted source: after fitting the wing, QuantLib's section checks a digital at (k₁ −
/// shift)/2 (<c>kahalesmilesection.cpp:109-110</c>), k₁ the wing's shifted end, the midpoint of [0, k₁ − shift] where
/// the wing spans [−shift, k₁ − shift]; once the core's left strike is negative unshifted the point lies in the core,
/// the check fails and the core moves right, the wing replacing the source's prices below it. nanoql checks at the
/// wing's midpoint, (k₁ − 2·shift)/2, QuantLib's expression at a shift of 0. QuantLib's check: its own section on the
/// smile restated unshifted (the forward and the strikes plus the shift, no shift), whose prices at K + shift a shifted
/// section's at K equal. On the wheel, over a SABR smile at a forward of 1% shifted by 3%, QuantLib's shifted section's
/// call at -1% is 0.020066641655499824 where its section restated unshifted gives 0.02015567637929827 at 2%; with
/// interpolation, on a SABR smile at an exercise time of 1 that QuantLib's interpolation fits without arbitrage once
/// restated unshifted, its shifted section's call at 0% is 0.010484248860552634 against 0.015632415044761726 at 3%.
/// </summary>
public class KahaleLeftWingTests
{
    private const double Forward = 0.01, Shift = 0.03, T = 5.0;
    private static readonly double[] Sabr = [0.02, 0.5, 0.3, -0.2];

    [Fact]
    public void AShiftedSectionIsItsUnshiftedRestatement()
    {
        var shifted = new KahaleSmileSection(new SabrSmileSection(T, Forward, Sabr, Shift));
        var unshifted = new KahaleSmileSection(new SabrSmileSection(T, Forward + Shift, Sabr));
        foreach (double k in new[] { -0.025, -0.02, -0.01, 0.0, 0.01, 0.02, 0.05 })
        {
            Assert.True(Math.Abs(shifted.OptionPrice(k) - unshifted.OptionPrice(k + Shift)) <= 1e-15, $"{k}: {shifted.OptionPrice(k)} against {unshifted.OptionPrice(k + Shift)}");
        }

        // QuantLib's section restated unshifted, at 0.5%, 1%, 2% and 3% (its bits at a shift of 0).
        (double K, double Price)[] quantLib = [(-0.025, 0.03500481346819129), (-0.02, 0.030023375483622772), (-0.01, 0.02015567637929827), (0.0, 0.010811753880004556)];
        foreach ((double k, double price) in quantLib)
        {
            Assert.True(Math.Abs(shifted.OptionPrice(k) - price) <= 1e-15, $"{k}: {shifted.OptionPrice(k)} against {price}");
        }

        // QuantLib's shifted section's call at -1%.
        Assert.True(Math.Abs(shifted.OptionPrice(-0.01) - 0.020066641655499824) > 5e-5);
    }

    [Fact]
    public void AShiftedInterpolatedSectionIsItsUnshiftedRestatement()
    {
        // A smile QuantLib's interpolation fits without arbitrage once restated unshifted (no call below its intrinsic
        // value or above the forward, its digitals in [0, 1] and falling), though its one core segment, from 0.2% to 6%
        // restated, misses its end by 6.5e-6 of the price, so it is refitted (KahaleMissedSegmentTests).
        double[] sabr = [0.04, 0.5, 0.4, -0.2];
        var shifted = new KahaleSmileSection(new SabrSmileSection(1.0, Forward, sabr, Shift), null, true);
        var unshifted = new KahaleSmileSection(new SabrSmileSection(1.0, Forward + Shift, sabr), null, true);
        foreach (double k in new[] { -0.028, -0.015, -0.01, 0.0, 0.01, 0.02, 0.05 })
        {
            Assert.True(Math.Abs(shifted.OptionPrice(k) - unshifted.OptionPrice(k + Shift)) <= 1e-15, $"{k}: {shifted.OptionPrice(k)} against {unshifted.OptionPrice(k + Shift)}");
        }

        // QuantLib's section restated unshifted: its bits in the wings, at 0.1% and 6.5%, and at the core's left end,
        // 0.2%; inside the refitted segment, at 1.5%, 3%, 4% and 5%, within 1e-7 of the price.
        (double K, double Price)[] quantLib =
        [
            (-0.029, 0.03900000041394236), (-0.028, 0.038000004717865236), (0.035, 3.3817228444046183e-93),
        ];
        foreach ((double k, double price) in quantLib)
        {
            Assert.True(Math.Abs(shifted.OptionPrice(k) - price) <= 1e-15, $"{k}: {shifted.OptionPrice(k)} against {price}");
        }

        (double K, double Price)[] refitted =
        [
            (-0.015, 0.02726856343072976), (0.0, 0.015632415044761726), (0.01, 0.00913195575587826), (0.02, 0.003947362270802203),
        ];
        foreach ((double k, double price) in refitted)
        {
            Assert.True(Math.Abs(shifted.OptionPrice(k) - price) <= 1e-7 * price, $"{k}: {shifted.OptionPrice(k)} against {price}");
        }

        // QuantLib's shifted interpolated section at 0%.
        Assert.True(Math.Abs(shifted.OptionPrice(0.0) - 0.010484248860552634) > 5e-3);
    }
}
