// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/doublebarriertype.hpp ql/instruments/doublebarriertype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The type of a double barrier: QuantLib's <c>DoubleBarrier::Type</c>.</summary>
/// <remarks>
/// QuantLib's stream operator writes "KnockIn", "KnockOut", "KI lo+KO up" and "KO lo+KI up", the last two not the
/// members' names, so it is <see cref="DoubleBarrierTypeFunctions.ToString(DoubleBarrierType)"/>.
/// </remarks>
public enum DoubleBarrierType
{
    /// <summary>The option comes into existence when the underlying reaches either barrier.</summary>
    KnockIn = 0,

    /// <summary>The option ceases to exist when the underlying reaches either barrier.</summary>
    KnockOut = 1,

    /// <summary>The lower barrier knocks in, the upper one knocks out.</summary>
    KIKO = 2,

    /// <summary>The lower barrier knocks out, the upper one knocks in.</summary>
    KOKI = 3,
}
