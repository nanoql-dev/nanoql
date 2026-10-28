// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The histogram's and the discrepancy statistics' conventions (the S-core plan's C3): data giving a bin width of 0
/// under the Freedman-Diaconis or Scott rule refused (CR7 (xxxi)), where QuantLib's bin count is undefined and it throws
/// <c>std::length_error</c>, "vector too long" (<c>histogram.cpp:116</c>, the plan's <c>misc1.cpp</c>); QuantLib's
/// empty histogram (CD15) and checks; a first sample of dimension 1 refused with QuantLib's text, the statistics left
/// as they were (CR7 (xxxvii)); and QuantLib's checks of the Student t distributions.
/// </summary>
public class StatisticsConventionTests
{
    [Fact]
    public void DataOfNoSpreadAreRefusedUnderTheWidthRules()
    {
        double[] flat = [.. Enumerable.Repeat(3.0, 20)];
        ArgumentException e = Assert.Throws<ArgumentException>(() => new Histogram(flat, HistogramAlgorithm.Scott));
        Assert.Equal("data", e.ParamName);
        Assert.Contains("bin width is 0", e.Message, StringComparison.Ordinal);

        // zero interquartile range with a range of 9: QuantLib throws "vector too long" here too
        double[] spike = [.. flat];
        spike[0] = 0.0;
        spike[19] = 9.0;
        ArgumentException fd = Assert.Throws<ArgumentException>(() => new Histogram(spike, HistogramAlgorithm.FD));
        Assert.Equal("data", fd.ParamName);
        Assert.Contains("bin width is 0", fd.Message, StringComparison.Ordinal);

        // Sturges' rule and given bins don't read the spread
        Assert.Equal(6, new Histogram(flat, HistogramAlgorithm.Sturges).Bins);
        Assert.Equal(3, new Histogram(flat, 2).Bins);
    }

    [Fact]
    public void TheDefaultHistogramIsQuantLibsEmptyOne()
    {
        var h = new Histogram();
        Assert.True(h.IsEmpty);
        Assert.Equal(0, h.Bins);
        Assert.Empty(h.Breaks);
        Assert.Equal((HistogramAlgorithm)(-1), h.Algorithm);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => h.Counts(0)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => h.Frequency(0)).ParamName);
    }

    [Fact]
    public void TheHistogramsChecksAreQuantLibs()
    {
        Assert.Equal("no data given", Message(() => new Histogram([], 3)));
        Assert.Equal("a bin-partition algorithm is required", Message(() => new Histogram([1.0, 2.0], HistogramAlgorithm.None)));
        Assert.Equal("unknown bin-partition algorithm", Message(() => new Histogram([1.0, 2.0], (HistogramAlgorithm)7)));
        Assert.Equal("breaks", Assert.Throws<ArgumentOutOfRangeException>(() => new Histogram([1.0, 2.0], -1)).ParamName);
        var given = new Histogram([1.0, 2.0, 3.0], 2);
        Assert.Equal(HistogramAlgorithm.None, given.Algorithm);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => given.Counts(3)).ParamName);
    }

    [Fact]
    public void AFirstSampleOfDimensionOneIsRefusedAndChangesNothing()
    {
        var stat = new DiscrepancyStatistics(0);
        ArgumentException e = Assert.Throws<ArgumentException>(() => stat.Add([0.5]));
        Assert.Equal("sample", e.ParamName);
        Assert.StartsWith("dimension==1 not allowed", e.Message, StringComparison.Ordinal);
        Assert.Equal(0, stat.Size);
        Assert.Equal(0, stat.Samples);

        stat.Add([0.5, 0.25]);
        Assert.Equal(2, stat.Size);
        Assert.Equal(1, stat.Samples);

        Assert.Equal("dimension", Assert.Throws<ArgumentOutOfRangeException>(() => new DiscrepancyStatistics(1)).ParamName);
        Assert.Equal("dimension", Assert.Throws<ArgumentOutOfRangeException>(() => stat.Reset(1)).ParamName);
        stat.Reset();
        Assert.Equal(2, stat.Size);
        Assert.Equal(0, stat.Samples);
    }

    [Fact]
    public void TheStudentDistributionsChecksAreQuantLibs()
    {
        Assert.Equal("invalid parameter for t-distribution", Message(() => new InverseCumulativeStudent(0)));
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => new InverseCumulativeStudent(-3)).ParamName);
        Assert.StartsWith(
            "InverseCumulativeStudent(1.1) undefined: must be 0 < x < 1",
            Assert.Throws<ArgumentOutOfRangeException>(() => new InverseCumulativeStudent(3).Evaluate(1.1)).Message,
            StringComparison.Ordinal);
        Assert.Equal(0.0, new InverseCumulativeStudent(7).Evaluate(0.5));
    }

    private static string Message(Action action)
    {
        Exception e = Assert.ThrowsAny<ArgumentException>(action);
        string message = e.Message;
        int i = message.IndexOf(" (Parameter", StringComparison.Ordinal);
        return i < 0 ? message : message[..i];
    }
}
