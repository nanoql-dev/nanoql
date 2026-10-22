// SPDX-FileCopyrightText: Copyright (C) 2015 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/thirty365.hpp ql/time/daycounters/thirty365.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>30/365 day count convention.</summary>
public sealed class Thirty365 : DayCounter
{
    /// <inheritdoc/>
    public override string Name => "30/365";

    /// <inheritdoc/>
    public override int DayCount(Date d1, Date d2)
    {
        int dd1 = d1.DayOfMonth, dd2 = d2.DayOfMonth;
        int mm1 = (int)d1.Month, mm2 = (int)d2.Month;
        int yy1 = d1.Year, yy2 = d2.Year;

        // date adjustment rules as in ISO 20022
        // see https://www.iso20022.org/15022/uhb/mt565-16-field-22f.htm
        if (dd1 == 31)
        {
            dd1 = 30;
        }

        if (dd2 == 31)
        {
            dd2 = 30;
        }

        return (360 * (yy2 - yy1)) + (30 * (mm2 - mm1)) + (dd2 - dd1);
    }

    /// <inheritdoc/>
    public override double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null)
    {
        RequireDates(d1, d2, refPeriodStart, refPeriodEnd);
        return DayCount(d1, d2) / 365.0;
    }
}
