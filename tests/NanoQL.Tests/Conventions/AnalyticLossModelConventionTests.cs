// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The analytic loss models' refusals and kept designs: the recursive and binomial models' reads before their
/// distribution's start, a recursive model of no loss, a base correlation outside [0, 1), a copula's negative
/// correlation, the saddle-point expansion's unresolvable loss, the surface's own checks, and a copula at correlation
/// 1, the comonotonic step.
/// </summary>
public class AnalyticLossModelConventionTests
{
    private static readonly string[] Names = ["N0", "N1", "N2", "N3", "N4"];

    [Fact]
    public void ACopulasNegativeCorrelationIsRefused()
    {
        // QuantLib accepts [-1, 1] and takes the square root: "conditional probability -nan(ind)out of range"
        Assert.Throws<ArgumentOutOfRangeException>(() => new OneFactorGaussianCopula(-0.3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OneFactorStudentCopula(-0.1, 5, 5));
    }

    [Fact]
    public void ACopulaAtCorrelationOneIsTheComonotonicStep()
    {
        // QuantLib's continuous limit: 0.0545030778829 at 1 against 0.0545030775379 at 0.9999, the Euler grid's 0.0545
        // for 0.05 its own \todo; the moments' check refuses at both
        Assert.Equal(0.0545030778829, new OneFactorGaussianCopula(1.0).Integral(0.05), 1e-12);
        Assert.Equal(0.0545030775379, new OneFactorGaussianCopula(0.9999).Integral(0.05), 1e-12);
        Assert.ThrowsAny<Exception>(() => new OneFactorGaussianCopula(1.0).CheckMoments(1e-4));
        Assert.ThrowsAny<Exception>(() => new OneFactorGaussianCopula(0.9999).CheckMoments(1e-4));
    }

    [Fact]
    public void ARecursiveModelOfNoLossIsRefused()
    {
        // every recovery 1: QuantLib's loss unit reads the minimum of an empty list
        Assert.Throws<NanoQLException>(() => Basket().SetLossModel(new RecursiveGaussLossModel(Latent(1.0))));
    }

    [Fact]
    public void TheRecursivePercentileBeforeTheDistributionsStartIsRefused()
    {
        // the first point's cumulative is above 0.5 and below 1: QuantLib reads before the map's start
        Basket basket = Basket().SetLossModel(new RecursiveGaussLossModel(Latent(0.4)));
        Assert.True(basket.LossDistribution(D5).Values.First() > 0.5);
        Assert.Throws<NanoQLException>(() => basket.Percentile(D5, 0.5));
    }

    [Fact]
    public void TheShortfallsAtTheDistributionsStartAreRefused()
    {
        Basket binomial = Basket().SetLossModel(new GaussianBinomialLossModel(Latent(0.4)));
        Basket recursive = Basket().SetLossModel(new RecursiveGaussLossModel(Latent(0.4)));
        Assert.Throws<NanoQLException>(() => binomial.ExpectedShortfall(D5, 0.5));
        Assert.Throws<NanoQLException>(() => recursive.ExpectedShortfall(D5, 0.5));
    }

    [Fact]
    public void ABaseCorrelationOutsideItsRangeIsRefused()
    {
        // the surface's bilinear extrapolation below its first level reads -0.012 at an attachment of 1%; QuantLib's
        // LHP base model then reads NaN
        BaseCorrelationTermStructure<Bilinear> surface = BaseCorrelationTermStructure<Bilinear>.Create(
            0, new TARGET(), BusinessDayConvention.Following, [new Period(1, TimeUnit.Years), new Period(10, TimeUnit.Years)],
            [0.05, 0.6], [[0.05, 0.05], [0.9, 0.9]], new Actual365Fixed(), Context);
        Assert.True(surface.Correlation(D5, 0.01) < 0.0);
        Basket basket = Basket(0.01, 0.03).SetLossModel(new GaussianLHPFlatBCLM(surface, [.. Enumerable.Repeat(0.4, 5)]));
        NanoQLException e = Assert.Throws<NanoQLException>(() => basket.ExpectedTrancheLoss(D5));
        Assert.Contains("loss level 0.01", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSurfaceChecksItsLevelsAndRows()
    {
        Period[] tenors = [new Period(1, TimeUnit.Years), new Period(5, TimeUnit.Years)];
        BaseCorrelationTermStructure<Bilinear> Make(double[] levels, double[][] rows) =>
            BaseCorrelationTermStructure<Bilinear>.Create(0, new TARGET(), BusinessDayConvention.Following, tenors, levels, rows, new Actual365Fixed(), Context);

        // QuantLib defines these checks and never calls them, interpolating on unsorted levels
        Assert.Throws<ArgumentException>(() => Make([0.1, 0.05], [[0.2, 0.2], [0.3, 0.3]]));
        Assert.Throws<ArgumentException>(() => Make([0.0, 0.1], [[0.2, 0.2], [0.3, 0.3]]));
        Assert.Throws<ArgumentException>(() => Make([0.1, 1.5], [[0.2, 0.2], [0.3, 0.3]]));

        // a row shorter than the first, which QuantLib reads past
        Assert.Throws<ArgumentException>(() => Make([0.05, 0.1], [[0.2, 0.2], [0.3]]));
        Assert.Equal(0.25, Make([0.05, 0.1], [[0.2, 0.2], [0.3, 0.3]]).Correlation(3.0, 0.075), 1e-15);
    }

    [Fact]
    public void TheSaddlePointExpansionRefusesAnUnresolvableLoss()
    {
        // every recovery 0.9999995: the largest relative loss, 1e-7, is below the bracket's 1e-5 gap, where QuantLib's
        // takes the logarithm of a negative number
        Basket basket = Basket().SetLossModel(new SaddlePointLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(Latent(0.9999995)));
        Assert.Throws<NanoQLException>(() => basket.ProbOverLoss(D5, 1e-7));
    }

    private static Basket Basket(double a = 0.0, double d = 1.0)
    {
        DefaultProbabilityTermStructure[] curves = [.. new[] { 0.01, 0.03, 0.02, 0.015, 0.025 }.Select(h => (DefaultProbabilityTermStructure)Flat(Today, h))];
        return new Basket(Today, Names, Enumerable.Repeat(10.0, 5), PoolOf(Names, curves), Context, a, d);
    }

    private static GaussianConstantLossLM Latent(double recovery) =>
        new(0.3, [.. Enumerable.Repeat(recovery, 5)], LatentModelIntegrationType.GaussianQuadrature, 5);
}
