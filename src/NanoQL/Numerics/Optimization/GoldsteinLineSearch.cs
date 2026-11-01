// SPDX-FileCopyrightText: Copyright (C) 2015 Cheng Li
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/goldstein.hpp ql/math/optimization/goldstein.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.ArrayFunctions;

namespace NanoQL.Numerics;

/// <summary>Goldstein and Price's line search: the step is bracketed until the Goldstein conditions hold.</summary>
/// <remarks>
/// A working type, as every line search is (<see cref="LineSearch"/>): <see cref="Evaluate"/> changes it and reads the
/// gradient its previous call left; a <see cref="LineSearchBasedMethod"/> runs each minimization on a new one
/// (<see cref="NewSearch"/>).
/// </remarks>
[WorkingType]
public sealed class GoldsteinLineSearch : LineSearch
{
    // QuantLib's Null<Real>(), the function value and gradient norm of a problem reset and not yet evaluated:
    // std::numeric_limits<float>::max().
    private const double NullReal = float.MaxValue;

    private readonly double eps;
    private readonly double alpha;
    private readonly double beta;
    private readonly double extrapolation;

    /// <summary>Initializes a new instance of the <see cref="GoldsteinLineSearch"/> class.</summary>
    /// <param name="eps">The tolerance.</param>
    /// <param name="alpha">The lower Goldstein coefficient.</param>
    /// <param name="beta">The upper Goldstein coefficient.</param>
    /// <param name="extrapolation">The factor a step grows by while no upper bracket is found.</param>
    public GoldsteinLineSearch(double eps = 1e-8, double alpha = 0.05, double beta = 0.65, double extrapolation = 1.5)
        : base(eps)
    {
        this.eps = eps;
        this.alpha = alpha;
        this.beta = beta;
        this.extrapolation = extrapolation;
    }

    /// <inheritdoc/>
    public override double Evaluate(ProblemState P, ref EndCriteriaType ecType, EndCriteria endCriteria, double t_ini)
    {
        ArgumentNullException.ThrowIfNull(P);
        ArgumentNullException.ThrowIfNull(endCriteria);

        Constraint constraint = P.Constraint;
        Succeeded = true;
        bool maxIter = false;
        double t = t_ini;
        int loopNumber = 0;

        double q0 = P.FunctionValue ?? NullReal;
        double qp0 = P.GradientNormValue ?? NullReal;

        double tl = 0.0;
        double tr = 0.0;

        Qt = q0;
        Qpt = GradientValue.Empty ? qp0 : -DotProduct(GradientValue, SearchDirection);

        // Initialize gradient
        GradientValue = new Vector(P.CurrentValue.Size);

        // Compute new point
        Xtd = P.CurrentValue.Clone();
        t = Update(Xtd, SearchDirection, t, constraint);

        // Compute function value at the new point
        Qt = P.Value(Xtd);

        while ((Qt - q0) < -beta * t * Qpt || (Qt - q0) > -alpha * t * Qpt)
        {
            if ((Qt - q0) > -alpha * t * Qpt)
            {
                tr = t;
            }
            else
            {
                tl = t;
            }

            ++loopNumber;

            // calculate the new step
            if (ComparisonFunctions.CloseEnough(tr, 0.0))
            {
                t *= extrapolation;
            }
            else
            {
                t = (tl + tr) / 2.0;
            }

            // New point value
            Xtd = P.CurrentValue.Clone();
            t = Update(Xtd, SearchDirection, t, constraint);

            // Compute function value at the new point
            Qt = P.Value(Xtd);
            P.Gradient(GradientValue, Xtd);

            // and it squared norm
            maxIter = endCriteria.CheckMaxIterations(loopNumber);
            if (maxIter)
            {
                ecType = EndCriteriaType.MaxIterations;
                break;
            }
        }

        if (maxIter)
        {
            Succeeded = false;
        }

        // Compute new gradient
        P.Gradient(GradientValue, Xtd);

        // and it squared norm
        Qpt = DotProduct(GradientValue, GradientValue);

        // Return new step value
        return t;
    }

    /// <inheritdoc/>
    protected internal override LineSearch NewSearch() => new GoldsteinLineSearch(eps, alpha, beta, extrapolation);
}
