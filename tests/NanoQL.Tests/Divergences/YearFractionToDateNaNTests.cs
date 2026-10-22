// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <c>yearFractionToDate</c> on a NaN time. QuantLib casts <c>round(NaN * 365.25)</c> to an integer through
/// <c>boost::numeric_cast</c>, whose range check a NaN passes (<c>yearfractiontodate.cpp:40-41</c>), so the cast is
/// undefined: the wheel reads -2147439651 days and throws "Date's serial number (-2147439651) outside allowed range".
/// nanoql refuses a NaN on entry with <see cref="ArgumentException"/> naming <c>t</c>.
/// </summary>
public class YearFractionToDateNaNTests
{
    [Fact]
    public void ANaNTimeIsRefusedOnEntry()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => YearFractionToDateFunctions.YearFractionToDate(new Actual365Fixed(), new Date(15, Month.June, 2020), double.NaN));
        Assert.Equal("t", e.ParamName);
        Assert.StartsWith("the time is NaN", e.Message, StringComparison.Ordinal);
    }
}
