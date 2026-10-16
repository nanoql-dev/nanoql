// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2008, 2010, 2011, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009, 2012 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/schedule.hpp ql/time/schedule.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>QuantLib's free functions for schedules (<c>schedule.hpp</c>).</summary>
public static class ScheduleFunctions
{
    /// <summary>
    /// The date on or before the given date that is the 20th of its month and, where the rule needs it, of a main IMM
    /// month.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <param name="rule">The date-generation rule.</param>
    /// <returns>The 20th on or before <paramref name="d"/>.</returns>
    public static Date PreviousTwentieth(Date d, DateGenerationRule rule)
    {
        var result = new Date(20, d.Month, d.Year);
        if (result > d)
        {
            result -= new Period(1, TimeUnit.Months);
        }

        if (rule is DateGenerationRule.TwentiethIMM or DateGenerationRule.OldCDS or DateGenerationRule.CDS
            or DateGenerationRule.CDS2015)
        {
            int m = (int)result.Month;
            if (m % 3 != 0)
            {
                // not a main IMM month
                int skip = m % 3;
                result -= new Period(skip, TimeUnit.Months);
            }
        }

        return result;
    }

    /// <summary>Whether the tenor allows the end-of-month convention: a non-zero tenor in months or years.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns><see langword="true"/> for at least one month, in months or years.</returns>
    public static bool AllowsEndOfMonth(Period tenor) =>
        (tenor.Units == TimeUnit.Months || tenor.Units == TimeUnit.Years) && tenor >= new Period(1, TimeUnit.Months);
}
