// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/averagetype.hpp ql/instruments/averagetype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The averaging type of an Asian option: QuantLib's <c>Average::Type</c>.</summary>
/// <remarks>
/// QuantLib's stream operator writes the members' names, "Arithmetic" and "Geometric", which the enum's own text gives.
/// </remarks>
public enum AverageType
{
    /// <summary>The arithmetic average.</summary>
    Arithmetic = 0,

    /// <summary>The geometric average.</summary>
    Geometric = 1,
}
