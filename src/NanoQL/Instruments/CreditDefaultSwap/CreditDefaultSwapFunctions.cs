// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/creditdefaultswap.hpp ql/instruments/creditdefaultswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>QuantLib's free functions on credit default swaps.</summary>
public static class CreditDefaultSwapFunctions
{
    /// <summary>The maturity of a standard credit default swap, from its trade date, its tenor and its date-generation
    /// rule.</summary>
    /// <param name="tradeDate">The trade date.</param>
    /// <param name="tenor">The tenor: years, or a multiple of 3 months, zero included.</param>
    /// <param name="rule">The rule: <see cref="DateGenerationRule.CDS2015"/>, <see cref="DateGenerationRule.CDS"/> or
    /// <see cref="DateGenerationRule.OldCDS"/>.</param>
    /// <returns>The maturity, or <see langword="null"/>, QuantLib's <c>Date()</c>, when a zero tenor under the 2015
    /// rule gives no valid maturity: a trade date whose previous roll date is 20 June or 20 December.</returns>
    /// <exception cref="ArgumentException"><paramref name="tradeDate"/> is <c>default(Date)</c>; the rule isn't a CDS
    /// rule; the tenor isn't years or a multiple of 3 months, or is 0 months under the old rule.</exception>
    /// <exception cref="NanoQLException">The maturity isn't after the trade date.</exception>
    public static Date? CdsMaturity(Date tradeDate, Period tenor, DateGenerationRule rule)
    {
        Date.ThrowIfDefault(tradeDate, nameof(tradeDate));
        if (!(rule == DateGenerationRule.CDS2015 || rule == DateGenerationRule.CDS || rule == DateGenerationRule.OldCDS))
        {
            throw new ArgumentException(
                "cdsMaturity should only be used with date generation rule CDS2015, CDS or OldCDS", nameof(rule));
        }

        if (!(tenor.Units == TimeUnit.Years || (tenor.Units == TimeUnit.Months && tenor.Length % 3 == 0)))
        {
            throw new ArgumentException("cdsMaturity expects a tenor that is a multiple of 3 months.", nameof(tenor));
        }

        if (rule == DateGenerationRule.OldCDS)
        {
            // QuantLib's tenor != 0 * Months, under its Period comparison, for which every zero-length period is equal
            if (tenor.Length == 0)
            {
                throw new ArgumentException("A tenor of 0M is not supported for OldCDS.", nameof(tenor));
            }
        }

        Date anchorDate = ScheduleFunctions.PreviousTwentieth(tradeDate, rule);
        if (rule == DateGenerationRule.CDS2015
            && (anchorDate == new Date(20, Month.December, anchorDate.Year)
                || anchorDate == new Date(20, Month.June, anchorDate.Year)))
        {
            if (tenor.Length == 0)
            {
                return null;
            }
            else
            {
                anchorDate -= new Period(3, TimeUnit.Months);
            }
        }

        Date maturity = anchorDate + tenor + new Period(3, TimeUnit.Months);
        if (!(maturity > tradeDate))
        {
            throw new NanoQLException(
                $"error calculating CDS maturity. Tenor is {tenor}, trade date is {Iso(tradeDate)} generating a maturity "
                + $"of {Iso(maturity)} <= trade date.");
        }

        return maturity;
    }

    private static string Iso(Date d) => d.ToDateOnly().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
