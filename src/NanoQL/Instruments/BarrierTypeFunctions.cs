// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/barriertype.hpp ql/instruments/barriertype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The free functions of QuantLib's <c>barriertype.hpp</c>.</summary>
public static class BarrierTypeFunctions
{
    /// <summary>A barrier type, as QuantLib's stream operator writes it.</summary>
    /// <param name="type">The type.</param>
    /// <returns>"Down&amp;In", "Up&amp;In", "Down&amp;Out" or "Up&amp;Out".</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type, with
    /// QuantLib's "unknown Barrier::Type (n)".</exception>
    public static string ToString(BarrierType type) => type switch
    {
        BarrierType.DownIn => "Down&In",
        BarrierType.UpIn => "Up&In",
        BarrierType.DownOut => "Down&Out",
        BarrierType.UpOut => "Up&Out",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"unknown Barrier::Type ({(int)type})"),
    };
}
