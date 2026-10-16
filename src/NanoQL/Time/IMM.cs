// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/imm.hpp ql/time/imm.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: isIMMcode, code, date, nextDate and nextCode, which the wheel exposes and a later slot ports

namespace NanoQL.Time;

/// <summary>The main cycle of the International Money Market (IMM) months.</summary>
public static class IMM
{
    /// <summary>Whether the given date is an IMM date: the third Wednesday of its month.</summary>
    /// <param name="d">The date.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <returns><see langword="true"/> for an IMM date.</returns>
    public static bool IsIMMdate(Date d, bool mainCycle = true)
    {
        if (d.Weekday != Weekday.Wednesday)
        {
            return false;
        }

        int day = d.DayOfMonth;
        if (day < 15 || day > 21)
        {
            return false;
        }

        if (!mainCycle)
        {
            return true;
        }

        return d.Month is Month.March or Month.June or Month.September or Month.December;
    }
}
