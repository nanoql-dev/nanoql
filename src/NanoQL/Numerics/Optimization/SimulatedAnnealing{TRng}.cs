// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/simulatedannealing.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.ArrayFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// Simulated annealing: Numerical Recipes in C (second edition), Chapter 10.9, with the original exit criterion in f(x)
/// replaced by one in x (as the simplex method's).
/// </summary>
/// <typeparam name="TRng">The generator of the thermal fluctuations, whose samples are uniform in (0, 1).</typeparam>
/// <remarks>
/// <para>
/// Immutable: it holds a copy of the generator it is given, and each minimization draws from a copy of that, so a
/// second minimization repeats the first's draws, where QuantLib's continues its generator's stream (the S-core plan's
/// CR2 (c); <c>Divergences/SimulatedAnnealingTests.cs</c>). The generator is required: QuantLib's default, a Mersenne
/// Twister seeded from the clock, is refused.
/// </para>
/// <para>
/// A vertex whose value is NaN enters the simplex at the largest double, where QuantLib's guard on the initial
/// vertices tests the previous trial's value instead (left from an earlier minimization, or unset) and so lets the NaN
/// in (CR7 (xix)); the vertices a contraction moves are guarded alike, which QuantLib's leaves out.
/// </para>
/// </remarks>
public sealed class SimulatedAnnealing<TRng> : OptimizationMethod
    where TRng : class, IRng
{
    private readonly Scheme scheme;
    private readonly double lambda, t0, epsilon, alpha;
    private readonly int k;
    private readonly int m;
    private readonly IRng rng;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulatedAnnealing{TRng}"/> class that reduces the temperature T by
    /// a factor of (1 − ε) after m moves.
    /// </summary>
    /// <param name="lambda">The characteristic length of the initial simplex.</param>
    /// <param name="T0">The initial temperature.</param>
    /// <param name="epsilon">The temperature's reduction factor ε.</param>
    /// <param name="m">The number of moves between reductions, not negative.</param>
    /// <param name="rng">The generator, which it copies.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="m"/> is negative.</exception>
    public SimulatedAnnealing(double lambda, double T0, double epsilon, int m, TRng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentOutOfRangeException.ThrowIfNegative(m);
        scheme = Scheme.ConstantFactor;
        this.lambda = lambda;
        t0 = T0;
        this.epsilon = epsilon;
        alpha = 0.0;
        k = 0;
        this.m = m;
        this.rng = rng.Clone();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulatedAnnealing{TRng}"/> class that budgets a total of K moves
    /// and sets the temperature T to the initial temperature times (1 − k/K)^α, with k the total number of moves so
    /// far. After K moves the temperature is guaranteed to be zero, after that the optimization runs like a deterministic
    /// simplex algorithm.
    /// </summary>
    /// <param name="lambda">The characteristic length of the initial simplex.</param>
    /// <param name="T0">The initial temperature.</param>
    /// <param name="K">The budget of moves, not negative.</param>
    /// <param name="alpha">The temperature's exponent α.</param>
    /// <param name="rng">The generator, which it copies.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="K"/> is negative.</exception>
    public SimulatedAnnealing(double lambda, double T0, int K, double alpha, TRng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentOutOfRangeException.ThrowIfNegative(K);
        scheme = Scheme.ConstantBudget;
        this.lambda = lambda;
        t0 = T0;
        epsilon = 0.0;
        this.alpha = alpha;
        k = K;
        m = 0;
        this.rng = rng.Clone();
    }

    // QuantLib's public SimulatedAnnealing::Scheme, which no member takes or returns.
    private enum Scheme
    {
        ConstantFactor,
        ConstantBudget,
    }

    /// <inheritdoc/>
    protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria ec) =>
        new Minimization(this, rng.Clone()).Minimize(P, ec);

    // One minimization's state, QuantLib's mutable members.
    private sealed class Minimization
    {
        private readonly SimulatedAnnealing<TRng> method;
        private readonly IRng rng;

        private double T;
        private Vector[] vertices = [];
        private Vector values = new(0), sum = new(0);
        private int i, ihi, ilo, j, n;
        private double fac1, fac2, yflu;
        private double yhi, ylo, ynhi, ysave, yt, ytry, yb, tt;
        private Vector pb = new(0), ptry = new(0);
        private int iteration, iterationT;

        public Minimization(SimulatedAnnealing<TRng> method, IRng rng)
        {
            this.method = method;
            this.rng = rng;
        }

        public EndCriteriaType Minimize(ProblemState P, EndCriteria ec)
        {
            int stationaryStateIterations_ = 0;
            EndCriteriaType ecType = EndCriteriaType.None;
            P.Reset();
            Vector x = P.CurrentValue.Clone();
            iteration = 0;
            n = x.Size;
            ptry = new Vector(n, 0.0);

            // build vertices

            vertices = new Vector[n + 1];
            for (i = 0; i <= n; i++)
            {
                vertices[i] = x.Clone();
            }

            for (i = 0; i < n; i++)
            {
                var direction = new Vector(n, 0.0);
                direction[i] = 1.0;
                P.Constraint.Update(vertices[i + 1], direction, method.lambda);
            }

            values = new Vector(n + 1, 0.0);
            for (i = 0; i <= n; i++)
            {
                if (!P.Constraint.Test(vertices[i]))
                {
                    values[i] = NumericLimits.QL_MAX_REAL;
                }
                else
                {
                    values[i] = P.Value(vertices[i]);
                }

                if (double.IsNaN(values[i]))
                {
                    // handle NAN
                    values[i] = NumericLimits.QL_MAX_REAL;
                }
            }

            // minimize

            T = method.t0;
            yb = NumericLimits.QL_MAX_REAL;
            pb = new Vector(n, 0.0);
            do
            {
                iterationT = iteration;
                do
                {
                    sum = new Vector(n, 0.0);
                    for (i = 0; i <= n; i++)
                    {
                        sum += vertices[i];
                    }

                    tt = -T;
                    ilo = 0;
                    ihi = 1;
                    ynhi = values[0] + (tt * Math.Log(rng.Next().Value));
                    ylo = ynhi;
                    yhi = values[1] + (tt * Math.Log(rng.Next().Value));
                    if (ylo > yhi)
                    {
                        ihi = 0;
                        ilo = 1;
                        ynhi = yhi;
                        yhi = ylo;
                        ylo = ynhi;
                    }

                    for (i = 2; i < n + 1; i++)
                    {
                        yt = values[i] + (tt * Math.Log(rng.Next().Value));
                        if (yt <= ylo)
                        {
                            ilo = i;
                            ylo = yt;
                        }

                        if (yt > yhi)
                        {
                            ynhi = yhi;
                            ihi = i;
                            yhi = yt;
                        }
                        else
                        {
                            if (yt > ynhi)
                            {
                                ynhi = yt;
                            }
                        }
                    }

                    // rtol_ = 2.0 * std::fabs(yhi_ - ylo_) /
                    //         (std::fabs(yhi_) + std::fabs(ylo_));
                    // check rtol against some ftol... // NR end criterion in f(x)

                    // GSL end criterion in x (cf. above)
                    EndCriteriaCheck stationary = ec.CheckStationaryPoint(SimplexSize(), 0.0, stationaryStateIterations_);
                    stationaryStateIterations_ = stationary.StationaryStateIterations;
                    bool stop = stationary.Stop;
                    if (stop)
                    {
                        ecType = stationary.Type;
                    }
                    else if (ec.CheckMaxIterations(iteration))
                    {
                        ecType = EndCriteriaType.MaxIterations;
                        stop = true;
                    }

                    if (stop)
                    {
                        // no matter what, we return the best ever point !
                        P.CurrentValue = pb.Clone();
                        P.FunctionValue = yb;
                        return ecType;
                    }

                    iteration += 2;
                    Amotsa(P, -1.0);
                    if (ytry <= ylo)
                    {
                        Amotsa(P, 2.0);
                    }
                    else
                    {
                        if (ytry >= ynhi)
                        {
                            ysave = yhi;
                            Amotsa(P, 0.5);
                            if (ytry >= ysave)
                            {
                                for (i = 0; i < n + 1; i++)
                                {
                                    if (i != ilo)
                                    {
                                        for (j = 0; j < n; j++)
                                        {
                                            sum[j] = 0.5 * (vertices[i][j] + vertices[ilo][j]);
                                            vertices[i][j] = sum[j];
                                        }

                                        values[i] = P.Value(sum);
                                        if (double.IsNaN(values[i]))
                                        {
                                            values[i] = NumericLimits.QL_MAX_REAL;
                                        }
                                    }
                                }

                                iteration += n;
                                for (i = 0; i < n; i++)
                                {
                                    sum[i] = 0.0;
                                }

                                for (i = 0; i <= n; i++)
                                {
                                    sum += vertices[i];
                                }
                            }
                        }
                        else
                        {
                            iteration += 1;
                        }
                    }
                }
                while (iteration < iterationT + (method.scheme == Scheme.ConstantFactor ? method.m : 1));

                switch (method.scheme)
                {
                    case Scheme.ConstantFactor:
                        T *= 1.0 - method.epsilon;
                        break;
                    case Scheme.ConstantBudget:
                        if (iteration <= method.k)
                        {
                            T = method.t0 * Math.Pow(1.0 - ((double)iteration / method.k), method.alpha);
                        }
                        else
                        {
                            T = 0.0;
                        }

                        break;
                }
            }
            while (true);
        }

        // this is taken from the simplex method
        private double SimplexSize()
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
                result += Norm2(temp);
            }

            return result / vertices.Length;
        }

        private void Amotsa(ProblemState P, double fac)
        {
            fac1 = (1.0 - fac) / n;
            fac2 = fac1 - fac;
            for (j = 0; j < n; j++)
            {
                ptry[j] = (sum[j] * fac1) - (vertices[ihi][j] * fac2);
            }

            if (!P.Constraint.Test(ptry))
            {
                ytry = NumericLimits.QL_MAX_REAL;
            }
            else
            {
                ytry = P.Value(ptry);
            }

            if (double.IsNaN(ytry))
            {
                ytry = NumericLimits.QL_MAX_REAL;
            }

            if (ytry <= yb)
            {
                yb = ytry;
                pb = ptry.Clone();
            }

            yflu = ytry - (tt * Math.Log(rng.Next().Value));
            if (yflu < yhi)
            {
                values[ihi] = ytry;
                yhi = yflu;
                for (j = 0; j < n; j++)
                {
                    sum[j] += ptry[j] - vertices[ihi][j];
                    vertices[ihi][j] = ptry[j];
                }
            }

            ytry = yflu;
        }
    }
}
