// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The analytic loss models against each other on one basket: the recursive model exact, the binomial and the
/// inhomogeneous pool within their approximations; and the base-correlation model as its LHP sub-models at the
/// surface's nodes, exactly.
/// </summary>
public class AnalyticLossModelTwoRouteTests
{
    private static readonly string[] Names = [.. Enumerable.Range(0, 20).Select(i => $"N{i}")];

    [Theory]
    [InlineData(0.0, 0.03)]
    [InlineData(0.03, 0.07)]
    [InlineData(0.07, 0.15)]
    public void TheApproximationsAreNearTheRecursiveModel(double a, double d)
    {
        double exact = Basket(a, d).SetLossModel(new RecursiveGaussLossModel(Latent())).ExpectedTrancheLoss(D5);
        double binomial = Basket(a, d).SetLossModel(new GaussianBinomialLossModel(Latent())).ExpectedTrancheLoss(D5);
        double pool = Basket(a, d).SetLossModel(new IHGaussPoolLossModel(Latent(), 200)).ExpectedTrancheLoss(D5);

        // the adjusted binomial matches two moments; the pool's buckets read left edges, about 3% high
        Assert.Equal(exact, binomial, 0.05 * exact);
        Assert.Equal(exact, pool, 0.05 * exact);
    }

    [Theory]
    [InlineData(0.03, 0.07)]
    [InlineData(0.07, 0.15)]
    public void TheBaseCorrelationModelIsItsLhpSubModelsAtTheNodes(double a, double d)
    {
        // a surface flat in time whose loss levels include the tranche's ends: the model is the detachment's equity
        // tranche at its correlation less the attachment's at its own
        double[] levels = [0.03, 0.07, 0.15];
        double[] correls = [0.2, 0.3, 0.45];
        BaseCorrelationTermStructure<Bilinear> surface = BaseCorrelationTermStructure<Bilinear>.Create(
            0, new TARGET(), BusinessDayConvention.Following, [new Period(1, TimeUnit.Years), new Period(10, TimeUnit.Years)],
            levels, [.. correls.Select(c => new[] { c, c })], new Actual365Fixed(), Context);
        double[] recoveries = [.. Enumerable.Repeat(0.4, 20)];

        double model = Basket(a, d).SetLossModel(new GaussianLHPFlatBCLM(surface, recoveries)).ExpectedTrancheLoss(D5);
        double Equity(double level) => Basket(0.0, level)
            .SetLossModel(new GaussianLHPLossModel(correls[Array.IndexOf(levels, level)], recoveries)).ExpectedTrancheLoss(D5);
        Assert.Equal(Equity(d) - Equity(a), model, 1e-13);
    }

    private static Basket Basket(double a, double d)
    {
        DefaultProbabilityTermStructure[] curves = [.. Enumerable.Range(0, 20).Select(i => (DefaultProbabilityTermStructure)Flat(Today, 0.01 + (0.001 * i)))];
        return new Basket(Today, Names, Enumerable.Repeat(10.0, 20), PoolOf(Names, curves), Context, a, d);
    }

    private static GaussianConstantLossLM Latent() =>
        new(0.3, [.. Enumerable.Repeat(0.4, 20)], LatentModelIntegrationType.GaussianQuadrature, 20);
}
