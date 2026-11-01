// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using System.Runtime.InteropServices;

namespace NanoQL.Native;

/// <summary>C's <c>nql_complex</c>: a real and an imaginary part.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NqlComplex
{
    /// <summary>The real part.</summary>
    public double Re;

    /// <summary>The imaginary part.</summary>
    public double Im;

    /// <summary>Initializes a new instance of the <see cref="NqlComplex"/> struct from a complex number.</summary>
    /// <param name="c">The number.</param>
    public NqlComplex(Complex c)
    {
        Re = c.Real;
        Im = c.Imaginary;
    }
}
