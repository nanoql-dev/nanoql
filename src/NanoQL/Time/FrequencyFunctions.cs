// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/frequency.hpp ql/time/frequency.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>QuantLib's free functions for frequencies (<c>frequency.hpp</c>).</summary>
public static class FrequencyFunctions
{
    /// <summary>A frequency's text, as QuantLib writes it; it differs from the member name for four members.</summary>
    /// <param name="f">The frequency.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a frequency.</exception>
    public static string ToString(Frequency f) => f switch
    {
        Frequency.NoFrequency => "No-Frequency",
        Frequency.Once => "Once",
        Frequency.Annual => "Annual",
        Frequency.Semiannual => "Semiannual",
        Frequency.EveryFourthMonth => "Every-Fourth-Month",
        Frequency.Quarterly => "Quarterly",
        Frequency.Bimonthly => "Bimonthly",
        Frequency.Monthly => "Monthly",
        Frequency.EveryFourthWeek => "Every-fourth-week",
        Frequency.Biweekly => "Biweekly",
        Frequency.Weekly => "Weekly",
        Frequency.Daily => "Daily",
        Frequency.OtherFrequency => "Unknown frequency",
        _ => throw new ArgumentOutOfRangeException(nameof(f), f, $"unknown frequency ({(int)f})"),
    };
}
