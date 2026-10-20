// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The default-probability term structures' refusals, with QuantLib's texts (<c>defaulttermstructure.cpp</c>): jumps
/// and jump dates of different counts are an <see cref="ArgumentException"/> naming the dates, "mismatch between number
/// of jumps (2) and jump dates (1)"; a jump outside (0, 1] is a <see cref="NanoQLException"/> when a probability reads
/// it, "invalid 1st jump value: 1.5"; and a default probability between two dates or times in the wrong order is an
/// <see cref="ArgumentException"/> naming the first, "initial date (…) later than final date (…)" or "initial time (…)
/// later than final time (…)".
/// </summary>
public class DefaultProbabilityRefusalTests
{
    private static readonly Date Today = new(15, Month.June, 2024);

    [Fact]
    public void JumpsAndTheirDatesMustMatch()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new InterpolatedHazardRateCurve<BackwardFlat>(
                [Today, Today + 3650], [0.02, 0.02], new Actual365Fixed(), null, [0.99, 0.98], [Today + 180]));
        Assert.Equal("jumpDates", e.ParamName);
        Assert.StartsWith("mismatch between number of jumps (2) and jump dates (1)", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AJumpOutsideTheUnitIntervalIsRefusedWhenRead()
    {
        var curve = new InterpolatedHazardRateCurve<BackwardFlat>(
            [Today, Today + 3650], [0.02, 0.02], new Actual365Fixed(), null, [1.5], [Today + 180]);

        // before the jump it isn't read
        Assert.Equal(Math.Exp(-0.02 * 0.25), curve.SurvivalProbability(0.25), 15);
        Assert.Equal(
            "invalid 1st jump value: 1.5",
            Assert.Throws<NanoQLException>(() => curve.SurvivalProbability(1.0)).Message);
    }

    [Fact]
    public void DatesAndTimesOutOfOrderAreRefused()
    {
        var curve = new FlatHazardRate(Today, 0.02, new Actual365Fixed());
        Date d1 = Today + 365, d2 = Today + 180;

        ArgumentException e = Assert.Throws<ArgumentException>(() => curve.DefaultProbability(d1, d2));
        Assert.Equal("d1", e.ParamName);
        Assert.StartsWith($"initial date ({d1}) later than final date ({d2})", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => curve.DefaultProbability(2.0, 1.0));
        Assert.Equal("t1", e.ParamName);
        Assert.StartsWith("initial time (2) later than final time (1)", e.Message, StringComparison.Ordinal);
    }
}
