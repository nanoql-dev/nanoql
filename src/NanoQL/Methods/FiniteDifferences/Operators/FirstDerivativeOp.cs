// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/firstderivativeop.hpp ql/methods/finitedifferences/operators/firstderivativeop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The first-derivative operator in one direction: central differences on the mesher's non-uniform spacing, upwind
/// at the lower edge and downwind at the upper one.
/// </summary>
/// <remarks>A working type, as its base is.</remarks>
[WorkingType]
public sealed class FirstDerivativeOp : TripleBandLinearOp
{
    /// <summary>Initializes a new instance of the <see cref="FirstDerivativeOp"/> class.</summary>
    /// <param name="direction">The direction.</param>
    /// <param name="mesher">The mesher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FirstDerivativeOp(int direction, FdmMesher mesher)
        : base(direction, mesher)
    {
        FdmLinearOpLayout layout = mesher.Layout;
        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            int i = iter.Index;
            double hm = mesher.Dminus(iter, this.direction);
            double hp = mesher.Dplus(iter, this.direction);

            double zetam1 = hm * (hm + hp);
            double zeta0 = hm * hp;
            double zetap1 = hp * (hm + hp);

            if (iter.Coordinates[this.direction] == 0)
            {
                // upwinding scheme
                lower[i] = 0.0;
                diag[i] = -(upper[i] = 1 / hp);
            }
            else if (iter.Coordinates[this.direction] == layout.Dim[direction] - 1)
            {
                // downwinding scheme
                lower[i] = -(diag[i] = 1 / hm);
                upper[i] = 0.0;
            }
            else
            {
                lower[i] = -hp / zetam1;
                diag[i] = (hp - hm) / zeta0;
                upper[i] = hm / zetap1;
            }
        }
    }
}
