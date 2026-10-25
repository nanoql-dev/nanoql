// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdm3dimsolver.hpp ql/methods/finitedifferences/solvers/fdm3dimsolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The three-dimensional finite-difference solver: rolls the initial values back over the mesher with the operator,
/// the conditions and the boundaries, and interpolates the result.
/// </summary>
/// <remarks>
/// Immutable: the rollback runs in the constructor, where QuantLib's solver runs it lazily on the first query (the
/// S-hes plan's HD8, S-sr's <see cref="Fdm2DimSolver"/> form). A value is a bicubic spline in each layer of the third
/// coordinate, then a monotonic natural cubic spline across the layers, QuantLib's. A snapshot just after the start
/// gives the theta.
/// </remarks>
public sealed class Fdm3DimSolver
{
    private readonly FdmSnapshotCondition thetaCondition;
    private readonly FdmStepConditionComposite conditions;
    private readonly double[] x;
    private readonly double[] y;
    private readonly double[] z;
    private readonly Matrix[] resultValues;
    private readonly BicubicSpline[] interpolation;

    /// <summary>Initializes a new instance of the <see cref="Fdm3DimSolver"/> class, and rolls back.</summary>
    /// <param name="solverDesc">The problem: the mesher, the boundaries, the conditions, the calculator, the maturity
    /// and the steps.</param>
    /// <param name="schemeDesc">The scheme.</param>
    /// <param name="op">The operator.</param>
    /// <exception cref="ArgumentNullException">An argument, or the description's mesher, condition or calculator, is
    /// <see langword="null"/>.</exception>
    public Fdm3DimSolver(FdmSolverDesc solverDesc, FdmSchemeDesc schemeDesc, FdmLinearOpComposite op)
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

        FdmLinearOpLayout layout = solverDesc.Mesher.Layout;
        double[] initialValues = new double[layout.Size];
        var xs = new List<double>(layout.Dim[0]);
        var ys = new List<double>(layout.Dim[1]);
        var zs = new List<double>(layout.Dim[2]);

        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            initialValues[iter.Index] = solverDesc.Calculator.AvgInnerValue(iter, solverDesc.Maturity);

            if (iter.Coordinates[1] == 0 && iter.Coordinates[2] == 0)
            {
                xs.Add(solverDesc.Mesher.Location(iter, 0));
            }

            if (iter.Coordinates[0] == 0 && iter.Coordinates[2] == 0)
            {
                ys.Add(solverDesc.Mesher.Location(iter, 1));
            }

            if (iter.Coordinates[0] == 0 && iter.Coordinates[1] == 0)
            {
                zs.Add(solverDesc.Mesher.Location(iter, 2));
            }
        }

        x = [.. xs];
        y = [.. ys];
        z = [.. zs];

        // QuantLib's performCalculations
        double[] rhs = (double[])initialValues.Clone();

        new FdmBackwardSolver(op, solverDesc.BcSet, conditions, schemeDesc)
            .Rollback(rhs, solverDesc.Maturity, 0.0, solverDesc.TimeSteps, solverDesc.DampingSteps);

        resultValues = new Matrix[z.Length];
        interpolation = new BicubicSpline[z.Length];
        int layer = y.Length * x.Length;
        for (int i = 0; i < z.Length; ++i)
        {
            resultValues[i] = new Matrix(y.Length, x.Length, rhs.AsSpan(i * layer, layer));
            interpolation[i] = new BicubicSpline(x, y, resultValues[i]);
        }
    }

    /// <summary>The value at a point, interpolated.</summary>
    /// <param name="x">The first coordinate.</param>
    /// <param name="y">The second coordinate.</param>
    /// <param name="z">The third coordinate.</param>
    /// <returns>The value.</returns>
    public double InterpolateAt(double x, double y, double z)
    {
        double[] zArray = new double[this.z.Length];
        for (int i = 0; i < this.z.Length; ++i)
        {
            zArray[i] = interpolation[i].Evaluate(x, y);
        }

        return new MonotonicCubicNaturalSpline(this.z, zArray).Evaluate(z);
    }

    /// <summary>The theta at a point: the change from the snapshot just after the start, over its time.</summary>
    /// <param name="x">The first coordinate.</param>
    /// <param name="y">The second coordinate.</param>
    /// <param name="z">The third coordinate.</param>
    /// <returns>The theta, or <see langword="null"/> where the first stopping time is the start, QuantLib's
    /// <c>Null&lt;Real&gt;()</c>.</returns>
    public double? ThetaAt(double x, double y, double z)
    {
        if (conditions.StoppingTimes[0] == 0.0)
        {
            return null;
        }

        ReadOnlySpan<double> rhs = thetaCondition.Values;
        int layer = this.y.Length * this.x.Length;
        double[] zArray = new double[this.z.Length];
        for (int i = 0; i < this.z.Length; ++i)
        {
            var thetaValues = new Matrix(this.y.Length, this.x.Length, rhs.Slice(i * layer, layer));
            zArray[i] = new BicubicSpline(this.x, this.y, thetaValues).Evaluate(x, y);
        }

        return (new MonotonicCubicNaturalSpline(this.z, zArray).Evaluate(z) - InterpolateAt(x, y, z)) / thetaCondition.Time;
    }

    // std::min(a, b)
    private static double Min(double a, double b) => b < a ? b : a;
}
