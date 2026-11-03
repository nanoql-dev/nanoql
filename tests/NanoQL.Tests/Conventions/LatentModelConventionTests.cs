// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The latent model framework's own rules: the copula policies' zero values refused, the samplers' and the polar
/// generator's seeds explicit and their generators copied, the models and integrations of no dimension or above 15
/// refused, the weights copied; a T density given more values than the systemic factors refused, as QuantLib's extra
/// safety check refuses them, and a latent variable outside the model or past its sample, a name, variable or factor
/// past the model's or its bound basket's, or fewer factor values than a name's weights, where QuantLib reads past.
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
    public void ADensityOrALatentValueReadPastItsFactorsIsRefused()
    {
        // The T density takes the systemic factors' values: as many as the distributions are refused, the upper half of
        // QuantLib's extra safety check, which its default build skips, reading the idiosyncratic factor's.
        TCopulaPolicy t = TCopulaPolicy.Create([[0.3]], new TCopulaPolicyInitTraits([5, 5]));
        ArgumentException e = Assert.Throws<ArgumentException>(() => t.Density([0.1, 0.2]));
        Assert.Equal("m", e.ParamName);
        Assert.StartsWith("2 factor values given for 1 systemic factors", e.Message, StringComparison.Ordinal);
        Assert.True(t.Density([0.1]) > 0.0);

        // A variable the model has, and a sample reaching its idiosyncratic factor.
        var lm = new LatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(0.3, 2);
        e = Assert.Throws<ArgumentException>(() => lm.LatentVarValue([0.1, 0.2], 1));
        Assert.Equal("allFactors", e.ParamName);
        Assert.StartsWith("2 factor values don't reach variable 1's", e.Message, StringComparison.Ordinal);
        Assert.Equal("iVar", Assert.Throws<ArgumentOutOfRangeException>(() => lm.LatentVarValue([0.1, 0.2, 0.3], 2)).ParamName);
        Assert.Equal("iVar", Assert.Throws<ArgumentOutOfRangeException>(() => lm.LatentVarValue([0.1, 0.2, 0.3], -1)).ParamName);
        Assert.Equal((0.3 * 0.1) + (Math.Sqrt(1.0 - (0.3 * 0.3)) * 0.3), lm.LatentVarValue([0.1, 0.2, 0.3], 1), 14);
    }

    [Fact]
    public void ANameVariableOrFactorPastTheModelsIsRefused()
    {
        // Each index into the model's weights, recoveries, quotes or the T policy's distributions, and the factors'
        // values a name's weights read, which QuantLib reads unchecked past their ends.
        var lm = new LatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(0.3, 2);
        var defaults = new GaussianDefProbLM(0.3, 2, LatentModelIntegrationType.GaussianQuadrature);
        double[][] w = [.. Enumerable.Repeat(new[] { 0.5 }, 6)];
        var spot = new GaussianSpotLossLM(w, [0.4, 0.4, 0.4], 1.0, LatentModelIntegrationType.GaussianQuadrature);
        var constant = new GaussianConstantLossLM(0.3, [0.4, 0.4, 0.4], LatentModelIntegrationType.GaussianQuadrature, 3);
        var random = new GaussianRandomDefaultLM(constant, 100, 42);
        var lhp = new GaussianLHPLossModel(0.3, [0.4, 0.4, 0.4]);
        TCopulaPolicy t = TCopulaPolicy.Create([[0.3]], new TCopulaPolicyInitTraits([5, 5]));
        var tModel = new TDefProbLM(0.3, 3, LatentModelIntegrationType.GaussianQuadrature, new TCopulaPolicyInitTraits([5, 5]));
        var today = new Date(15, Month.October, 2026);
        var key = new DefaultProbKey();
        foreach ((string param, string message, Action call) in new (string, string, Action)[]
                 {
                     ("iVar1", "iVar1 ('2') must be less than '2'.", () => lm.LatentVariableCorrel(2, 0)),
                     ("iVar2", "iVar2 ('-1') must be a non-negative value.", () => lm.LatentVariableCorrel(0, -1)),
                     ("iName", "iName ('2') must be less than '2'.", () => defaults.ConditionalDefaultProbabilityInvP(0.1, 2, [0.1])),
                     ("iName", "iName ('6') must be less than '6'.", () => spot.ConditionalDefaultProbabilityInvP(0.1, 6, [0.1])),
                     ("iName", "iName ('2') must be less than '2'.", () => defaults.ConditionalDefaultProbability(0.1, 2, [0.1])),
                     ("iName", "iName ('6') must be less than '6'.", () => spot.ConditionalDefaultProbability(0.1, 6, [0.1])),
                     ("iName", "iName ('3') must be less than '3'.", () => spot.ExpCondRecoveryP(0.1, 3, [0.1])),
                     ("iName", "iName ('3') must be less than '3'.", () => spot.ExpCondRecoveryInvPinvRR(-1.0, -0.2, 3, [0.1])),
                     ("iName", "iName ('3') must be less than '3'.", () => constant.ConditionalRecovery(today, 3, [0.1])),
                     ("iName", "iName ('3') must be less than '3'.", () => constant.ConditionalRecovery(0.1, 3, [0.1])),
                     ("iName", "iName ('-1') must be a non-negative value.", () => constant.ConditionalRecoveryInvP(-1.0, -1, [0.1])),
                     ("iName", "iName ('3') must be less than '3'.", () => constant.ConditionalRecovery(0.1, 3, today)),
                     ("iName", "iName ('3') must be less than '3'.", () => constant.ExpectedRecovery(today, 3, key)),
                     ("iName", "iName ('3') must be less than '3'.", () => random.ExpectedRecovery(today, 3, key)),
                     ("iName", "iName ('3') must be less than '3'.", () => lhp.ExpectedRecovery(today, 3, key)),
                     ("iVariable", "iVariable ('1') must be less than '1'.", () => t.CumulativeY(0.1, 1)),
                     ("iVariable", "iVariable ('1') must be less than '1'.", () => t.InverseCumulativeY(0.5, 1)),
                     ("iFactor", "iFactor ('2') must be less than '2'.", () => t.InverseCumulativeDensity(0.5, 2)),
                     ("iVariable", "iVariable ('3') must be less than '3'.", () => tModel.CumulativeY(0.1, 3)),
                 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(call);
            Assert.Equal(param, e.ParamName);
            Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
        }

        foreach ((string param, string message, Action call) in new (string, string, Action)[]
                 {
                     ("m", "0 factor values given for 1 factors", () => defaults.ConditionalDefaultProbabilityInvP(0.1, 0, [])),
                     ("m", "0 factor values given for 1 factors", () => spot.ConditionalDefaultProbabilityInvP(0.1, 0, [])),
                     ("mktFactors", "0 factor values given for 1 factors", () => spot.ExpCondRecoveryInvPinvRR(-1.0, -0.2, 0, [])),
                     ("mktFactors", "0 factor values given for 1 factors", () => spot.ExpCondRecoveryP(0.1, 0, [])),
                     ("m", "0 factor values given for 1 factors", () => defaults.ConditionalDefaultProbability(0.1, 0, [])),
                     ("probs", "0 probabilities given for 1 systemic factors", () => t.AllFactorCumulInverter([])),
                 })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(call);
            Assert.Equal(param, e.ParamName);
            Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
        }

        // Each index and factor within the model still computes.
        Assert.Equal((0.4, 0.4, 0.4, 0.4), (constant.ConditionalRecovery(0.1, 2, today), constant.ExpectedRecovery(today, 2, key),
            random.ExpectedRecovery(today, 2, key), lhp.ExpectedRecovery(today, 2, key)));
        Assert.InRange(defaults.ConditionalDefaultProbabilityInvP(-1.0, 1, [0.1]), 0.0, 1.0);
        Assert.InRange(spot.ExpCondRecoveryP(0.1, 2, [0.1]), 0.0, 1.0);
        Assert.InRange(t.CumulativeY(0.1, 0), 0.0, 1.0);
        Assert.InRange(tModel.CumulativeY(0.1, 2), 0.0, 1.0);
        Assert.True(double.IsFinite(t.InverseCumulativeDensity(0.5, 1)));
        Assert.Equal(2, t.AllFactorCumulInverter([0.5, 0.5]).Length);
        Assert.Equal(0.3 * 0.3, lm.LatentVariableCorrel(0, 1), 15);
    }

    [Fact]
    public void ANamePastTheBoundBasketsIsRefused()
    {
        // The bound models read the basket's names, keys and curves by a caller's name, unchecked in QuantLib; the
        // sweep binds no model to a basket, so these cases are the reads' only check.
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.01), Flat(Today, 0.02), Flat(Today, 0.03)];
        var basket = new Basket(Today, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves), Context);
        var defaults = new GaussianDefProbLM(0.3, 3, LatentModelIntegrationType.GaussianQuadrature).ResetBasket(basket);
        double[][] w = [.. Enumerable.Repeat(new[] { 0.5 }, 6)];
        var spot = new GaussianSpotLossLM(w, [0.4, 0.4, 0.4], 1.0, LatentModelIntegrationType.GaussianQuadrature).ResetBasket(basket);
        Date d = Today + 365;
        foreach ((string param, int i, Action call) in new (string, int, Action)[]
                 {
                     ("iName", 3, () => defaults.ProbOfDefault(3, d)),
                     ("iNamei", 3, () => defaults.DefaultCorrelation(d, 3, 0)),
                     ("iNamej", -1, () => defaults.DefaultCorrelation(d, 0, -1)),
                     ("iName", 3, () => spot.ConditionalDefaultProbability(d, 3, [0.1])),
                     ("iName", 3, () => spot.ExpCondRecovery(d, 3, [0.1])),
                     ("iName", -1, () => spot.ConditionalRecovery(0.1, -1, d)),
                 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(call);
            Assert.Equal(param, e.ParamName);
            Assert.StartsWith(
                i < 0 ? $"{param} ('{i}') must be a non-negative value." : $"{param} ('{i}') must be less than '3'.",
                e.Message,
                StringComparison.Ordinal);
        }

        Assert.InRange(defaults.ProbOfDefault(2, d), 0.0, 1.0);
        Assert.InRange(defaults.DefaultCorrelation(d, 0, 2), -1.0, 1.0);
        Assert.InRange(spot.ConditionalDefaultProbability(d, 2, [0.1]), 0.0, 1.0);
        Assert.InRange(spot.ExpCondRecovery(d, 2, [0.1]), 0.0, 1.0);
        Assert.True(double.IsFinite(spot.ConditionalRecovery(0.1, 2, d)));
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
