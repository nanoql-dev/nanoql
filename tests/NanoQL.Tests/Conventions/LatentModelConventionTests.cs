// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The latent model framework's own rules (the S-xcr plan's XR2, XR4 (a), XR8 (xxx)): the copula policies' zero values
/// refused, the samplers' and the polar generator's seeds S2R4's and their generators copied, the models and
/// integrations of no dimension or above 15 refused, the weights copied.
/// </summary>
public class LatentModelConventionTests
{
    [Fact]
    public void ThePoliciesZeroValuesAreRefused()
    {
        GaussianCopulaPolicy g = default;
        TCopulaPolicy t = default;

        Assert.Throws<InvalidOperationException>(() => g.NumFactors);
        Assert.Throws<InvalidOperationException>(() => g.CumulativeZ(0.0));
        Assert.Throws<InvalidOperationException>(() => g.AllFactorCumulInverter([0.5]));
        Assert.Throws<InvalidOperationException>(() => t.NumFactors);
        Assert.Throws<InvalidOperationException>(() => t.InverseCumulativeZ(0.3));
        Assert.Throws<InvalidOperationException>(() => t.VarianceFactors);
        Assert.Throws<InvalidOperationException>(() => t.GetInitTraits());
        Assert.Throws<ArgumentException>(() => TCopulaPolicy.Create([[0.3]], default));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(4294967296L)]
    public void TheSeedsAreS2R4s(long seed)
    {
        GaussianCopulaPolicy g = GaussianCopulaPolicy.Create([[0.3], [0.5]]);
        TCopulaPolicy t = TCopulaPolicy.Create([[0.3], [0.5]], new TCopulaPolicyInitTraits([5, 5]));

        Assert.Throws<ArgumentOutOfRangeException>(() => PolarStudentTRng.Create<MersenneTwisterUniformRng>(5.0, seed));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoxMullerFactorSampler.Create<MersenneTwisterUniformRng>(g, seed));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarStudentTFactorSampler.Create<MersenneTwisterUniformRng>(t, seed));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LatentModelFactorSampler.Create<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits, SobolRsg>(g, seed));

        // QuantLib's seeds are 32-bit, whatever the generator: xoshiro's own rule takes any seed but 0
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarStudentTRng.Create<Xoshiro256StarStarUniformRng>(5.0, seed));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoxMullerFactorSampler.Create<Xoshiro256StarStarUniformRng>(g, seed));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolarStudentTFactorSampler.Create<Xoshiro256StarStarUniformRng>(t, seed));
    }

    [Fact]
    public void ASamplerCopiesTheGeneratorItIsGiven()
    {
        GaussianCopulaPolicy g = GaussianCopulaPolicy.Create([[0.3], [0.5]]);
        var urng = new MersenneTwisterUniformRng(42);
        var rsg = RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(3, 42);

        var polar = new PolarStudentTRng<MersenneTwisterUniformRng>(5.0, urng);
        var sampler = new LatentModelFactorSampler<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits, RandomSequenceGenerator<MersenneTwisterUniformRng>>(g, rsg);
        double first = polar.Next().Value;
        double[] draw = sampler.NextSequence().Value.ToArray();

        Assert.Equal(new MersenneTwisterUniformRng(42).Next().Value, urng.Next().Value);
        Assert.Equal(RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(3, 42).NextSequence().Value.ToArray(), rsg.NextSequence().Value.ToArray());
        Assert.Equal(first, PolarStudentTRng.Create<MersenneTwisterUniformRng>(5.0, 42).Next().Value);
        Assert.Equal(3, draw.Length);
        Assert.Throws<ArgumentException>(() =>
            new LatentModelFactorSampler<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits, RandomSequenceGenerator<MersenneTwisterUniformRng>>(
                g, RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(2, 42)));
    }

    [Fact]
    public void ModelsAndIntegrationsOfNoDimensionAreRefused()
    {
        Assert.Throws<ArgumentException>(() => new LatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(Array.Empty<double[]>()));
        Assert.Throws<ArgumentException>(() => new LatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>([[]]));
        Assert.Throws<ArgumentException>(() => LatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>.FromCorrelation(0.3, 0));
        Assert.Throws<ArgumentException>(() => new MultidimIntegral([]));
        Assert.Throws<ArgumentException>(() => new MultidimIntegral(Enumerable.Range(0, 16).Select(_ => (Integrator)new TrapezoidIntegral<Default>(1e-4, 10))));
        Assert.Equal("dimension", Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianQuadMultidimIntegrator(0, 10)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianQuadMultidimIntegrator(16, 10));
        Assert.Throws<ArgumentException>(() => new MultidimIntegral([new TrapezoidIntegral<Default>(1e-4, 10)]).Integrate(_ => 1.0, [0.0, 0.0], [1.0, 1.0]));
        Assert.Equal("No vector integration provided", Assert.Throws<NanoQLException>(() =>
            new MultidimLMIntegration([new TrapezoidIntegral<Default>(1e-4, 10)], -1.0, 1.0).IntegrateV(_ => [1.0])).Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianDefProbLM(0.3, 2, (LatentModelIntegrationType)7));
    }

    [Fact]
    public void ABaseLatentModelHasNoIntegration()
    {
        var lm = new LatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(0.3, 2);

        Assert.Equal("Integration non implemented in Latent model.", Assert.Throws<NanoQLException>(() => lm.IntegratedExpectedValue(_ => 1.0)).Message);
    }

    [Fact]
    public void TheGaussianPolicyAndQuadratureReportTheirParameters()
    {
        GaussianCopulaPolicy g = GaussianCopulaPolicy.Create([[0.3], [0.5]]);

        Assert.Equal(default, g.GetInitTraits());
        Assert.Equal(7, new GaussianQuadLMIntegration(2, 7).Order);
    }

    [Fact]
    public void ThePolarSamplerNeedsThePolicysVariables()
    {
        // a policy without variables has one factor for orders [5, 5] and two t generators: QuantLib's writes past its
        // sequence
        TCopulaPolicy bare = TCopulaPolicy.Create(ImmutableArray<ImmutableArray<double>>.Empty, new TCopulaPolicyInitTraits([5, 5]));

        Assert.Equal(
            "copula",
            Assert.Throws<ArgumentException>(() => PolarStudentTFactorSampler.Create<MersenneTwisterUniformRng>(bare, 42)).ParamName);
    }

    [Fact]
    public void TheModelCopiesItsWeights()
    {
        double[][] w = [[0.3], [0.5]];
        var lm = new LatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(w);

        w[0][0] = 0.9;

        Assert.Equal(0.3, lm.FactorWeights[0][0]);
        Assert.Equal(Math.Sqrt(1 - (0.3 * 0.3)), lm.IdiosyncFctrs[0]);
        Assert.Equal(ImmutableArray.Create(0.5), lm.FactorWeights[1]);
    }
}
