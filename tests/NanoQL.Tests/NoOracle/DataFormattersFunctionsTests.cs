// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Utilities;

namespace NanoQL.Tests.NoOracle;

/// <summary>
/// The data formatters no QuantLib test or wheel binding checks. Expected values are derived from QuantLib v1.44's
/// <c>ql/utilities/dataformatters.hpp</c> and <c>.cpp</c>:
/// <list type="bullet">
/// <item><c>power_of_two</c> (hpp:158-172): "null" for the null value; otherwise the odd part and the power of two,
/// "n*2^p", shifting right while the low bit is clear, with zero left as "0*2^0".</item>
/// <item><c>checknull</c> (hpp:149-155): "null" for the null value, otherwise the value as the stream writes it
/// (default precision 6, "%g" style).</item>
/// <item><c>sequence</c> (hpp:175-182): "( ", each element followed by a space, then ")".</item>
/// <item><c>volatility</c> (hpp:133-135), like <c>rate</c>, writes <c>percent</c> (cpp:42-55): the value times 100 in
/// fixed notation with six decimals, then " %"; "null" for the null value.</item>
/// <item><c>ordinal</c> (cpp:25-40): "th" for exactly 11, 12 and 13, otherwise by the last digit, so 111 gives "111st".</item>
/// </list>
/// </summary>
public class DataFormattersFunctionsTests
{
    [Theory]
    [InlineData(12, "3*2^2")]
    [InlineData(1, "1*2^0")]
    [InlineData(0, "0*2^0")]
    [InlineData(7, "7*2^0")]
    [InlineData(1024, "1*2^10")]
    [InlineData(-4, "-1*2^2")]
    public void PowerOfTwoSplitsTheOddPart(int n, string expected)
    {
        Assert.Equal(expected, DataFormattersFunctions.PowerOfTwo<int>(n));
    }

    [Fact]
    public void PowerOfTwoOfNullAndOfLong()
    {
        Assert.Equal("null", DataFormattersFunctions.PowerOfTwo<int>(null));
        Assert.Equal("3*2^40", DataFormattersFunctions.PowerOfTwo<long>(3L << 40));
    }

    [Fact]
    public void CheckNullWritesNullOrTheStreamText()
    {
        Assert.Equal("null", DataFormattersFunctions.CheckNull<double>(null));
        Assert.Equal("null", DataFormattersFunctions.CheckNull<int>(null));
        Assert.Equal("42", DataFormattersFunctions.CheckNull<int>(42));
        Assert.Equal("0.1", DataFormattersFunctions.CheckNull<double>(0.1));
        Assert.Equal("1e-07", DataFormattersFunctions.CheckNull<double>(1e-7));
        Assert.Equal("123457", DataFormattersFunctions.CheckNull<double>(123456.7));
        Assert.Equal("1.23457e+06", DataFormattersFunctions.CheckNull<double>(1234567.0));
        Assert.Equal("0.0001", DataFormattersFunctions.CheckNull<double>(0.0001));
        Assert.Equal("-2.5", DataFormattersFunctions.CheckNull<double>(-2.5));
        Assert.Equal("3", DataFormattersFunctions.CheckNull<double>(3.0));
    }

    [Fact]
    public void SequenceWritesSpaceSeparatedValuesInParentheses()
    {
        Assert.Equal("( )", DataFormattersFunctions.Sequence(Array.Empty<double>()));
        Assert.Equal("( 1 2.5 3 )", DataFormattersFunctions.Sequence(new[] { 1.0, 2.5, 3.0 }));
        Assert.Equal("( 1 2 3 )", DataFormattersFunctions.Sequence(new[] { 1, 2, 3 }));
        Assert.Equal("( 1e-07 0.333333 )", DataFormattersFunctions.Sequence(new[] { 1e-7, 1.0 / 3.0 }));
    }

    [Fact]
    public void VolatilityWritesAPercentageLikeRate()
    {
        Assert.Equal("20.000000 %", DataFormattersFunctions.Volatility(0.2));
        Assert.Equal(DataFormattersFunctions.Rate(0.0325), DataFormattersFunctions.Volatility(0.0325));
        Assert.Equal("3.250000 %", DataFormattersFunctions.Volatility(0.0325));
        Assert.Equal("null", DataFormattersFunctions.Volatility(null));
    }

    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(111, "111st")]
    [InlineData(112, "112nd")]
    public void OrdinalSpecialCasesOnlyElevenToThirteen(int n, string expected)
    {
        Assert.Equal(expected, DataFormattersFunctions.Ordinal(n));
    }
}
