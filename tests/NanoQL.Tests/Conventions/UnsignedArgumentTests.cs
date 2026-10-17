// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Where QuantLib takes an unsigned value (<c>Size</c>, <c>Natural</c>, <c>BigNatural</c>), nanoql takes an
/// <see cref="int"/> or a <see cref="long"/> and range-checks it on input (playbook §4), throwing
/// <see cref="ArgumentOutOfRangeException"/> with the parameter's name. QuantLib can't be given these values, so no
/// vector holds them.
/// </summary>
public class UnsignedArgumentTests
{
    [Fact]
    public void APoissonCountIsInTheUnsigned32BitRange()
    {
        var pmf = new PoissonDistribution(1.0);
        var cdf = new CumulativePoissonDistribution(1.0);

        foreach (long k in new[] { -1L, 4294967296L, long.MinValue })
        {
            Assert.Equal("k", Assert.Throws<ArgumentOutOfRangeException>(() => pmf.Evaluate(k)).ParamName);
            Assert.Equal("k", Assert.Throws<ArgumentOutOfRangeException>(() => cdf.Evaluate(k)).ParamName);
        }
    }

    [Fact]
    public void AFactorialOrAPrimeIndexIsNotNegative()
    {
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => Factorial.Get(-1)).ParamName);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => Factorial.Ln(-1)).ParamName);
        Assert.Equal(
            "absoluteIndex",
            Assert.Throws<ArgumentOutOfRangeException>(() => PrimeNumbers.Get(-1)).ParamName);
    }

    [Fact]
    public void ASizeOrAnIndexIsNotNegative()
    {
        var v = new Vector(3, 1.0);
        var m = new Matrix(2, 2, 1.0);

        Assert.Equal("size", Assert.Throws<ArgumentOutOfRangeException>(() => new Vector(-1)).ParamName);
        Assert.Equal("size", Assert.Throws<ArgumentOutOfRangeException>(() => new Vector(-1, 0.0)).ParamName);
        Assert.Equal("size", Assert.Throws<ArgumentOutOfRangeException>(() => new Vector(-1, 0.0, 1.0)).ParamName);
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => v.Resize(-1)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => v.At(-1)).ParamName);
        Assert.Equal("rows", Assert.Throws<ArgumentOutOfRangeException>(() => new Matrix(-1, 2)).ParamName);
        Assert.Equal("columns", Assert.Throws<ArgumentOutOfRangeException>(() => new Matrix(2, -1)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => m.At(-1)).ParamName);
        Assert.Equal("j", Assert.Throws<ArgumentOutOfRangeException>(() => m.Column(-1)).ParamName);
        Assert.Equal("size", Assert.Throws<ArgumentOutOfRangeException>(() => new TridiagonalOperator(-1)).ParamName);
    }
}
