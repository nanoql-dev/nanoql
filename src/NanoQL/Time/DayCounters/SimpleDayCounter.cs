// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/simpledaycounter.hpp ql/time/daycounters/simpledaycounter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Simple day counter for reproducing theoretical calculations.</summary>
/// <remarks>
/// This day counter tries to ensure that whole-month distances are returned as a simple fraction, i.e., 1 year = 1.0,
/// 6 months = 0.5, 3 months = 0.25 and so forth. Other distances fall back to 30/360 (Bond Basis). It should be used
/// together with <see cref="NullCalendar"/>, which ensures that dates at whole-month distances share the same day of
/// month; it is not guaranteed to work with any other calendar.
/// </remarks>
public sealed class SimpleDayCounter : DayCounter
{
    private static readonly DayCounter Fallback = new Thirty360(Thirty360Convention.BondBasis);

    /// <inheritdoc/>
    public override string Name => "Simple";

    /// <inheritdoc/>
    public override int DayCount(Date d1, Date d2) => Fallback.DayCount(d1, d2);

    /// <inheritdoc/>
    public override double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null)
    {
        RequireDates(d1, d2, refPeriodStart, refPeriodEnd);
        int dm1 = d1.DayOfMonth, dm2 = d2.DayOfMonth;

        if (dm1 == dm2
            // e.g., Aug 30 -> Feb 28 ?
            || (dm1 > dm2 && Date.IsEndOfMonth(d2))
            // e.g., Feb 28 -> Aug 30 ?
            || (dm1 < dm2 && Date.IsEndOfMonth(d1)))
        {
            return (d2.Year - d1.Year) + (((int)d2.Month - (int)d1.Month) / 12.0);
        }
        else
        {
            return Fallback.YearFraction(d1, d2);
        }
    }
}
