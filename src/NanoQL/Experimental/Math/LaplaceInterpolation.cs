// SPDX-FileCopyrightText: Copyright (C) 2015, 2024 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/laplaceinterpolation.hpp ql/experimental/math/laplaceinterpolation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The reconstruction of missing values on a grid by Laplace interpolation, in any number of dimensions n ≥ 1 and on
/// non-equidistant grids: QuantLib's <c>LaplaceInterpolation</c>.
/// </summary>
/// <remarks>
/// <para>
/// For n = 1 the method is linear interpolation with flat extrapolation. A missing value at a corner of the grid is
/// the weighted mean of its neighbours along each direction, which generalizes Numerical Recipes' eq. 3.8.6: on
/// linear data the corner reads 2.5 where the line says 1, the book's rule. Reference: Numerical Recipes, 3rd
/// edition, ch. 3.8.
/// </para>
/// <para>
/// Solved at construction: the linear system by BiCGstab, once. A coordinate whose grid has a single point is left
/// out of the system; with every coordinate left out, the value is the given one, or 0 where it is missing.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class LaplaceInterpolation
{
    private readonly Func<IReadOnlyList<int>, double?> y;
    private readonly int dimensions;
    private readonly ImmutableArray<bool> coordinateIncluded;
    private readonly int numberOfCoordinatesIncluded;
    private readonly FdmLinearOpLayout? layout;
    private readonly Vector interpolatedValues = new();

    /// <summary>Initializes a new instance of the <see cref="LaplaceInterpolation"/> class, solving its system.</summary>
    /// <param name="y">
    /// The value at each grid point, by its coordinates; <see langword="null"/> where it is missing, QuantLib's
    /// <c>Null&lt;Real&gt;()</c>.
    /// </param>
    /// <param name="x">The grid of each coordinate.</param>
    /// <param name="relTol">BiCGstab's relative tolerance.</param>
    /// <param name="maxIterMultiplier">BiCGstab's iterations, per grid point.</param>
    /// <exception cref="NanoQLException">BiCGstab fails to converge, with its text.</exception>
    public LaplaceInterpolation(
        Func<IReadOnlyList<int>, double?> y,
        IEnumerable<IEnumerable<double>> x,
        double relTol = 1E-6,
        int maxIterMultiplier = 10)
    {
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(x);
        this.y = y;
        ImmutableArray<double>[] grids = [.. x.Select(g => g.ToImmutableArray())];
        dimensions = grids.Length;

        // set up the mesher
        var dim = new List<int>();
        var included = new bool[grids.Length];
        for (int i = 0; i < grids.Length; ++i)
        {
            included[i] = grids[i].Length > 1;
            if (included[i])
            {
                dim.Add(grids[i].Length);
            }
        }

        coordinateIncluded = [.. included];
        numberOfCoordinatesIncluded = dim.Count;

        if (numberOfCoordinatesIncluded == 0)
        {
            return;
        }

        layout = new FdmLinearOpLayout(dim);

        var meshers = new List<Fdm1dMesher>();
        foreach (ImmutableArray<double> grid in grids)
        {
            if (grid.Length > 1)
            {
                meshers.Add(new Predefined1dMesher(grid));
            }
        }

        var mesher = new FdmMesherComposite(layout, meshers);

        // set up the Laplace operator, the sum of the second derivatives, and convert it to a matrix
        SparseMatrix? op = null;
        for (int direction = 0; direction < mesher.Layout.Dim.Length; ++direction)
        {
            if (mesher.Layout.Dim[direction] > 1)
            {
                SparseMatrix part = new SecondDerivativeOp(direction, mesher).ToMatrix();
                op = op is null ? part.Clone() : op + part;
            }
        }

        // set up the linear system to solve
        int N = layout.Size;
        var g = new SparseMatrix(N, N, 5 * N);
        Vector rhs = new(N, 0.0), guess = new(N, 0.0);
        double guessTmp = 0.0;

        var cornerH = new double[dim.Count];
        var cornerNeighbourIndex = new int[dim.Count];
        FdmLinearOpIterator pos = layout.Begin();
        for (int count = 0; count < N; ++count, pos.Increment())
        {
            int[] coord = pos.Coordinates.ToArray();
            double? val = y(numberOfCoordinatesIncluded == dimensions ? coord : FullCoordinates(coord));
            if (val is null)
            {
                bool isCorner = true;
                for (int d = 0; d < dim.Count && isCorner; ++d)
                {
                    if (coord[d] == 0)
                    {
                        cornerH[d] = meshers[d].Dplus(0);
                        cornerNeighbourIndex[d] = 1;
                    }
                    else if (coord[d] == layout.Dim[d] - 1)
                    {
                        cornerH[d] = meshers[d].Dminus(dim[d] - 1);
                        cornerNeighbourIndex[d] = dim[d] - 2;
                    }
                    else
                    {
                        isCorner = false;
                    }
                }

                if (isCorner)
                {
                    // handling of the "corners", all second derivatives are zero in the op; this directly
                    // generalizes Numerical Recipes, 3rd ed, eq 3.8.6
                    double sumCornerH = 0.0;
                    foreach (double h in cornerH)
                    {
                        sumCornerH += h;
                    }

                    for (int j = 0; j < dim.Count; ++j)
                    {
                        int[] coordJ = (int[])coord.Clone();
                        coordJ[j] = cornerNeighbourIndex[j];
                        double weight = 0.0;
                        for (int i = 0; i < dim.Count; ++i)
                        {
                            if (i != j)
                            {
                                weight += cornerH[i];
                            }
                        }

                        weight = dim.Count == 1 ? 1.0 : weight / sumCornerH;
                        g[count, layout.Index(coordJ)] = -weight;
                    }

                    g[count, count] = 1.0;
                }
                else
                {
                    // a point with at least one dimension with a non-trivial second derivative: the operator's row
                    CopyRow(op!, count, g);
                }

                rhs[count] = 0.0;
                guess[count] = guessTmp;
            }
            else
            {
                g[count, count] = 1;
                rhs[count] = val.Value;
                guess[count] = guessTmp = val.Value;
            }
        }

        interpolatedValues = new BiCGstab(v => SparseMatrixFunctions.Prod(g, v), maxIterMultiplier * N, relTol)
            .Solve(rhs, guess).X;
    }

    /// <summary>The value at a grid point, given or interpolated: QuantLib's <c>operator()</c>.</summary>
    /// <param name="coordinates">The point's coordinates, one per grid.</param>
    /// <returns>The value; 0 where it is missing and every grid has a single point.</returns>
    /// <exception cref="ArgumentException">The coordinates are not one per grid, with QuantLib's text.</exception>
    public double Value(IReadOnlyList<int> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        if (coordinates.Count != dimensions)
        {
            throw new ArgumentException(
                $"LaplaceInterpolation::operator(): expected {dimensions} coordinates, got {coordinates.Count}",
                nameof(coordinates));
        }

        if (numberOfCoordinatesIncluded == 0)
        {
            return y(coordinates) ?? 0.0;
        }

        int[] c = numberOfCoordinatesIncluded == dimensions ? [.. coordinates] : ProjectedCoordinates(coordinates);
        return interpolatedValues[layout!.Index(c)];
    }

    // The operator's stored elements of a row, explicit zeros included, in their column order.
    private static void CopyRow(SparseMatrix op, int row, SparseMatrix g)
    {
        if (row + 1 >= op.Filled1)
        {
            return;
        }

        ReadOnlySpan<int> starts = op.Index1Data;
        ReadOnlySpan<int> columns = op.Index2Data;
        ReadOnlySpan<double> values = op.ValueData;
        for (int k = starts[row]; k < starts[row + 1]; ++k)
        {
            g[row, columns[k]] = values[k];
        }
    }

    private int[] ProjectedCoordinates(IReadOnlyList<int> coordinates)
    {
        var tmp = new List<int>();
        for (int i = 0; i < coordinates.Count; ++i)
        {
            if (coordinateIncluded[i])
            {
                tmp.Add(coordinates[i]);
            }
        }

        return [.. tmp];
    }

    private int[] FullCoordinates(int[] projectedCoordinates)
    {
        var tmp = new int[coordinateIncluded.Length];
        for (int i = 0, count = 0; i < coordinateIncluded.Length; ++i)
        {
            if (coordinateIncluded[i])
            {
                tmp[i] = projectedCoordinates[count++];
            }
        }

        return tmp;
    }
}
