// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/simplex.hpp ql/math/optimization/simplex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

/* The implementation of the algorithm was highly inspired by
 * "Numerical Recipes in C", 2nd edition, Press, Teukolsky, Vetterling,
 * Flannery, chapter 10.
 * Modified may 2007: end criteria set on x instead on fx,
 * inspired by bad behaviour found with test function fx=x*x+x+1,
 * xStart = -100, lambda = 1.0, ftol = 1.e-16
 * (it reports x=0 as the minimum!)
 * and by GSL implementation, v. 1.9 (http://www.gnu.org/software/gsl/)
 */

namespace NanoQL.Numerics;

/// <summary>The multi-dimensional downhill simplex method of Nelder and Mead.</summary>
/// <remarks>
/// <para>
/// This method is rather raw and requires quite a lot of computing resources, but it has the advantage that it does
/// not need any evaluation of the cost function's gradient, and that it is quite easily implemented. First, we choose
/// N+1 starting points, given here by a starting point P₀ and N points such that Pᵢ = P₀ + λeᵢ, where λ is the
/// problem's characteristic length scale. These points will form a geometrical form called simplex. The principle of
/// the downhill simplex method is, at each iteration, to move the worst point (highest cost function value) through
/// the opposite face to a better point. When the simplex seems to be constrained in a valley, it will be contracted
/// downhill, keeping the best point unchanged.
/// </para>
/// <para>
/// The minimization ends when the simplex's size, the mean distance of its vertices from their centre, falls below the
/// end criteria's root epsilon, or at the maximum number of iterations. Immutable: the vertices, which QuantLib keeps
/// in <c>mutable</c> members, live inside each minimization.
/// </para>
/// </remarks>
public sealed class Simplex : OptimizationMethod
{
    /// <summary>Initializes a new instance of the <see cref="Simplex"/> class.</summary>
    /// <param name="lambda">The characteristic length, λ.</param>
    public Simplex(double lambda)
    {
        Lambda = lambda;
    }

    /// <summary>Gets the characteristic length, λ.</summary>
    public double Lambda { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The current value doesn't satisfy the constraint, or it is empty, where QuantLib reads past its arrays (a
    /// divergence: nanoql refuses it).
    /// </exception>
    protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria)
    {
        // set up of the problem
        //Real ftol = endCriteria.functionEpsilon();    // end criteria on f(x) (see Numerical Recipes in C++, p.410)
        double xtol = endCriteria.RootEpsilon;          // end criteria on x (see GSL v. 1.9, http://www.gnu.org/software/gsl/)
        int maxStationaryStateIterations = endCriteria.MaxStationaryStateIterations;
        EndCriteriaType ecType = EndCriteriaType.None;
        P.Reset();

        Vector x = P.CurrentValue.Clone();
        if (!P.Constraint.Test(x))
        {
            throw new NanoQLException($"Initial guess {x} is not in the feasible region.");
        }

        int iterationNumber = 0;

        // Initialize vertices of the simplex
        int n = x.Size;

        // A divergence: with no variables QuantLib sizes values_ at n + 1 = 1 and then reads values_[1]
        // (simplex.cpp:99-106,119), a result C++ leaves undefined. nanoql refuses it.
        if (n == 0)
        {
            throw new NanoQLException("Simplex needs at least one variable: the initial value is empty");
        }
        var vertices = new Vector[n + 1];
        for (int i = 0; i <= n; i++)
        {
            vertices[i] = x.Clone();
        }

        for (int i = 0; i < n; ++i)
        {
            var direction = new Vector(n, 0.0);
            direction[i] = 1.0;
            P.Constraint.Update(vertices[i + 1], direction, Lambda);
        }

        // Initialize function values at the vertices of the simplex
        var values = new Vector(n + 1, 0.0);
        for (int i = 0; i <= n; ++i)
        {
            values[i] = P.Value(vertices[i]);
        }

        // Loop looking for minimum
        do
        {
            var sum = new Vector(n, 0.0);
            int i;
            for (i = 0; i <= n; i++)
            {
                sum += vertices[i];
            }

            // Determine the best (iLowest), worst (iHighest)
            // and 2nd worst (iNextHighest) vertices
            int iLowest = 0;
            int iHighest, iNextHighest;
            if (values[0] < values[1])
            {
                iHighest = 1;
                iNextHighest = 0;
            }
            else
            {
                iHighest = 0;
                iNextHighest = 1;
            }

            for (i = 1; i <= n; i++)
            {
                if (values[i] > values[iHighest])
                {
                    iNextHighest = iHighest;
                    iHighest = i;
                }
                else
                {
                    if ((values[i] > values[iNextHighest]) && i != iHighest)
                    {
                        iNextHighest = i;
                    }
                }

                if (values[i] < values[iLowest])
                {
                    iLowest = i;
                }
            }

            // Now compute accuracy, update iteration number and check end criteria
            // GSL exit strategy on x (see GSL v. 1.9, http://www.gnu.org/software/gsl
            double simplexSize = ComputeSimplexSize(vertices);
            ++iterationNumber;
            bool maxIterations = false;
            if (simplexSize < xtol || (maxIterations = endCriteria.CheckMaxIterations(iterationNumber)))
            {
                // QuantLib's checks set ecType when they hold: the iteration check of the condition, the stationary
                // check (which, at the maximum count, holds whenever the root epsilon is positive), then the
                // iteration check again.
                if (maxIterations)
                {
                    ecType = EndCriteriaType.MaxIterations;
                }

                EndCriteriaCheck stationary =
                    endCriteria.CheckStationaryPoint(0.0, 0.0, maxStationaryStateIterations);
                maxStationaryStateIterations = stationary.StationaryStateIterations;
                if (stationary.Stop)
                {
                    ecType = stationary.Type;
                }

                if (endCriteria.CheckMaxIterations(iterationNumber))
                {
                    ecType = EndCriteriaType.MaxIterations;
                }

                x = vertices[iLowest];
                double low = values[iLowest];
                P.FunctionValue = low;
                P.CurrentValue = x;
                return ecType;
            }

            // If end criteria is not met, continue
            double factor = -1.0;
            double vTry = Extrapolate(P, iHighest, ref factor, vertices, values, sum);
            if ((vTry <= values[iLowest]) && (factor == -1.0))
            {
                factor = 2.0;
                Extrapolate(P, iHighest, ref factor, vertices, values, sum);
            }
            else if (Math.Abs(factor) > NumericLimits.QL_EPSILON)
            {
                if (vTry >= values[iNextHighest])
                {
                    double vSave = values[iHighest];
                    factor = 0.5;
                    vTry = Extrapolate(P, iHighest, ref factor, vertices, values, sum);
                    if (vTry >= vSave && Math.Abs(factor) > NumericLimits.QL_EPSILON)
                    {
                        for (int k = 0; k <= n; k++)
                        {
                            if (k != iLowest)
                            {
                                vertices[k] = 0.5 * (vertices[k] + vertices[iLowest]);
                                values[k] = P.Value(vertices[k]);
                            }
                        }
                    }
                }
            }

            // If can't extrapolate given the constraints, exit
            if (Math.Abs(factor) <= NumericLimits.QL_EPSILON)
            {
                x = vertices[iLowest];
                double low = values[iLowest];
                P.FunctionValue = low;
                P.CurrentValue = x;
                return EndCriteriaType.StationaryFunctionValue;
            }
        }
        while (true);

        // QuantLib's QL_FAIL("optimization failed: unexpected behaviour") after the loop is unreachable.
    }

    // Computes the size of the simplex
    private static double ComputeSimplexSize(Vector[] vertices)
    {
        var center = new Vector(vertices[0].Size, 0);
        foreach (Vector vertice in vertices)
        {
            center += vertice;
        }

        center *= 1 / (double)vertices.Length;
        double result = 0;
        foreach (Vector vertice in vertices)
        {
            Vector temp = vertice - center;
            result += ArrayFunctions.Norm2(temp);
        }

        return result / vertices.Length;
    }

    // QuantLib's extrapolate, over the minimization's vertices, values and their sum, which QuantLib keeps in members:
    // it moves the highest vertex through the opposite face by the factor, halving it until the point is feasible,
    // and replaces the vertex, its value and the sum in place when the point is lower.
    private static double Extrapolate(
        ProblemState P, int iHighest, ref double factor, Vector[] vertices, Vector values, Vector sum)
    {
        Vector pTry;
        do
        {
            int dimensions = values.Size - 1;
            double factor1 = (1.0 - factor) / dimensions;
            double factor2 = factor1 - factor;
            pTry = (sum * factor1) - (vertices[iHighest] * factor2);
            factor *= 0.5;
        }
        while (!P.Constraint.Test(pTry) && Math.Abs(factor) > NumericLimits.QL_EPSILON);

        if (Math.Abs(factor) <= NumericLimits.QL_EPSILON)
        {
            return values[iHighest];
        }

        factor *= 2.0;
        double vTry = P.Value(pTry);
        if (vTry < values[iHighest])
        {
            values[iHighest] = vTry;
            sum += pTry - vertices[iHighest];
            vertices[iHighest] = pTry;
        }

        return vTry;
    }
}
