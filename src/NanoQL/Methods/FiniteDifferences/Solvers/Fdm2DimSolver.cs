// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdm2dimsolver.hpp ql/methods/finitedifferences/solvers/fdm2dimsolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The two-dimensional finite-difference solver: rolls the cell-averaged inner values back from maturity to time 0, and
/// reads the value, its derivatives and the theta at a point through a bicubic spline.
/// </summary>
/// <remarks>
/// Computed at construction, where QuantLib's lazy object computes at the first read, as the one-dimensional solver is,
/// its spline built there; immutable after, its reads changing nothing. The theta comes from a snapshot of the values a
/// short time before time 0.
/// </remarks>
public sealed class Fdm2DimSolver
{
    private readonly FdmSnapshotCondition thetaCondition;
    private readonly FdmStepConditionComposite conditions;
    private readonly double[] x;
    private readonly double[] y;
    private readonly Matrix resultValues;
    private readonly BicubicSpline interpolation;

    /// <summary>Initializes a new instance of the <see cref="Fdm2DimSolver"/> class, and rolls the problem back.</summary>
    /// <param name="solverDesc">The problem.</param>
    /// <param name="schemeDesc">The scheme.</param>
    /// <param name="op">The operator.</param>
    /// <exception cref="ArgumentNullException">An argument or one of the problem's parts is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of steps is negative.</exception>
    /// <exception cref="ArgumentException">The rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">A step or the spline fails.</exception>
    public Fdm2DimSolver(FdmSolverDesc solverDesc, FdmSchemeDesc schemeDesc, FdmLinearOpComposite op)
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

        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            initialValues[iter.Index] = solverDesc.Calculator.AvgInnerValue(iter, solverDesc.Maturity);

            if (iter.Coordinates[1] == 0)
            {
                xs.Add(solverDesc.Mesher.Location(iter, 0));
            }

            if (iter.Coordinates[0] == 0)
            {
                ys.Add(solverDesc.Mesher.Location(iter, 1));
            }
        }

        x = [.. xs];
        y = [.. ys];

        // QuantLib's performCalculations
        double[] rhs = (double[])initialValues.Clone();

        new FdmBackwardSolver(op, solverDesc.BcSet, conditions, schemeDesc)
            .Rollback(rhs, solverDesc.Maturity, 0.0, solverDesc.TimeSteps, solverDesc.DampingSteps);

        resultValues = new Matrix(layout.Dim[1], layout.Dim[0], rhs);
        interpolation = new BicubicSpline(x, y, resultValues);
    }

    /// <summary>The value at a point.</summary>
    /// <param name="x">The point's first coordinate.</param>
    /// <param name="y">The point's second coordinate.</param>
    /// <returns>The interpolated value.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double InterpolateAt(double x, double y) => interpolation.Evaluate(x, y);

    /// <summary>The theta at a point: the change of value from the snapshot's time to time 0, by that time.</summary>
    /// <param name="x">The point's first coordinate.</param>
    /// <param name="y">The point's second coordinate.</param>
    /// <returns>The theta, or <see langword="null"/> (QuantLib's <c>Null&lt;Real&gt;()</c>) when the first stopping time is 0.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double? ThetaAt(double x, double y)
    {
        if (conditions.StoppingTimes[0] == 0.0)
        {
            return null;
        }

        var thetaValues = new Matrix(resultValues.Rows, resultValues.Columns, thetaCondition.Values);

        double temp = new BicubicSpline(this.x, this.y, thetaValues).Evaluate(x, y);
        return (temp - InterpolateAt(x, y)) / thetaCondition.Time;
    }

    /// <summary>The first derivative in the first coordinate at a point.</summary>
    /// <param name="x">The point's first coordinate.</param>
    /// <param name="y">The point's second coordinate.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double DerivativeX(double x, double y) => interpolation.DerivativeX(x, y);

    /// <summary>The first derivative in the second coordinate at a point.</summary>
    /// <param name="x">The point's first coordinate.</param>
    /// <param name="y">The point's second coordinate.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double DerivativeY(double x, double y) => interpolation.DerivativeY(x, y);

    /// <summary>The second derivative in the first coordinate at a point.</summary>
    /// <param name="x">The point's first coordinate.</param>
    /// <param name="y">The point's second coordinate.</param>
    /// <returns>The second derivative.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double DerivativeXX(double x, double y) => interpolation.SecondDerivativeX(x, y);

    /// <summary>The second derivative in the second coordinate at a point.</summary>
    /// <param name="x">The point's first coordinate.</param>
    /// <param name="y">The point's second coordinate.</param>
    /// <returns>The second derivative.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double DerivativeYY(double x, double y) => interpolation.SecondDerivativeY(x, y);

    /// <summary>The mixed derivative at a point.</summary>
    /// <param name="x">The point's first coordinate.</param>
    /// <param name="y">The point's second coordinate.</param>
    /// <returns>The mixed derivative.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double DerivativeXY(double x, double y) => interpolation.DerivativeXY(x, y);

    // std::min(a, b)
    private static double Min(double a, double b) => b < a ? b : a;
}
