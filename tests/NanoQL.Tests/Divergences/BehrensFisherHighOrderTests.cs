// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The convolved Student t past order 171, its factorial and power scaled together (the S-xcr plan's XR8 (xxvi)):
/// QuantLib's series divides a factorial by a power, both of which leave the doubles from degree 172 on
/// (<c>convolvedstudentt.cpp:124-127</c>), so on two t's of order 173 its cumulative reads −∞ and its inverse refuses
/// ("root not bracketed"). QuantLib's values are the S-xcr run's <c>c1_facts.cpp</c>; the check is the same series in
/// 50 digits (the run's <c>premises\bf_ref.py</c>, after the planner's <c>xp_bf_precision.py</c>), within R7: the scaled
/// terms carry the logarithms' rounding through the series' alternating sum, 4e-12 at most here.
/// </summary>
public class BehrensFisherHighOrderTests
{
    [Theory]
    [InlineData(new[] { 173, 173 }, new[] { 0.6, 0.6 }, -1.0, 0.12038203631177926933, 0.23471643760775215538)]
    [InlineData(new[] { 173, 173 }, new[] { 0.6, 0.6 }, 0.5, 0.72139232682773853801, 0.39402935909567745303)]
    [InlineData(new[] { 173, 173 }, new[] { 0.6, 0.6 }, 2.0, 0.99033552618425557068, 0.030014917484255393846)]
    [InlineData(new[] { 301, 301 }, new[] { 0.3, 0.9 }, -1.0, 0.14641142215773372075, 0.24096565019993618787)]
    [InlineData(new[] { 301, 301 }, new[] { 0.3, 0.9 }, 2.0, 0.98209857619803162324, 0.045968498698160977057)]
    [InlineData(new[] { 1001 }, new[] { 1.0 }, 0.5, 0.69140751452130050784, 0.3519389569584092623)]
    [InlineData(new[] { 173, 3 }, new[] { 0.8, 0.5 }, -1.0, 0.16986156844607528714, 0.23331240664573317927)]
    public void TheSeriesIsAccuratePastOrder171(int[] degrees, double[] factors, double x, double cdf, double density)
    {
        // QuantLib, two t's of order 173 at 0.6, 0.6: cdf(0.5) -inf; inverse(0.9) throws "root not bracketed"
        var c = new CumulativeBehrensFisher(degrees, factors);

        Assert.Equal(cdf, c.Value(x), 1e-10 * cdf);
        Assert.Equal(density, c.Density(x), 1e-10 * density);
    }

    [Fact]
    public void TheInverseFindsItsRoot()
    {
        var inverse = new InverseCumulativeBehrensFisher([173, 173], [0.6, 0.6], 1e-12);
        var c = new CumulativeBehrensFisher([173, 173], [0.6, 0.6]);

        Assert.Equal(0.9, c.Value(inverse.Value(0.9)), 1e-12);
    }
}
