// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/frequency.hpp ql/time/frequency.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Frequency of events.</summary>
public enum Frequency
{
    /// <summary>Null frequency.</summary>
    NoFrequency = -1,

    /// <summary>Only once, e.g., a zero-coupon.</summary>
    Once = 0,

    /// <summary>Once a year.</summary>
    Annual = 1,

    /// <summary>Twice a year.</summary>
    Semiannual = 2,

    /// <summary>Every fourth month.</summary>
    EveryFourthMonth = 3,

    /// <summary>Every third month.</summary>
    Quarterly = 4,

    /// <summary>Every second month.</summary>
    Bimonthly = 6,

    /// <summary>Once a month.</summary>
    Monthly = 12,

    /// <summary>Every fourth week.</summary>
    EveryFourthWeek = 13,

    /// <summary>Every second week.</summary>
    Biweekly = 26,

    /// <summary>Once a week.</summary>
    Weekly = 52,

    /// <summary>Once a day.</summary>
    Daily = 365,

    /// <summary>Some other unknown frequency.</summary>
    OtherFrequency = 999,
}
