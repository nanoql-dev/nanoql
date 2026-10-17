// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of Boost.Math's errors (playbook §2): a domain error on the caller's argument throws
/// <see cref="ArgumentOutOfRangeException"/>; a domain error on a stored parameter and an overflow throw
/// <see cref="NanoQLException"/>; each keeps Boost's message. A vector's <c>{"throws": true}</c> accepts either type,
/// so these facts assert them.
/// </summary>
public class BoostErrorTests
{
    [Fact]
    public void AProbabilityOutsideTheUnitIntervalIsTheCallersArgument()
    {
        var quantile = new MaddockInverseCumulativeNormal();

        foreach (double p in new[] { -0.1, 1.5 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => quantile.Evaluate(p));
            Assert.StartsWith(
                "Error in function boost::math::quantile(const normal_distribution<double>&, double): Probability "
                + $"argument is {p.ToString(System.Globalization.CultureInfo.InvariantCulture)}, but must be >= 0 and <= 1 !",
                e.Message,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ANaNToTheCdfIsTheCallersArgument()
    {
        var cdf = new MaddockCumulativeNormal();

        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => cdf.Evaluate(double.NaN));
        Assert.StartsWith(
            "Error in function boost::math::cdf(const normal_distribution<double>&, double): Random variate x is NaN, "
            + "but must be finite!",
            e.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnInfiniteMeanIsAStoredParameter()
    {
        // QuantLib's constructors check only sigma; Boost's normal_distribution refuses the mean when evaluated.
        var cdf = new MaddockCumulativeNormal(double.PositiveInfinity, 1.0);
        var quantile = new MaddockInverseCumulativeNormal(double.NegativeInfinity, 1.0);

        NanoQLException e = Assert.Throws<NanoQLException>(() => cdf.Evaluate(0.0));
        Assert.Equal(
            "Error in function boost::math::normal_distribution<double>::normal_distribution: Location parameter is "
            + "Infinity, but must be finite!",
            e.Message);
        e = Assert.Throws<NanoQLException>(() => quantile.Evaluate(0.5));
        Assert.Equal(
            "Error in function boost::math::normal_distribution<double>::normal_distribution: Location parameter is "
            + "-Infinity, but must be finite!",
            e.Message);
    }

    [Fact]
    public void TheQuantileOfZeroOrOneIsAnOverflow()
    {
        var quantile = new MaddockInverseCumulativeNormal(3.0, 2.0);

        foreach (double p in new[] { 0.0, 1.0 })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => quantile.Evaluate(p));
            Assert.Equal("Error in function boost::math::erfc_inv<double>(double, double): Overflow Error", e.Message);
        }
    }
}
