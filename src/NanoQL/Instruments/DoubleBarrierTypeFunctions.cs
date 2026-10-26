// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/doublebarriertype.hpp ql/instruments/doublebarriertype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The free functions of QuantLib's <c>doublebarriertype.hpp</c>.</summary>
public static class DoubleBarrierTypeFunctions
{
    /// <summary>A double barrier type, as QuantLib's stream operator writes it.</summary>
    /// <param name="type">The type.</param>
    /// <returns>"KnockIn", "KnockOut", "KI lo+KO up" or "KO lo+KI up".</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type, with
    /// QuantLib's "unknown DoubleBarrier::Type (n)".</exception>
    public static string ToString(DoubleBarrierType type) => type switch
    {
        DoubleBarrierType.KnockIn => "KnockIn",
        DoubleBarrierType.KnockOut => "KnockOut",
        DoubleBarrierType.KIKO => "KI lo+KO up",
        DoubleBarrierType.KOKI => "KO lo+KI up",
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, $"unknown DoubleBarrier::Type ({(int)type})"),
    };
}
