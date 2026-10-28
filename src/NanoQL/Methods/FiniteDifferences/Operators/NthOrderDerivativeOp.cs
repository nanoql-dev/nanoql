// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/nthorderderivativeop.hpp ql/methods/finitedifferences/operators/nthorderderivativeop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The n-th order derivative in one direction as a sparse linear operator: at each point, Fornberg's weights on the
/// given number of the direction's grid points around it, shifted inward at the grid's edges.
/// </summary>
/// <remarks>
/// A working type of the finite-difference framework: <see cref="Apply(ReadOnlySpan{double}, Span{double})"/> writes
/// into the caller's span, and <see cref="ToMatrix"/> returns a copy of the operator's matrix. The direction's
/// locations are deduplicated and sorted, as QuantLib's set of them, so the weights' offsets are distinct.
/// </remarks>
public sealed class NthOrderDerivativeOp : FdmLinearOp
{
    private readonly SparseMatrix m;

    /// <summary>Initializes a new instance of the <see cref="NthOrderDerivativeOp"/> class.</summary>
    /// <param name="direction">The direction of the derivative.</param>
    /// <param name="order">The derivative's order, below <paramref name="nPoints"/>.</param>
    /// <param name="nPoints">The number of points per derivative, from 2 to the direction's size.</param>
    /// <param name="mesher">The mesher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="direction"/> isn't one of the mesher's, or <paramref name="order"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The direction's locations repeat, the number of points is inconsistent, or it isn't above the order.
    /// </exception>
    public NthOrderDerivativeOp(int direction, int order, int nPoints, FdmMesher mesher)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        FdmLinearOpLayout layout = mesher.Layout;
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, layout.Dim.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        m = new SparseMatrix(layout.Size, layout.Size);

        int hPoints = nPoints / 2;
        bool isEven = nPoints == 2 * hPoints;

        // unique vector
        double[] xValues = [.. new SortedSet<double>(mesher.Locations(direction).AsSpan().ToArray())];

        int nx = layout.Dim[direction];

        if (!(xValues.Length == nx))
        {
            throw new ArgumentException($"inconsistent set of grid values in direction {direction}", nameof(mesher));
        }

        if (!(nPoints > 1 && nPoints <= nx))
        {
            throw new ArgumentException("inconsistent number of points", nameof(nPoints));
        }

        var xOffsets = new double[nPoints];

        FdmLinearOpIterator iter = layout.Begin();
        for (int count = 0; count < layout.Size; ++count, iter.Increment())
        {
            int ix = iter.Coordinates[direction];
            int offset = Math.Max(0, hPoints - ix) - Math.Max(0, hPoints - (nx - (isEven ? 0 : 1) - ix));

            int ilx = ix - hPoints + offset;

            for (int j = 0; j < nPoints; ++j)
            {
                int idx = ilx + j;
                xOffsets[j] = xValues[idx] - xValues[ix];
            }

            ImmutableArray<double> weights = NumericalDifferentiation.CalcWeights(xOffsets, order);

            int i = iter.Index;
            for (int j = 0; j < nPoints; ++j)
            {
                int k = layout.Neighbourhood(iter, direction, ilx - ix + j);

                m[i, k] = weights[j];
            }
        }
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        if (!(r.Length == m.Size2 && result.Length == m.Size1))
        {
            throw new ArgumentException(
                $"vectors and sparse matrices with different sizes ({r.Length}, {m.Size1}x{m.Size2}) cannot be multiplied",
                nameof(r));
        }

        ReadOnlySpan<int> index1 = m.Index1Data, index2 = m.Index2Data;
        ReadOnlySpan<double> values = m.ValueData;
        result.Clear();
        for (int i = 0; i < m.Filled1 - 1; ++i)
        {
            int begin = index1[i];
            int end = index1[i + 1];
            double t = 0;
            for (int j = begin; j < end; ++j)
            {
                t += values[j] * r[index2[j]];
            }

            result[i] = t;
        }
    }

    /// <inheritdoc/>
    public override SparseMatrix ToMatrix() => m.Clone();
}
