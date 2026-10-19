// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The option date of a time whose interpolated serial number is NaN, infinite or outside the integers. QuantLib's
/// <c>SwaptionVolatilityDiscrete::optionDateFromTime</c> truncates the serial with
/// <c>static_cast&lt;Date::serial_type&gt;</c> (<c>swaptionvoldiscrete.hpp:121-123</c>), whose result C++ leaves
/// undefined there; MSVC's conversion, the platform the vectors are generated on, gives the smallest integer, which
/// <c>Date</c>'s range check refuses. .NET's conversion saturates instead (NaN to 0, a large value to
/// <see cref="int.MaxValue"/>), so a literal port would refuse other serials, or none: nanoql takes MSVC's serial in those
/// cases, and the refusal is QuantLib's, <c>Date</c>'s <see cref="ArgumentOutOfRangeException"/> and text.
/// </summary>
public class OptionDateFromTimeTests
{
    [Fact]
    public void ATimeWhoseSerialLeavesTheIntegersIsRefusedAsMsvcsSmallestInteger()
    {
        var today = new Date(15, Month.January, 2025);
        var matrix = new SwaptionVolatilityMatrix(
            today,
            new TARGET(),
            BusinessDayConvention.Following,
            [new Period(1, TimeUnit.Years), new Period(2, TimeUnit.Years)],
            [new Period(2, TimeUnit.Years), new Period(5, TimeUnit.Years)],
            new Matrix(2, 2, 0.2),
            new Actual365Fixed());

        foreach (double t in (double[])[double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1.0e9, -1.0e9, 6.0e6])
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => matrix.OptionDateFromTime(t));
            Assert.StartsWith(
                "Date's serial number (-2147483648) outside allowed range [367-109574]", e.Message, StringComparison.Ordinal);
        }

        // A serial inside the integers but outside the range is refused as it is; one in the range is the date.
        Assert.StartsWith(
            "Date's serial number (",
            Assert.Throws<ArgumentOutOfRangeException>(() => matrix.OptionDateFromTime(5.0e6)).Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "-2147483648",
            Assert.Throws<ArgumentOutOfRangeException>(() => matrix.OptionDateFromTime(5.0e6)).Message,
            StringComparison.Ordinal);
        Assert.Equal(today, matrix.OptionDateFromTime(0.0));
    }
}
