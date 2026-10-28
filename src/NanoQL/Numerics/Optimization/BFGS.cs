// SPDX-FileCopyrightText: Copyright (C) 2009 Frédéric Degraeve
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/bfgs.hpp ql/math/optimization/bfgs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Broyden-Fletcher-Goldfarb-Shanno method (<see href="http://en.wikipedia.org/wiki/BFGS_method"/>), adapted from
/// Numerical Recipes in C, 2nd edition.
/// </summary>
/// <remarks>
/// <para>
/// The user has to provide a line-search method and optimization end criteria. Immutable: each minimization runs on a
/// new line search and a new inverse Hessian, the identity at its start (the S-core plan's CR5 (a); SciPy's
/// <c>hess_inv0</c>). QuantLib's method keeps its inverse Hessian as a member, built on its first minimization only, so
/// a second minimization on one object starts from the first's Hessian, and one of another dimension reads it out of
/// range: a documented divergence (<c>Divergences/BFGSTests.cs</c>).
/// </para>
/// <para>
/// The update is QuantLib's, on the search direction before the line search's step scales it, not on the step, which
/// QuantLib's Armijo search, starting each iteration from the previous step and only shrinking it, compensates; kept as
/// it is (the plan's CR7, its kept designs): the textbook update fails to converge with the same searches.
/// </para>
/// </remarks>
public sealed class BFGS : LineSearchBasedMethod
{
    /// <summary>Initializes a new instance of the <see cref="BFGS"/> class.</summary>
    /// <param name="lineSearch">The line search, a prototype; <see langword="null"/> for an
    /// <see cref="ArmijoLineSearch"/> with its defaults.</param>
    public BFGS(LineSearch? lineSearch = null)
        : base(lineSearch)
    {
    }

    /// <inheritdoc/>
    protected override LineSearchBasedMethodState NewMinimizationState(ProblemState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int n = state.CurrentValue.Size;

        // first time in this update, we create needed structures
        var inverseHessian = new Matrix(n, n, 0.0);
        for (int i = 0; i < n; ++i)
        {
            inverseHessian[i, i] = 1.0;
        }

        return new HessianState(inverseHessian);
    }

    /// <inheritdoc/>
    protected override Vector GetUpdatedDirection(
        ProblemState P, double gold2, Vector gradient, LineSearch lineSearch, LineSearchBasedMethodState? minimizationState)
    {
        ArgumentNullException.ThrowIfNull(P);
        ArgumentNullException.ThrowIfNull(gradient);
        ArgumentNullException.ThrowIfNull(lineSearch);
        Matrix inverseHessian = ((HessianState)minimizationState!).InverseHessian;
        int n = P.CurrentValue.Size;
        Vector searchDirection = lineSearch.SearchDirection;
        Vector lastGradient = lineSearch.LastGradient;

        var diffGradientWithHessianApplied = new Vector(n, 0.0);

        Vector diffGradient = lastGradient - gradient;
        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
            {
                diffGradientWithHessianApplied[i] += inverseHessian[i, j] * diffGradient[j];
            }
        }

        double fac, fae, fad;
        double sumdg, sumxi;

        fac = fae = sumdg = sumxi = 0.0;
        for (int i = 0; i < n; ++i)
        {
            fac += diffGradient[i] * searchDirection[i];
            fae += diffGradient[i] * diffGradientWithHessianApplied[i];
            sumdg += Math.Pow(diffGradient[i], 2.0);
            sumxi += Math.Pow(searchDirection[i], 2.0);
        }

        // skip update if fac not sufficiently positive
        if (fac > Math.Sqrt(1e-8 * sumdg * sumxi))
        {
            fac = 1.0 / fac;
            fad = 1.0 / fae;

            for (int i = 0; i < n; ++i)
            {
                diffGradient[i] = (fac * searchDirection[i]) - (fad * diffGradientWithHessianApplied[i]);
            }

            for (int i = 0; i < n; ++i)
            {
                for (int j = 0; j < n; ++j)
                {
                    inverseHessian[i, j] += fac * searchDirection[i] * searchDirection[j];
                    inverseHessian[i, j] -= fad * diffGradientWithHessianApplied[i] * diffGradientWithHessianApplied[j];
                    inverseHessian[i, j] += fae * diffGradient[i] * diffGradient[j];
                }
            }
        }

        var direction = new Vector(n);
        for (int i = 0; i < n; ++i)
        {
            direction[i] = 0.0;
            for (int j = 0; j < n; ++j)
            {
                direction[i] -= inverseHessian[i, j] * lastGradient[j];
            }
        }

        return direction;
    }

    // The inverse Hessian through one minimization.
    private sealed class HessianState(Matrix inverseHessian) : LineSearchBasedMethodState
    {
        public Matrix InverseHessian { get; } = inverseHessian;
    }
}
