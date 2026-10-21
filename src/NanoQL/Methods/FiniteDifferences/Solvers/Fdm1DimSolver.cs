// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdm1dimsolver.hpp ql/methods/finitedifferences/solvers/fdm1dimsolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The one-dimensional finite-difference solver: rolls the cell-averaged inner values back from maturity to time 0, and
/// reads the value, its derivatives and the theta at a point through a monotonic cubic natural spline.
/// </summary>
/// <remarks>
/// Computed at construction, where QuantLib's lazy object computes at the first read, and immutable after: its reads
/// change nothing. The theta comes from a snapshot of the values a short time before time 0.
/// </remarks>
public sealed class Fdm1DimSolver
{
    private readonly FdmSnapshotCondition thetaCondition;
    private readonly FdmStepConditionComposite conditions;
    private readonly double[] x;
    private readonly double[] resultValues;
    private readonly CubicInterpolation interpolation;

    /// <summary>Initializes a new instance of the <see cref="Fdm1DimSolver"/> class, and rolls the problem
    /// back.</summary>
    /// <param name="solverDesc">The problem.</param>
    /// <param name="schemeDesc">The scheme.</param>
    /// <param name="op">The operator.</param>
    /// <exception cref="ArgumentNullException">An argument or one of the problem's parts is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of steps is negative.</exception>
    /// <exception cref="ArgumentException">The rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">A step or the spline fails.</exception>
    public Fdm1DimSolver(FdmSolverDesc solverDesc, FdmSchemeDesc schemeDesc, FdmLinearOpComposite op)
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
        x = new double[layout.Size];
        double[] initialValues = new double[layout.Size];

        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            initialValues[iter.Index] = solverDesc.Calculator.AvgInnerValue(iter, solverDesc.Maturity);
            x[iter.Index] = solverDesc.Mesher.Location(iter, 0);
        }

        // QuantLib's performCalculations
        double[] rhs = (double[])initialValues.Clone();

        new FdmBackwardSolver(op, solverDesc.BcSet, conditions, schemeDesc)
            .Rollback(rhs, solverDesc.Maturity, 0.0, solverDesc.TimeSteps, solverDesc.DampingSteps);

        resultValues = rhs;
        interpolation = new MonotonicCubicNaturalSpline(x, resultValues);
    }

    /// <summary>The value at a point.</summary>
    /// <param name="x">The point, in the grid's coordinate.</param>
    /// <returns>The interpolated value.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double InterpolateAt(double x) => interpolation.Evaluate(x);

    /// <summary>The theta at a point: the change of value from the snapshot's time to time 0, by that time.</summary>
    /// <param name="x">The point, in the grid's coordinate.</param>
    /// <returns>The theta, or <see langword="null"/> (QuantLib's <c>Null&lt;Real&gt;()</c>) when the first stopping
    /// time is 0.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double? ThetaAt(double x)
    {
        if (conditions.StoppingTimes[0] == 0.0)
        {
            return null;
        }

        double[] thetaValues = new double[resultValues.Length];

        thetaCondition.Values.CopyTo(thetaValues);

        double temp = new MonotonicCubicNaturalSpline(this.x, thetaValues).Evaluate(x);
        return (temp - InterpolateAt(x)) / thetaCondition.Time;
    }

    /// <summary>The first derivative at a point.</summary>
    /// <param name="x">The point, in the grid's coordinate.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double DerivativeX(double x) => interpolation.Derivative(x);

    /// <summary>The second derivative at a point.</summary>
    /// <param name="x">The point, in the grid's coordinate.</param>
    /// <returns>The second derivative.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double DerivativeXX(double x) => interpolation.SecondDerivative(x);

    // std::min(a, b)
    private static double Min(double a, double b) => b < a ? b : a;
}
