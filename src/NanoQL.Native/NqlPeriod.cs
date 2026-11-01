// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using NanoQL.Time;

namespace NanoQL.Native;

/// <summary>C's <c>nql_period</c>: a length and a unit, two <c>int32_t</c>s passed by value.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NqlPeriod
{
    /// <summary>The length.</summary>
    public int Length;

    /// <summary>The unit, a <see cref="TimeUnit"/>.</summary>
    public int Units;

    /// <summary>Initializes a new instance of the <see cref="NqlPeriod"/> struct from a period.</summary>
    /// <param name="p">The period.</param>
    public NqlPeriod(Period p)
    {
        Length = p.Length;
        Units = (int)p.Units;
    }
}
