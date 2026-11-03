// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/randomdefaultmodel.hpp ql/experimental/credit/randomdefaultmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>Random default times on a one-factor Gaussian copula.</summary>
/// <remarks>
/// <para>
/// Each sequence draws the factor and one variable per name from QuantLib's pseudo-random Gaussian sequences (MT19937
/// inverted), y = a·m + √(1 − a²)·zⱼ with a² the copula's correlation, and solves the name's default time for the
/// probability Φ(y): Brent's solver on [0, tmax] from tmax/2, bisection where it fails.
/// </para>
/// <para>
/// The seed is explicit, in [1, 4294967295]: QuantLib reads 0 as a seed drawn from the clock, and its <c>long</c> seed
/// is a 32-bit value on MSVC.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class GaussianRandomDefaultModel : RandomDefaultModel
{
    private static readonly CumulativeNormalDistribution Phi = new();
    private readonly OneFactorCopula copula;
    private readonly double accuracy;
    private readonly long seed;
    private InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal> rsg;

    /// <summary>Initializes a new instance of the <see cref="GaussianRandomDefaultModel"/> class.</summary>
    /// <param name="pool">The pool.</param>
    /// <param name="defaultKeys">The names' default keys, one per name.</param>
    /// <param name="copula">The one-factor copula, whose correlation the model reads.</param>
    /// <param name="accuracy">The default time solver's accuracy.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The keys don't number the pool's names.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> lies outside [1, 4294967295].</exception>
    public GaussianRandomDefaultModel(
        Pool pool, IReadOnlyList<DefaultProbKey> defaultKeys, OneFactorCopula copula, double accuracy, long seed)
        : base(pool, defaultKeys)
    {
        ArgumentNullException.ThrowIfNull(copula);
        Seeds.Check(seed);
        this.copula = copula;
        this.accuracy = accuracy;
        this.seed = seed;
        rsg = PseudoRandom.MakeSequenceGenerator(pool.Size + 1, seed);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        int dim = Pool.Size + 1;
        rsg = PseudoRandom.MakeSequenceGenerator(dim, seed);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">A curve refuses <paramref name="tmax"/> past its maximum time.</exception>
    public override void NextSequence(double tmax = double.MaxValue)
    {
        ReadOnlySpan<double> values = rsg.NextSequence().Value.Span;
        double a = Math.Sqrt(copula.Correlation);
        Pool pool = Pool;
        for (int j = 0; j < pool.Size; j++)
        {
            string name = pool.Names[j];
            DefaultProbabilityTermStructure dts = pool.Get(name).DefaultProbability(DefaultKeys[j]);

            double y = (a * values[0]) + (Math.Sqrt(1 - (a * a)) * values[j + 1]);
            double p = Phi.Evaluate(y);

            if (dts.DefaultProbability(tmax) < p)
            {
                pool = pool.SetTime(name, tmax + 1);
            }
            else
            {
                // we know there is a zero of f(t) = dts->defaultProbability(t) - p in [0, tmax]
                double Root(double t) => DefaultTimeRoot(dts, p, t);
                double time;
                try
                {
                    // try bracketing the root and find it with Brent
                    time = new Brent().SetLowerBound(0.0).SetUpperBound(tmax).Solve(Root, accuracy, tmax / 2.0, 1.0);
                }
                catch (NanoQLException)
                {
                    // if Brent fails, use Bisection, this is guaranteed to find the root
                    time = new Bisection().Solve(Root, accuracy, tmax / 2.0, 0.0, tmax);
                }

                pool = pool.SetTime(name, time);
            }
        }

        Pool = pool;
    }

    // Utility for the numerical solver
    private static double DefaultTimeRoot(DefaultProbabilityTermStructure dts, double pd, double t)
    {
        if (!(t >= 0.0))
        {
            throw new NanoQLException(string.Create(
                CultureInfo.InvariantCulture, $"GaussianRandomDefaultModel: internal error, t < 0 ({t}) during root searching."));
        }

        return dts.DefaultProbability(t, true) - pd;
    }
}
