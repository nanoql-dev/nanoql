// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2020 Leonardo Arcari
// SPDX-FileCopyrightText: Copyright (C) 2020 Kline s.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/date.hpp ql/time/date.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Month names, with QuantLib's values and three-letter aliases.</summary>
/// <remarks>
/// The aliases share their month's value, so <see cref="Enum.ToString()"/> may return either name; use
/// <see cref="DateFunctions.ToString(Month)"/> for QuantLib's text.
/// </remarks>
public enum Month
{
    /// <summary>January.</summary>
    January = 1,

    /// <summary>February.</summary>
    February = 2,

    /// <summary>March.</summary>
    March = 3,

    /// <summary>April.</summary>
    April = 4,

    /// <summary>May.</summary>
    May = 5,

    /// <summary>June.</summary>
    June = 6,

    /// <summary>July.</summary>
    July = 7,

    /// <summary>August.</summary>
    August = 8,

    /// <summary>September.</summary>
    September = 9,

    /// <summary>October.</summary>
    October = 10,

    /// <summary>November.</summary>
    November = 11,

    /// <summary>December.</summary>
    December = 12,

    /// <summary>January.</summary>
    Jan = 1,

    /// <summary>February.</summary>
    Feb = 2,

    /// <summary>March.</summary>
    Mar = 3,

    /// <summary>April.</summary>
    Apr = 4,

    /// <summary>June.</summary>
    Jun = 6,

    /// <summary>July.</summary>
    Jul = 7,

    /// <summary>August.</summary>
    Aug = 8,

    /// <summary>September.</summary>
    Sep = 9,

    /// <summary>October.</summary>
    Oct = 10,

    /// <summary>November.</summary>
    Nov = 11,

    /// <summary>December.</summary>
    Dec = 12,
}
