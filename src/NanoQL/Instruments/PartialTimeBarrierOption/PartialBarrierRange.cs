// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/partialtimebarrieroption.hpp ql/instruments/partialtimebarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>When a partial-time barrier is monitored: QuantLib's <c>PartialBarrier::Range</c>, with its values.</summary>
public enum PartialBarrierRange
{
    /// <summary>From the start of the option's lifetime until the cover event.</summary>
    Start = 0,

    /// <summary>
    /// From the cover event to the exercise date; a knock-out triggers only if the barrier is hit or crossed from
    /// either side, whatever the underlying's value when monitoring starts.
    /// </summary>
    EndB1 = 2,

    /// <summary>
    /// From the cover event to the exercise date; a knock-out triggers at once if the underlying is on the wrong side
    /// of the barrier when monitoring starts.
    /// </summary>
    EndB2 = 3,
}
