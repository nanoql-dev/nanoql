// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/catrisk.hpp ql/experimental/catbonds/catrisk.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The simulation of a <see cref="BetaRisk"/>: each path's events arrive as a Poisson process over the period, each
/// event's loss the maximum loss times a beta draw, X / (X + Y) of two gamma draws.
/// </summary>
/// <remarks>
/// <para>
/// <b>The draws</b> are those of QuantLib built with MSVC 14.44, its oracle's standard library (the S-xcr plan's XR4
/// (c)): a 32-bit Mersenne Twister seeded with QuantLib's constant 5489, no parameter, as <c>std::mt19937</c>'s
/// default; each uniform from two of its integers as MSVC's <c>generate_canonical</c> forms it, the first's top 21 bits
/// and the second's 32, (g₀ &gt;&gt; 11) + (g₁ &lt;&lt; 21) times 2⁻⁵³, truncating where the standard's form rounds; the
/// exponential as −log(1 − u)/λ; and the gamma by Knuth's published algorithms (TAOCP vol. 2, §3.4.1) in the order
/// MSVC draws them: Ahrens-Dieter's GS below α = 1, the exponential at α = 1, a product of uniforms for an integer α
/// below 20, and Ahrens' tangent method otherwise. They are written from those algorithms and this description of
/// MSVC's behaviour; the standard leaves a distribution's algorithm to the implementation.
/// </para>
/// <para>
/// <b>The event times</b> are the inter-arrival times cumulated, so the count is Poisson's and a path's dates ascend
/// (XR8 (xlii)): QuantLib's draws each event's time afresh, so its count is geometric, a mean of e^(λT) − 1 for λT
/// (3.48 for 1.5 at a two-year return period over three years), and its dates come unordered, which the notional path
/// reads as ascending. A path of one event or none is QuantLib's bit for bit up to the first path where QuantLib's next
/// fresh draw falls inside the period and the cumulated time doesn't; the streams part there
/// (<c>Divergences/BetaRiskTests.cs</c>).
/// </para>
/// <para>A working type (XR5's form): each simulation owns its generator, stepping it in place.</para>
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class BetaRiskSimulation : CatSimulation
{
    // std::mt19937's default seed, QuantLib's constant
    private const long Seed = 5489;

    private readonly double maxLoss;
    private readonly int dayCount;
    private readonly double yearFraction;
    private readonly double lambda;
    private readonly double alpha;
    private readonly double beta;
    private readonly MersenneTwisterUniformRng rng = new(Seed);

    /// <summary>Initializes a new instance of the <see cref="BetaRiskSimulation"/> class.</summary>
    /// <param name="start">The period's start.</param>
    /// <param name="end">The period's end.</param>
    /// <param name="maxLoss">The maximum loss.</param>
    /// <param name="lambda">The events' rate, per year.</param>
    /// <param name="alpha">The beta distribution's first parameter.</param>
    /// <param name="beta">The beta distribution's second parameter.</param>
    public BetaRiskSimulation(Date start, Date end, double maxLoss, double lambda, double alpha, double beta)
        : base(start, end)
    {
        this.maxLoss = maxLoss;
        this.lambda = lambda;
        this.alpha = alpha;
        this.beta = beta;
        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        dayCount = dayCounter.DayCount(start, end);
        yearFraction = dayCounter.YearFraction(start, end);
    }

    /// <summary>The next event's loss: the maximum loss times X / (X + Y), X and Y gamma draws of the beta
    /// distribution's parameters.</summary>
    /// <returns>The loss.</returns>
    public double GenerateBeta()
    {
        double x = Gamma(alpha);
        double y = Gamma(beta);
        return x * maxLoss / (x + y);
    }

    /// <inheritdoc/>
    public override bool NextPath(List<KeyValuePair<Date, double>> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        path.Clear();
        double eventFraction = Exponential(lambda);
        while (eventFraction <= yearFraction)
        {
            int days = (int)Math.Round(eventFraction * dayCount / yearFraction, MidpointRounding.AwayFromZero);
            Date eventDate = Start + days;
            if (eventDate <= End)
            {
                path.Add(new KeyValuePair<Date, double>(eventDate, GenerateBeta()));
            }
            else
            {
                break;
            }

            // the inter-arrival times cumulated, where QuantLib's draws each event's time afresh (XR8 (xlii))
            eventFraction += Exponential(lambda);
        }

        return true;
    }

    // MSVC's generate_canonical<double, 53>: the first integer's top 21 bits and the second's 32, times 2^-53.
    private double Canonical()
    {
        ulong g0 = (ulong)rng.NextInt32();
        ulong g1 = (ulong)rng.NextInt32();
        return ((g0 >> 11) + (g1 << 21)) * (1.0 / 9007199254740992.0);
    }

    // The exponential of a rate, -log(1 - u) / lambda.
    private double Exponential(double rate) => -Math.Log(1.0 - Canonical()) / rate;

    // The gamma of a shape and unit scale, by Knuth's algorithms in MSVC's order of draws.
    private double Gamma(double shape)
    {
        if (shape < 1.0)
        {
            // Ahrens-Dieter's GS
            double px = Math.E / (shape + Math.E);
            while (true)
            {
                double u = Canonical();
                double v;
                do
                {
                    v = Canonical();
                }
                while (v == 0.0);

                double x, q;
                if (u < px)
                {
                    x = Math.Pow(v, 1.0 / shape);
                    q = Math.Exp(-x);
                }
                else
                {
                    x = 1.0 - Math.Log(v);
                    q = Math.Pow(x, shape - 1.0);
                }

                if (Canonical() < q)
                {
                    return x;
                }
            }
        }

        if (shape == 1.0)
        {
            return Exponential(1.0);
        }

        if (shape < 20.0 && shape == Math.Floor(shape))
        {
            // a product of uniforms
            double y = Canonical();
            int count = (int)shape;
            while (--count != 0)
            {
                double u;
                do
                {
                    u = Canonical();
                }
                while (u == 0.0);

                y *= u;
            }

            return -Math.Log(y);
        }

        // Ahrens' tangent method
        double sq = Math.Sqrt((2.0 * shape) - 1.0);
        while (true)
        {
            double y = Math.Tan(Math.PI * Canonical());
            double x = (sq * y) + shape - 1.0;
            if (x > 0.0 && Canonical() <= (1.0 + (y * y)) * Math.Exp(((shape - 1.0) * Math.Log(x / (shape - 1.0))) - (sq * y)))
            {
                return x;
            }
        }
    }
}
