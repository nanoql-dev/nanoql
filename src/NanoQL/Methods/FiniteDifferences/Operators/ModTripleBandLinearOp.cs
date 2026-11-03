// SPDX-FileCopyrightText: Copyright (C) 2013 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/modtriplebandlinearop.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Methods;

/// <summary>A triple-band operator whose bands are read and written point by point.</summary>
/// <remarks>
/// A working type, as <see cref="TripleBandLinearOp"/> is: the forward operators set their boundary rows through it.
/// QuantLib's const and mutable accessor pairs are each one accessor returning a reference.
/// </remarks>
[WorkingType]
public sealed class ModTripleBandLinearOp : TripleBandLinearOp
{
    /// <summary>Initializes a new instance of the <see cref="ModTripleBandLinearOp"/> class, its bands zero.</summary>
    /// <param name="direction">The direction.</param>
    /// <param name="mesher">The mesher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public ModTripleBandLinearOp(int direction, FdmMesher mesher)
        : base(direction, mesher)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModTripleBandLinearOp"/> class as a copy of a triple-band
    /// operator.</summary>
    /// <param name="m">The operator copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="m"/> is <see langword="null"/>.</exception>
    public ModTripleBandLinearOp(TripleBandLinearOp m)
        : base(m)
    {
    }

    /// <summary>The lower band at a point.</summary>
    /// <param name="i">The point's index.</param>
    /// <returns>A reference to the band's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="i"/> isn't a point of the layout, where QuantLib reads past its end.
    /// </exception>
    public ref double Lower(int i)
    {
        if ((uint)i >= (uint)lower.Length)
        {
            ThrowIndex(i, lower.Length, nameof(i));
        }

        return ref lower[i];
    }

    /// <summary>The diagonal at a point.</summary>
    /// <param name="i">The point's index.</param>
    /// <returns>A reference to the band's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="i"/> isn't a point of the layout, where QuantLib reads past its end.
    /// </exception>
    public ref double Diag(int i)
    {
        if ((uint)i >= (uint)diag.Length)
        {
            ThrowIndex(i, diag.Length, nameof(i));
        }

        return ref diag[i];
    }

    /// <summary>The upper band at a point.</summary>
    /// <param name="i">The point's index.</param>
    /// <returns>A reference to the band's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="i"/> isn't a point of the layout, where QuantLib reads past its end.
    /// </exception>
    public ref double Upper(int i)
    {
        if ((uint)i >= (uint)upper.Length)
        {
            ThrowIndex(i, upper.Length, nameof(i));
        }

        return ref upper[i];
    }

    // An index past the array an accessor reads: QuantLib's Array::at text, in a helper, so the accessors
    // stay small enough to inline.
    [DoesNotReturn]
    private static void ThrowIndex(int index, int length, string paramName) =>
        throw new ArgumentOutOfRangeException(
            paramName, $"index ({index}) must be less than {length}: array access out of range");
}
