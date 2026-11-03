// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmndimsolver.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The n-dimensional finite-difference solver: a problem rolled back on an operator of any dimension, its values read
/// through a <see cref="MultiCubicSpline"/> over the mesher's axes.
/// </summary>
/// <remarks>
/// <para>
/// Immutable and safe to share across threads: the rollback and the spline run in the constructor, where QuantLib's
/// lazy object runs them at the first query (the playbook's finite-difference row). Its dimension is the mesher
/// layout's, read at construction, where QuantLib's is a template parameter checked against the layout ("solver dim N
/// does not fit to layout dim"), a check that can't fail here; the spline's limit of 15 dimensions is its.
/// </para>
/// </remarks>
public sealed class FdmNdimSolver
{
    private readonly FdmSnapshotCondition thetaCondition;
    private readonly FdmStepConditionComposite conditions;
    private readonly FdmLinearOpLayout layout;
    private readonly ImmutableArray<ImmutableArray<double>> x;
    private readonly MultiCubicSpline interp;

    /// <summary>Initializes a new instance of the <see cref="FdmNdimSolver"/> class, and solves the problem.</summary>
    /// <param name="solverDesc">The problem.</param>
    /// <param name="schemeDesc">The scheme.</param>
    /// <param name="op">The operator, of the mesher's dimension.</param>
    /// <exception cref="ArgumentNullException">The problem, its mesher, condition or calculator, the scheme or the
    /// operator is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The rollback's arguments are refused, or an axis of the mesher can't carry the spline (fewer than four points,
    /// not increasing, or more than 15 dimensions).
    /// </exception>
    /// <exception cref="NanoQLException">A step fails.</exception>
    public FdmNdimSolver(FdmSolverDesc solverDesc, FdmSchemeDesc schemeDesc, FdmLinearOpComposite op)
    {
        ArgumentNullException.ThrowIfNull(solverDesc);
        ArgumentNullException.ThrowIfNull(solverDesc.Mesher, nameof(solverDesc));
        ArgumentNullException.ThrowIfNull(solverDesc.Condition, nameof(solverDesc));
        ArgumentNullException.ThrowIfNull(solverDesc.Calculator, nameof(solverDesc));
        ArgumentNullException.ThrowIfNull(op);

        thetaCondition = new FdmSnapshotCondition(
            0.99 * Min(1.0 / 365.0,
                       solverDesc.Condition.StoppingTimes.IsEmpty
                           ? solverDesc.Maturity
                           : solverDesc.Condition.StoppingTimes[0]));
        conditions = FdmStepConditionComposite.JoinConditions(thetaCondition, solverDesc.Condition);

        layout = solverDesc.Mesher.Layout;
        int n = layout.Dim.Length;
        var axes = new List<double>[n];
        for (int i = 0; i < n; ++i)
        {
            axes[i] = new List<double>(layout.Dim[i]);
        }

        double[] initialValues = new double[layout.Size];
        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            initialValues[iter.Index] = solverDesc.Calculator.AvgInnerValue(iter, solverDesc.Maturity);

            // QuantLib's accumulate(c) - c[i] == 0: every other coordinate is 0
            ReadOnlySpan<int> c = iter.Coordinates;
            long sum = 0;
            foreach (int ci in c)
            {
                sum += ci;
            }

            for (int i = 0; i < n; ++i)
            {
                if (sum - c[i] == 0)
                {
                    axes[i].Add(solverDesc.Mesher.Location(iter, i));
                }
            }
        }

        var grid = ImmutableArray.CreateBuilder<ImmutableArray<double>>(n);
        foreach (List<double> axis in axes)
        {
            grid.Add([.. axis]);
        }

        x = grid.MoveToImmutable();

        // QuantLib's performCalculations
        double[] rhs = (double[])initialValues.Clone();
        new FdmBackwardSolver(op, solverDesc.BcSet, conditions, schemeDesc)
            .Rollback(rhs, solverDesc.Maturity, 0.0, solverDesc.TimeSteps, solverDesc.DampingSteps);

        interp = new MultiCubicSpline(x, DataTable(rhs), [.. new bool[n]]);
    }

    /// <summary>The value at a point.</summary>
    /// <param name="x">The point, one coordinate per direction.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentException">
    /// The point doesn't hold one coordinate per direction, or lies outside the grid.
    /// </exception>
    public double InterpolateAt(ReadOnlySpan<double> x) => interp.Evaluate(x);

    /// <summary>The theta at a point: the change over the snapshot's time just before the first stopping
    /// time.</summary>
    /// <param name="x">The point, one coordinate per direction.</param>
    /// <returns>The theta, or <see langword="null"/> when the problem's first stopping time is 0.</returns>
    /// <exception cref="ArgumentException">
    /// The point doesn't hold one coordinate per direction, or lies outside the grid.
    /// </exception>
    public double? ThetaAt(ReadOnlySpan<double> x)
    {
        if (conditions.StoppingTimes[0] == 0.0)
        {
            return null;
        }

        var thetaSpline = new MultiCubicSpline(this.x, DataTable(thetaCondition.Values));
        return (thetaSpline.Evaluate(x) - InterpolateAt(x)) / thetaCondition.Time;
    }

    // QuantLib's setValue over the layout: each point's value at its coordinates in the nested data table, the first
    // direction slowest, where the layout's storage runs the first fastest.
    private double[] DataTable(ReadOnlySpan<double> rhs)
    {
        int n = layout.Dim.Length;
        int[] strides = new int[n];
        int stride = 1;
        for (int i = n - 1; i >= 0; --i)
        {
            strides[i] = stride;
            stride *= layout.Dim[i];
        }

        double[] f = new double[layout.Size];
        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            ReadOnlySpan<int> c = iter.Coordinates;
            int index = 0;
            for (int i = 0; i < n; ++i)
            {
                index += c[i] * strides[i];
            }

            f[index] = rhs[iter.Index];
        }

        return f;
    }

    // std::min(a, b)
    private static double Min(double a, double b) => b < a ? b : a;
}
