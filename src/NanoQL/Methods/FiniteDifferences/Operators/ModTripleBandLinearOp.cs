// SPDX-FileCopyrightText: Copyright (C) 2013 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/modtriplebandlinearop.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>A triple-band operator whose bands are read and written point by point.</summary>
/// <remarks>
/// A working type, as <see cref="TripleBandLinearOp"/> is (the S-hes plan's HD6, HD8): the forward operators set their
/// boundary rows through it. QuantLib's const and mutable accessor pairs are each one accessor returning a reference.
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
    /// <exception cref="IndexOutOfRangeException"><paramref name="i"/> isn't a point of the layout.</exception>
    public ref double Lower(int i) => ref lower[i];

    /// <summary>The diagonal at a point.</summary>
    /// <param name="i">The point's index.</param>
    /// <returns>A reference to the band's value.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="i"/> isn't a point of the layout.</exception>
    public ref double Diag(int i) => ref diag[i];

    /// <summary>The upper band at a point.</summary>
    /// <param name="i">The point's index.</param>
    /// <returns>A reference to the band's value.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="i"/> isn't a point of the layout.</exception>
    public ref double Upper(int i) => ref upper[i];
}
