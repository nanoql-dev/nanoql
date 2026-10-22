// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/armijo.hpp ql/math/optimization/armijo.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.ArrayFunctions;

namespace NanoQL.Numerics;

/// <summary>The Armijo line search.</summary>
/// <remarks>
/// Let α and β be two scalars in [0, 1], x the current value of the unknown, d the search direction and t the step,
/// and f the function to minimize. The line search stops when t verifies f(x + t·d) − f(x) ≤ −α t f′(x + t·d) and
/// f(x + t/β·d) − f(x) &gt; −α/β t f′(x + t·d) (see Polak, Algorithms and consistent approximations, Optimization,
/// volume 124 of Applied Mathematical Sciences, Springer-Verlag, NY, 1997).
/// </remarks>
public sealed class ArmijoLineSearch : LineSearch
{
    // QuantLib's Null<Real>(), the function value and gradient norm of a problem reset and not yet evaluated:
    // std::numeric_limits<float>::max().
    private const double NullReal = float.MaxValue;

    private readonly double eps;
    private readonly double alpha;
    private readonly double beta;

    /// <summary>Initializes a new instance of the <see cref="ArmijoLineSearch"/> class.</summary>
    /// <param name="eps">An accuracy QuantLib accepts and reads nowhere.</param>
    /// <param name="alpha">The scalar α, in [0, 1].</param>
    /// <param name="beta">The scalar β, in [0, 1], the factor each step is decreased by.</param>
    public ArmijoLineSearch(double eps = 1e-8, double alpha = 0.05, double beta = 0.65)
        : base(eps)
    {
        this.eps = eps;
        this.alpha = alpha;
        this.beta = beta;
    }

    /// <inheritdoc/>
    public override double Evaluate(ProblemState P, ref EndCriteriaType ecType, EndCriteria endCriteria, double t_ini)
    {
        ArgumentNullException.ThrowIfNull(P);
        ArgumentNullException.ThrowIfNull(endCriteria);

        //OptimizationMethod& method = P.method();
        Constraint constraint = P.Constraint;
        Succeeded = true;
        bool maxIter = false;
        double qtold, t = t_ini;
        int loopNumber = 0;

        double q0 = P.FunctionValue ?? NullReal;
        double qp0 = P.GradientNormValue ?? NullReal;

        Qt = q0;
        Qpt = GradientValue.Empty ? qp0 : -DotProduct(GradientValue, SearchDirection);

        // Initialize gradient
        GradientValue = new Vector(P.CurrentValue.Size);
        // Compute new point
        Xtd = P.CurrentValue.Clone();
        t = Update(Xtd, SearchDirection, t, constraint);
        // Compute function value at the new point
        Qt = P.Value(Xtd);

        // Enter in the loop if the criterion is not satisfied
        if ((Qt - q0) > -alpha * t * Qpt)
        {
            do
            {
                loopNumber++;
                // Decrease step
                t *= beta;
                // Store old value of the function
                qtold = Qt;
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
                }
            }
            while ((((Qt - q0) > (-alpha * t * Qpt)) || ((qtold - q0) <= (-alpha * t * Qpt / beta))) && !maxIter);
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
    protected internal override LineSearch NewSearch() => new ArmijoLineSearch(eps, alpha, beta);
}
