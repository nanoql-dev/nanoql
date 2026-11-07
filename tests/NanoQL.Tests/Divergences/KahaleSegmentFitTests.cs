// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Kahale's interpolated segments: QuantLib's fit of a segment's function accepts any root of its solver
/// (<c>kahalesmilesection.cpp:150-173</c>), even where the function's forward runs to 10¹⁴ and more, whose Black call
/// and constant then cancel, so its prices there are the cancellation's. On the wheel, over an unshifted SABR smile at a
/// forward of 4% with an exercise time of 5 (α 0.02, β 0.5, ν 0.3, ρ −0.2), QuantLib's interpolated section prices the
/// call struck at 0.4%, a grid point, at 0.0361328125 where the source gives 0.0360042, and seven strikes from 0.46% to
/// 1.46% below their intrinsic value, by up to 8.1e-4. nanoql solves such a segment again, and evaluates it, anchored
/// at its left end's price, the Black call's change taken through complementary normal integrals, so no term cancels;
/// a segment whose rounding is within 1e-8 of its ends' prices, relative, is QuantLib's, solved and evaluated as
/// QuantLib's. QuantLib's checks: the source's prices at the core's grid points, which every segment reproduces, and
/// QuantLib's own bits where its segments are sound, at any scale.
/// </summary>
public class KahaleSegmentFitTests
{
    [Fact]
    public void ACancellingSegmentIsFittedWithoutTheCancellation()
    {
        const double forward = 0.04;
        var source = new SabrSmileSection(5.0, forward, [0.02, 0.5, 0.3, -0.2]);
        var kahale = new KahaleSmileSection(source, null, true);
        Assert.Equal((3, 13), kahale.CoreIndices());

        // The core's grid points, the moneyness 0.25 and 0.4 erased as unsolved: the source's prices, where QuantLib's at
        // 0.4% is its cancellation's.
        foreach (double m in new[] { 0.1, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.25, 1.5, 1.75, 2.0 })
        {
            double k = forward * m;
            Assert.True(Math.Abs(kahale.OptionPrice(k) - source.OptionPrice(k)) <= 1e-12, $"{k}: {kahale.OptionPrice(k)} against {source.OptionPrice(k)}");
        }

        // QuantLib's bits where its segments are sound, on the wheel.
        Assert.Equal(0.02015567637929826, kahale.OptionPrice(0.02));
        Assert.Equal(0.0036743517307096622, kahale.OptionPrice(0.04));
        Assert.Equal(0.00019131649119835691, kahale.OptionPrice(0.06));

        // Arbitrage free: calls between their intrinsic value and the forward, digitals in [0, 1] and falling.
        double previous = 1.0;
        for (double k = 1e-4; k < 6.0 * forward; k += 5e-4)
        {
            double call = kahale.OptionPrice(k);
            Assert.True(call >= Math.Max(forward - k, 0.0) - 1e-10 && call <= forward + 1e-10, $"{k}: {call}");
            double digital = kahale.DigitalOptionPrice(k);
            Assert.True(digital >= -1e-8 && digital <= previous + 1e-8, $"{k}: {digital} after {previous}");
            previous = digital;
        }
    }

    [Fact]
    public void ASoundSegmentIsQuantLibsAtAnyScale()
    {
        // A lognormal SABR smile at a forward of 5e8 (α 0.3, β 1, ν 0.3, ρ −0.3, T 1): QuantLib's on the wheel, in its
        // sound segments from 70% of the forward up. Its segment from 10% to 70% misses its end on QuantLib's fit and is
        // refitted (KahaleMissedSegmentTests).
        const double forward = 5e8;
        var kahale = new KahaleSmileSection(new SabrSmileSection(1.0, forward, [0.3, 1.0, 0.3, -0.3]), null, true);
        (double M, double Price)[] quantLib =
        [
            (0.75, 138157425.46990943), (1.0, 59602159.70072213), (1.25, 20794963.414857432), (1.5, 6457470.895488158),
            (2.0, 610348.9711679919),
        ];
        foreach ((double m, double price) in quantLib)
        {
            Assert.Equal(price, kahale.OptionPrice(forward * m));
        }
    }
}
