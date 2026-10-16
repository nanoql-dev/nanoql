// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/timeunit.hpp ql/time/timeunit.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Units used to describe time periods.</summary>
public enum TimeUnit
{
    /// <summary>Days.</summary>
    Days,

    /// <summary>Weeks.</summary>
    Weeks,

    /// <summary>Months.</summary>
    Months,

    /// <summary>Years.</summary>
    Years,

    /// <summary>Hours.</summary>
    Hours,

    /// <summary>Minutes.</summary>
    Minutes,

    /// <summary>Seconds.</summary>
    Seconds,

    /// <summary>Milliseconds.</summary>
    Milliseconds,

    /// <summary>Microseconds.</summary>
    Microseconds,
}
