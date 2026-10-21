// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/secondderivativeop.hpp ql/methods/finitedifferences/operators/secondderivativeop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The second-derivative operator in one direction: central differences on the mesher's non-uniform spacing, zero at
/// the edges.
/// </summary>
/// <remarks>A working type, as its base is (the S-eq plan's SEQR1).</remarks>
public sealed class SecondDerivativeOp : TripleBandLinearOp
{
    /// <summary>Initializes a new instance of the <see cref="SecondDerivativeOp"/> class.</summary>
    /// <param name="direction">The direction.</param>
    /// <param name="mesher">The mesher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public SecondDerivativeOp(int direction, FdmMesher mesher)
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

            int co = iter.Coordinates[this.direction];
            if (co == 0 || co == layout.Dim[direction] - 1)
            {
                lower[i] = diag[i] = upper[i] = 0.0;
            }
            else
            {
                lower[i] = 2.0 / zetam1;
                diag[i] = -2.0 / zeta0;
                upper[i] = 2.0 / zetap1;
            }
        }
    }
}
