// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The prepayment models' refusals (QLNet's divergence, <c>Divergences/QLNet/PrepayRateRangeTests.cs</c>): each
/// exception's type, message and <c>ParamName</c>.
/// </summary>
public class PrepayModelErrorTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(-1e-300)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AConstantCprOutsideZeroToOneIsRefused(double cpr)
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => new ConstantCPR(cpr));

        Assert.Equal("cpr", e.ParamName);
        Assert.StartsWith("a conditional prepayment rate is a fraction in [0, 1]", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(-1e-300)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ANegativeOrNonFinitePsaMultiplierIsRefused(double multiplier)
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => new PSACurve(new Date(1, March, 2007), multiplier));

        Assert.Equal("multiplier", e.ParamName);
        Assert.StartsWith(
            "a PSA speed's multiplier is a finite number, 0 or more", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRangesEndsAreTaken()
    {
        Assert.Equal(0.0, new ConstantCPR(0.0).GetCPR(new Date(1, March, 2007)));
        Assert.Equal(1.0, new ConstantCPR(1.0).GetCPR(new Date(1, March, 2007)));
        Assert.Equal(0.0, new PSACurve(new Date(1, March, 2007), 0.0).GetCPR(new Date(1, March, 2007)));
        Assert.Equal(1.0, new PSACurve(new Date(1, March, 2007), double.MaxValue).GetCPR(new Date(1, March, 2007)));
    }
}
