// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A French amortization's notionals for a bond length that isn't positive, or at a frequency of no period.
/// QuantLib's <c>sinkingNotionals</c> finds no periods in a zero length, or minus one in a length of minus one period,
/// then writes past the end of a one-element vector or reads the first element of an empty one
/// (<c>amortizingfixedratebond.cpp:131-147</c>), which is undefined: the wheel ends the process for 0Y, 0M and -1Y; below
/// minus one period the vector's size wraps, and it throws "vector too long". At <see cref="Frequency.Once"/> or
/// <see cref="Frequency.NoFrequency"/> the period has no days, and the time ratio divides by zero and converts the
/// infinite ratio to an integer (<c>amortizingfixedratebond.cpp:99-104</c>), which is undefined: MSVC's smallest integer
/// matches no positive length, so the wheel refuses "Bond frequency is incompatible with the maturity tenor". nanoql
/// refuses a length that isn't positive, before its periods are counted, and a frequency of no period, with that text,
/// each with <see cref="ArgumentException"/> naming its parameter.
/// </summary>
public class SinkingNotionalsTests
{
    [Fact]
    public void ABondLengthThatIsNotPositiveIsRefused()
    {
        foreach ((Period length, Frequency frequency) in new[]
        {
            (new Period(0, TimeUnit.Years), Frequency.Annual),
            (new Period(0, TimeUnit.Months), Frequency.Monthly),
            (new Period(-1, TimeUnit.Years), Frequency.Annual),
            (new Period(-5, TimeUnit.Years), Frequency.Annual),
        })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => AmortizingFixedRateBondFunctions.SinkingNotionals(length, frequency, 0.05, 100.0));
            Assert.Equal("bondLength", e.ParamName);
        }

        // One period stays QuantLib's: the initial notional, then 0.
        Assert.Equal(
            [100.0, 0.0],
            AmortizingFixedRateBondFunctions.SinkingNotionals(new Period(1, TimeUnit.Years), Frequency.Annual, 0.05, 100.0));
    }

    [Fact]
    public void AFrequencyOfNoPeriodIsRefused()
    {
        foreach (Frequency frequency in new[] { Frequency.Once, Frequency.NoFrequency })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => AmortizingFixedRateBondFunctions.SinkingNotionals(
                    new Period(5, TimeUnit.Years), frequency, 0.05, 100.0));
            Assert.Equal("frequency", e.ParamName);
            Assert.StartsWith("Bond frequency is incompatible with the maturity tenor", e.Message, StringComparison.Ordinal);
        }
    }
}
