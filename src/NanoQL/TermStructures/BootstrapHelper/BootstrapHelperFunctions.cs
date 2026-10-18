// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/bootstraphelper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>The free functions of QuantLib's <c>bootstraphelper.hpp</c>.</summary>
public static class BootstrapHelperFunctions
{
    /// <summary>A pillar choice's text, as QuantLib's stream operator writes it.</summary>
    /// <param name="type">The pillar choice.</param>
    /// <returns>"MaturityPillarDate", "LastRelevantPillarDate" or "CustomPillarDate".</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public static string ToString(PillarChoice type) => type switch
    {
        PillarChoice.MaturityDate => "MaturityPillarDate",
        PillarChoice.LastRelevantDate => "LastRelevantPillarDate",
        PillarChoice.CustomDate => "CustomPillarDate",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"unknown Pillar::Choice({(int)type})"),
    };
}
