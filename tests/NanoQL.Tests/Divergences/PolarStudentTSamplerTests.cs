// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The polar Student t factor sampler draws its factors from one uniform stream in turn, each scaled to unit variance:
/// QuantLib's gives each t generator its own copy of one uniform generator, so the systemic draw k equals the
/// idiosyncratic stream's draw k, and leaves its draws raw, of variance ν/(ν − 2) (<c>latentmodel.hpp:779-794</c>,
/// <c>polarstudenttrng.hpp:86</c>). QuantLib's values are recorded in the test (ν 5, 5, weight √0.3, 200,000 paths at
/// seed 42); the checks, which QuantLib's sampler fails, are each factor's sample variance within three standard errors
/// of 1, the simulated default frequency against the model's probability and the inversion sampler's, and the systemic
/// draws against the idiosyncratic stream's.
/// </summary>
public class PolarStudentTSamplerTests
{
    private const int Paths = 200000;

    [Fact]
    public void TheFactorsAreOfUnitVariance()
    {
        // QuantLib: var(M) 1.6732, var(Z1) 1.6663
        (double varM, double varZ, _) = Simulate(Polar());

        // the variance's standard error for a t of order 5 is about sqrt(m4 - 1) / sqrt(n), m4 = 9 here: 0.0063
        Assert.Equal(1.0, varM, 3 * Math.Sqrt(8.0 / Paths));
        Assert.Equal(1.0, varZ, 3 * Math.Sqrt(8.0 / Paths));
    }

    [Fact]
    public void TheDefaultFrequencyIsTheModels()
    {
        // QuantLib: P(Y1 <= F^-1(0.01)) 0.02654 with the polar-T sampler, 0.01012 with the inversion sampler (se 0.00022)
        (_, _, double frequency) = Simulate(Polar());
        (_, _, double inversion) = Simulate(new LatentModelFactorSampler<TCopulaPolicy, TCopulaPolicyInitTraits, RandomSequenceGenerator<MersenneTwisterUniformRng>>(
            Policy(), RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(4, 42)));

        double se = Math.Sqrt(0.01 * 0.99 / Paths);
        Assert.Equal(0.01, frequency, 3 * se);
        Assert.Equal(inversion, frequency, 3 * Math.Sqrt(2) * se);
    }

    [Fact]
    public void TheSystemicDrawsArentTheIdiosyncraticStreams()
    {
        // QuantLib: the first draw's M equals its Z1, 1.3377929950 both, and its later systemic draws its Z1 stream's
        PolarStudentTFactorSampler<MersenneTwisterUniformRng> s = Polar();
        double[] first = s.NextSequence().Value.ToArray();

        Assert.NotEqual(first[0], first[1]);
    }

    private static TCopulaPolicy Policy() =>
        TCopulaPolicy.Create([.. Enumerable.Repeat(ImmutableArray.Create(Math.Sqrt(0.3)), 3)], new TCopulaPolicyInitTraits([5, 5]));

    private static PolarStudentTFactorSampler<MersenneTwisterUniformRng> Polar() =>
        PolarStudentTFactorSampler.Create<MersenneTwisterUniformRng>(Policy(), 42);

    private static (double VarM, double VarZ, double Frequency) Simulate(IRsg sampler)
    {
        double beta = Math.Sqrt(0.3);
        double threshold = Policy().InverseCumulativeY(0.01, 0);
        double sm = 0, sm2 = 0, sz = 0, sz2 = 0;
        int defaults = 0;
        for (int i = 0; i < Paths; i++)
        {
            ReadOnlySpan<double> v = sampler.NextSequence().Value.Span;
            sm += v[0];
            sm2 += v[0] * v[0];
            sz += v[1];
            sz2 += v[1] * v[1];
            defaults += (beta * v[0]) + (Math.Sqrt(1 - 0.3) * v[1]) <= threshold ? 1 : 0;
        }

        return ((sm2 / Paths) - ((sm / Paths) * (sm / Paths)), (sz2 / Paths) - ((sz / Paths) * (sz / Paths)), (double)defaults / Paths);
    }
}
