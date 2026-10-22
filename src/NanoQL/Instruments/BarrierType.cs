// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/barriertype.hpp ql/instruments/barriertype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The type of a barrier: QuantLib's <c>Barrier::Type</c>.</summary>
/// <remarks>
/// QuantLib's stream operator writes "Down&amp;In", "Up&amp;In", "Down&amp;Out" and "Up&amp;Out", not the members'
/// names, so it is <see cref="BarrierTypeFunctions.ToString(BarrierType)"/>.
/// </remarks>
public enum BarrierType
{
    /// <summary>The option comes into existence when the underlying falls to the barrier.</summary>
    DownIn = 0,

    /// <summary>The option comes into existence when the underlying rises to the barrier.</summary>
    UpIn = 1,

    /// <summary>The option ceases to exist when the underlying falls to the barrier.</summary>
    DownOut = 2,

    /// <summary>The option ceases to exist when the underlying rises to the barrier.</summary>
    UpOut = 3,
}
