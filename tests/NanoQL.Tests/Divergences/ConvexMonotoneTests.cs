// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The convex-monotone interpolation's sections. Hagan and West's method defines a section's forwards by two
/// properties: the section averages its value, and the forwards are continuous at the nodes. QuantLib's split of a
/// section's quadratic to keep it non-negative breaks both where it splits a quadratic that never dips below 0 inside
/// the section; its averages' discriminant, 0 in exact arithmetic beside a floored end forward, rounds a hair below 0,
/// and the unsplit quadratic dips below 0; and positivity forced on negative averages floors their sections and end
/// forwards at 0, breaking the averages. nanoql splits only where the quadratic dips, takes a discriminant within 1e-12
/// bAv² of 0 as 0, and forces no positivity on a section of negative average or the end forward beside one. QuantLib's
/// values are its own class's; each section is read through its own helper, as QuantLib's lookup takes the next
/// section's helper at the node itself: its average by its primitive just before its right end, and its forward there
/// as the limit of two readings before it.
/// </summary>
public class ConvexMonotoneTests
{
    // A section's own average: its helper's primitive just before the section's right end.
    private static double Average(Interpolation f, double[] x, int i) =>
        (f.Primitive(x[i] - 1e-13, true) - f.Primitive(x[i - 1], true)) / (x[i] - x[i - 1]);

    // The jump at a node from the section before it to the section after it, which QuantLib's lookup reads at the node
    // itself: the section before read at two offsets before the node and taken to its limit, linearly in the offset, so
    // no fixed offset's gap is read as continuity (§2.6).
    private static double Jump(Interpolation f, double[] x, int i)
    {
        double near = f.Evaluate(x[i] - 1e-11, true), far = f.Evaluate(x[i] - 1e-9, true);
        double left = near - ((far - near) / 99.0);
        return f.Evaluate(x[i], true) - left;
    }

    [Fact]
    public void AQuadraticIsSplitOnlyWhereItDipsBelowZero()
    {
        // Forwards of 3%, 2% and 1% at quadraticity 0, monotonicity 1, positivity forced: QuantLib splits the first two
        // sections' falling quadratics, which never dip below 0, and their averages fall to 1.68% and 1.28%, the
        // forwards jumping by 1.99% and 1.12% at the nodes
        double[] x = [0.0, 1.0, 2.0, 3.0], y = [0.03, 0.03, 0.02, 0.01];
        var f = new ConvexMonotoneInterpolation(x, y, 0.0, 1.0, true);
        double[] quantLibAverages = [0.016807845623833775, 0.01284614929393274, 0.0099999999999992525];
        double[] quantLibJumps = [0.019922467120862708, 0.011203656268773511];
        for (int i = 1; i <= 3; i++)
        {
            Assert.Equal(y[i], Average(f, x, i), 12);
        }

        // QuantLib's split sections miss their averages by a third and more; its last, unsplit, keeps its own
        Assert.True(Math.Abs(quantLibAverages[0] - y[1]) > 0.013 && Math.Abs(quantLibAverages[1] - y[2]) > 0.007);
        Assert.Equal(y[3], quantLibAverages[2], 12);

        for (int i = 1; i <= 2; i++)
        {
            Assert.True(Math.Abs(Jump(f, x, i)) < 1e-12, $"jump {Jump(f, x, i)} at node {i}, QuantLib's {quantLibJumps[i - 1]}");
        }

        // without forced positivity no section is split, and QuantLib's averages are right: nanoql's are the same
        var g = new ConvexMonotoneInterpolation(x, y, 0.0, 1.0, false);
        for (int i = 1; i <= 3; i++)
        {
            Assert.Equal(y[i], Average(g, x, i), 12);
        }
    }

    [Fact]
    public void EverySectionKeepsItsAverageAndTheForwardsTheirContinuity()
    {
        // 100 random positive curves of 8 sections at quadraticity 0 with positivity forced, where QuantLib misses a
        // section's average on 261 of 400 and jumps at a node on 190: nanoql's sections each average their value, and
        // the forwards meet at every node, the gap read at its limit
        var random = new Random(20261027);
        for (int curve = 0; curve < 100; curve++)
        {
            double[] x = new double[9], y = new double[9];
            for (int i = 1; i <= 8; i++)
            {
                x[i] = x[i - 1] + 0.25 + (2.0 * random.NextDouble());
                y[i] = 0.001 + (0.06 * random.NextDouble());
            }

            var f = new ConvexMonotoneInterpolation(x, y, 0.0, 0.7, true);
            for (int i = 1; i <= 8; i++)
            {
                Assert.True(Math.Abs(Average(f, x, i) - y[i]) < 1e-11, $"curve {curve} section {i}: {Average(f, x, i)} for {y[i]}");
                Assert.True(f.Evaluate(x[i] - ((x[i] - x[i - 1]) / 2), true) >= 0.0, $"curve {curve} section {i} below 0");
            }

            for (int i = 1; i <= 7; i++)
            {
                Assert.True(Math.Abs(Jump(f, x, i)) < 1e-9, $"curve {curve} node {i}: jump {Jump(f, x, i)}");
            }
        }
    }

    [Fact]
    public void ADiscriminantRoundedBelowZeroIsZero()
    {
        // Averages of 0.1%, 2.01% and 2.51% at QuantLib's default: the first end forward floored at 0, the averages'
        // discriminant, 0 in exact arithmetic, comes to -1.39e-17, and QuantLib's unsplit quadratic dips to -6.67 bp at
        // 0.294; nanoql's stays non-negative, every average kept
        double[] x = [0.0, 1.0, 2.0, 3.0], y = [0.0, 0.001, 0.0201, 0.0251];
        var f = new ConvexMonotoneInterpolation(x, y, 0.3, 0.7, true);
        double quantLibMinimum = -0.00066669498;
        double minimum = Enumerable.Range(0, 3001).Select(k => f.Evaluate(3.0 * k / 3000.0, true)).Min();
        Assert.True(minimum >= 0.0, $"minimum {minimum}, QuantLib's {quantLibMinimum}");
        Assert.True(f.Evaluate(0.294, true) >= 0.0);
        for (int i = 1; i <= 3; i++)
        {
            Assert.Equal(y[i], Average(f, x, i), 12);
        }
    }

    [Fact]
    public void NegativeAveragesKeepTheirAverages()
    {
        // Averages of -0.5% to -0.2% at QuantLib's default: QuantLib floors the first end forward and forces the
        // sections positive, its first section averaging -0.449%; nanoql forces no positivity there, so the curve is
        // the unforced one, every average kept
        double[] x = [0.0, 1.0, 2.0, 3.0, 4.0], y = [0.0, -0.005, -0.004, -0.003, -0.002];
        var f = new ConvexMonotoneInterpolation(x, y, 0.3, 0.7, true);
        var unforced = new ConvexMonotoneInterpolation(x, y, 0.3, 0.7, false);
        double quantLibFirstAverage = -0.0044854999999995497;
        Assert.Equal(-0.005, Average(f, x, 1), 12);
        Assert.True(Math.Abs(quantLibFirstAverage - Average(f, x, 1)) > 5e-4);
        for (int k = 0; k <= 400; k++)
        {
            double t = 4.0 * k / 400.0;
            Assert.Equal(unforced.Evaluate(t, true), f.Evaluate(t, true));
        }
    }

    [Fact]
    public void ANegativeRateCurveReadsNoSpuriousForward()
    {
        // QuantLib's PiecewiseConvexMonotoneForward on deposits at -0.5% to 0% on 25 September 2019 reprices them
        // (quote errors 9.98e-14) but reads a one-day forward of -12.05% 30 days out: nanoql's reprices them and its
        // one-day forwards stay within the deposits' range
        var today = new Date(25, Month.September, 2019);
        var context = new EvaluationContext { EvaluationDate = today };
        double[] rates = [-0.005, -0.004, -0.003, -0.002, -0.001, 0.0];
        int[] months = [1, 2, 3, 6, 9, 12];
        BootstrapHelper<YieldTermStructure>[] helpers =
        [
            .. rates.Zip(months).Select(p => new DepositRateHelper(
                p.First, new Period(p.Second, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing,
                false, new Actual360(), context)),
        ];
        var curve = new PiecewiseConvexMonotoneForward(today, helpers, new Actual360(), context);
        double quantLibMinimum = -0.12052960034072857;
        foreach (BootstrapHelper<YieldTermStructure> h in helpers)
        {
            Assert.True(Math.Abs(h.SetTermStructure(curve).QuoteError(context)) < 1e-10);
        }

        double minimum = Enumerable.Range(0, 120)
            .Select(i => curve.ForwardRate(today + (3 * i), today + (3 * i) + 1, new Actual360(), Compounding.Simple).Rate)
            .Min();
        Assert.True(minimum > -0.006, $"minimum one-day forward {minimum}, QuantLib's {quantLibMinimum}");
    }
}
