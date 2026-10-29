// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmextoujumpop.hpp ql/experimental/finitedifferences/fdmextoujumpop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The finite-difference operator of Kluge's <see cref="ExtOUWithJumpsProcess"/> on a two-dimensional mesher, X in
/// direction 0 and the jump component Y in direction 1: the extended Ornstein-Uhlenbeck operator in X, Y's decay, and
/// the jumps' integral by Gauss-Laguerre quadrature over the exponential jump sizes, linearly interpolated in Y.
/// </summary>
/// <remarks>
/// <para>
/// A finite-difference operator is a solver's working state: <see cref="SetTime"/> sets the Ornstein-Uhlenbeck part in
/// place, as QuantLib's does; the integral's sparse matrix is built at construction.
/// </para>
/// <para>
/// Reference: T. Kluge, Pricing Swing Options and other Electricity Derivatives, 2008.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class FdmExtOUJumpOp : FdmLinearOpComposite
{
    private readonly FdmMesher mesher;
    private readonly ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet;
    private readonly FdmExtendedOrnsteinUhlenbeckOp ouOp;
    private readonly TripleBandLinearOp dyMap;
    private readonly SparseMatrix integroPart;

    // the decay's image, held
    private readonly double[] dy;

    /// <summary>Initializes a new instance of the <see cref="FdmExtOUJumpOp"/> class.</summary>
    /// <param name="mesher">The two-dimensional mesher, X in direction 0 and Y in direction 1.</param>
    /// <param name="process">The process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="bcSet">The boundary conditions.</param>
    /// <param name="integroIntegrationOrder">The Gauss-Laguerre quadrature's order.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FdmExtOUJumpOp(
        FdmMesher mesher,
        ExtOUWithJumpsProcess process,
        YieldTermStructure rTS,
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet,
        int integroIntegrationOrder)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(rTS);
        this.mesher = mesher;
        this.bcSet = bcSet.IsDefault ? [] : bcSet;
        var gaussLaguerreIntegration = new GaussLaguerreIntegration(integroIntegrationOrder);
        ouOp = new FdmExtendedOrnsteinUhlenbeckOp(mesher, process.ExtendedOrnsteinUhlenbeckProcess, rTS, this.bcSet);
        dyMap = new FirstDerivativeOp(1, mesher).Mult(-process.Beta * mesher.Locations(1));

        double eta = process.Eta;
        double lambda = process.JumpIntensity;
        Vector yInt = gaussLaguerreIntegration.X();
        Vector weights = gaussLaguerreIntegration.Weights();
        FdmLinearOpLayout layout = mesher.Layout;
        int n = layout.Size;
        integroPart = new SparseMatrix(n, n);
        dy = new double[n];

        var yLoc = new double[layout.Dim[1]];
        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < n; ++k, iter.Increment())
        {
            yLoc[iter.Coordinates[1]] = mesher.Location(iter, 1);
        }

        iter = layout.Begin();
        for (int k = 0; k < n; ++k, iter.Increment())
        {
            int diag = iter.Index;
            integroPart[diag, diag] -= lambda;
            double y = mesher.Location(iter, 1);
            int yIndex = iter.Coordinates[1];
            for (int i = 0; i < yInt.Size; ++i)
            {
                double weight = Math.Exp(-yInt[i]) * weights[i];
                double ys = y + (yInt[i] / eta);
                int l = ys > yLoc[^1] ? yLoc.Length - 2 : UpperBound(yLoc, ys) - 1;
                double s = (ys - yLoc[l]) / (yLoc[l + 1] - yLoc[l]);
                int left = layout.Neighbourhood(iter, 1, l - yIndex);
                integroPart[diag, left] += weight * lambda * (1 - s);
                int right = layout.Neighbourhood(iter, 1, l + 1 - yIndex);
                integroPart[diag, right] += weight * lambda * s;
            }
        }
    }

    /// <inheritdoc/>
    public override int Size => mesher.Layout.Dim.Length;

    /// <inheritdoc/>
    public override void SetTime(double t1, double t2) => ouOp.SetTime(t1, t2);

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        // QuantLib's ouOp.apply(r) + dyMap.apply(r) + integro(r), summed in that order
        ouOp.Apply(r, result);
        dyMap.Apply(r, dy);
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] += dy[i];
        }

        AddIntegro(r, result);
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result)
    {
        result[..r.Length].Clear();
        AddIntegro(r, result);
    }

    /// <inheritdoc/>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == 0)
        {
            ouOp.ApplyDirection(direction, r, result);
        }
        else if (direction == 1)
        {
            dyMap.Apply(r, result);
        }
        else
        {
            result[..r.Length].Clear();
        }
    }

    /// <inheritdoc/>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == 0)
        {
            ouOp.SolveSplitting(direction, r, s, result);
        }
        else if (direction == 1)
        {
            dyMap.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            r.CopyTo(result);
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        ouOp.SolveSplitting(0, r, s, result);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The operator has boundary conditions ("boundary conditions are not
    /// supported").</exception>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp()
    {
        if (!bcSet.IsEmpty)
        {
            throw new NanoQLException("boundary conditions are not supported");
        }

        return [ouOp.ToMatrixDecomp()[0], dyMap.ToMatrix(), integroPart.Clone()];
    }

    // result += integroPart·r, row by row in the matrix's stored order, as uBLAS's prod sums
    private void AddIntegro(ReadOnlySpan<double> r, Span<double> result)
    {
        ReadOnlySpan<int> index1 = integroPart.Index1Data, index2 = integroPart.Index2Data;
        ReadOnlySpan<double> values = integroPart.ValueData;
        for (int i = 0; i < integroPart.Filled1 - 1; ++i)
        {
            double t = 0.0;
            for (int j = index1[i]; j < index1[i + 1]; ++j)
            {
                t += values[j] * r[index2[j]];
            }

            result[i] += t;
        }
    }

    // std::upper_bound over yLoc[0, size - 1): the first index whose value exceeds ys, size - 1 if none does
    private static int UpperBound(double[] yLoc, double ys)
    {
        int lo = 0, hi = yLoc.Length - 1;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (ys < yLoc[mid])
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return lo;
    }
}
