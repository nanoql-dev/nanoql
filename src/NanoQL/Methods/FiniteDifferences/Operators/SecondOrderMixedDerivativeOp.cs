// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/secondordermixedderivativeop.hpp ql/methods/finitedifferences/operators/secondordermixedderivativeop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The second-order mixed derivative ∂²/∂x∂y in two directions of a multi-dimensional layout, on a non-uniform grid:
/// the product of the two directions' central first-derivative stencils, one-sided at the grid's edges and corners.
/// </summary>
/// <remarks>A working type (the S-eq plan's SEQR1), its coefficients filled at construction.</remarks>
[WorkingType]
public sealed class SecondOrderMixedDerivativeOp : NinePointLinearOp
{
    /// <summary>Initializes a new instance of the <see cref="SecondOrderMixedDerivativeOp"/> class.</summary>
    /// <param name="d0">The first direction.</param>
    /// <param name="d1">The second direction.</param>
    /// <param name="mesher">The mesher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The directions are equal, or one of them isn't the layout's: QuantLib's "inconsistent derivative directions".
    /// </exception>
    public SecondOrderMixedDerivativeOp(int d0, int d1, FdmMesher mesher)
        : base(d0, d1, mesher)
    {
        FdmLinearOpLayout layout = mesher.Layout;
        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            int i = iter.Index;
            double hm_d0 = mesher.Dminus(iter, this.d0);
            double hp_d0 = mesher.Dplus(iter, this.d0);
            double hm_d1 = mesher.Dminus(iter, this.d1);
            double hp_d1 = mesher.Dplus(iter, this.d1);

            double zetam1 = hm_d0 * (hm_d0 + hp_d0);
            double zeta0 = hm_d0 * hp_d0;
            double zetap1 = hp_d0 * (hm_d0 + hp_d0);
            double phim1 = hm_d1 * (hm_d1 + hp_d1);
            double phi0 = hm_d1 * hp_d1;
            double phip1 = hp_d1 * (hm_d1 + hp_d1);

            int c0 = iter.Coordinates[this.d0];
            int c1 = iter.Coordinates[this.d1];
            int last0 = layout.Dim[this.d0] - 1;
            int last1 = layout.Dim[this.d1] - 1;
            if (c0 == 0 && c1 == 0)
            {
                // lower left corner
                a00[i] = a01[i] = a02[i] = a10[i] = a20[i] = 0.0;
                a21[i] = a12[i] = -(a11[i] = a22[i] = 1.0 / (hp_d0 * hp_d1));
            }
            else if (c0 == last0 && c1 == 0)
            {
                // upper left corner
                a22[i] = a21[i] = a20[i] = a10[i] = a00[i] = 0.0;
                a11[i] = a02[i] = -(a01[i] = a12[i] = 1.0 / (hm_d0 * hp_d1));
            }
            else if (c0 == 0 && c1 == last1)
            {
                // lower right corner
                a00[i] = a01[i] = a02[i] = a12[i] = a22[i] = 0.0;
                a20[i] = a11[i] = -(a10[i] = a21[i] = 1.0 / (hp_d0 * hm_d1));
            }
            else if (c0 == last0 && c1 == last1)
            {
                // upper right corner
                a20[i] = a21[i] = a22[i] = a12[i] = a02[i] = 0.0;
                a10[i] = a01[i] = -(a00[i] = a11[i] = 1.0 / (hm_d0 * hm_d1));
            }
            else if (c0 == 0)
            {
                // lower side
                a00[i] = a01[i] = a02[i] = 0.0;

                a20[i] = -(a10[i] = hp_d1 / (hp_d0 * phim1));
                a11[i] = -(a21[i] = (hp_d1 - hm_d1) / (hp_d0 * phi0));
                a12[i] = -(a22[i] = hm_d1 / (hp_d0 * phip1));
            }
            else if (c0 == last0)
            {
                // upper side
                a20[i] = a21[i] = a22[i] = 0.0;

                a10[i] = -(a00[i] = hp_d1 / (hm_d0 * phim1));
                a01[i] = -(a11[i] = (hp_d1 - hm_d1) / (hm_d0 * phi0));
                a02[i] = -(a12[i] = hm_d1 / (hm_d0 * phip1));
            }
            else if (c1 == 0)
            {
                // left side
                a00[i] = a10[i] = a20[i] = 0.0;

                a02[i] = -(a01[i] = hp_d0 / (zetam1 * hp_d1));
                a11[i] = -(a12[i] = (hp_d0 - hm_d0) / (zeta0 * hp_d1));
                a21[i] = -(a22[i] = hm_d0 / (zetap1 * hp_d1));
            }
            else if (c1 == last1)
            {
                // right side
                a22[i] = a12[i] = a02[i] = 0.0;

                a01[i] = -(a00[i] = hp_d0 / (zetam1 * hm_d1));
                a10[i] = -(a11[i] = (hp_d0 - hm_d0) / (zeta0 * hm_d1));
                a20[i] = -(a21[i] = hm_d0 / (zetap1 * hm_d1));
            }
            else
            {
                a00[i] = hp_d0 * hp_d1 / (zetam1 * phim1);
                a10[i] = -(hp_d0 - hm_d0) * hp_d1 / (zeta0 * phim1);
                a20[i] = -hm_d0 * hp_d1 / (zetap1 * phim1);
                a01[i] = -hp_d0 * (hp_d1 - hm_d1) / (zetam1 * phi0);
                a11[i] = (hp_d0 - hm_d0) * (hp_d1 - hm_d1) / (zeta0 * phi0);
                a21[i] = hm_d0 * (hp_d1 - hm_d1) / (zetap1 * phi0);
                a02[i] = -hp_d0 * hm_d1 / (zetam1 * phip1);
                a12[i] = hm_d1 * (hp_d0 - hm_d0) / (zeta0 * phip1);
                a22[i] = hm_d0 * hm_d1 / (zetap1 * phip1);
            }
        }
    }
}
