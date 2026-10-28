// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The saddle-point model's cumulants and conditional statistics, which its statistics read and QuantLib exposes to
/// derived models: each derivative the central difference of the one before, the combined forms equal to the single
/// ones, the generating function 0 at 0 and its first derivative there the expected loss, the split adding up to the
/// loss at its saddle point, the shortfall split's sum its closed form, and an equity tranche's shortfall the sum of
/// the portfolio's.
/// </summary>
public class SaddlePointCumulantTests
{
    private const double H = 1e-4;
    private static readonly string[] Names = [.. Enumerable.Range(0, 10).Select(i => $"N{i}")];

    [Theory]
    [InlineData(-1.0, 0.5)]
    [InlineData(0.0, 2.0)]
    [InlineData(1.5, -1.0)]
    public void EachDerivativeIsTheDifferenceOfTheOneBefore(double factor, double s)
    {
        Probe p = Bound();
        double[] inv = p.Inverse(D5);
        double[] m = [factor];
        double Diff(Func<double, double> f) => (f(s + H) - f(s - H)) / (2 * H);
        Assert.Equal(Diff(x => p.K0(inv, x, m)), p.K1(inv, s, m), 1e-7);
        Assert.Equal(Diff(x => p.K1(inv, x, m)), p.K2(inv, s, m), 1e-7);
        Assert.Equal(Diff(x => p.K2(inv, x, m)), p.K3(inv, s, m), 1e-7);
        Assert.Equal(Diff(x => p.K3(inv, x, m)), p.K4(inv, s, m), 1e-7);

        (double d0, double d2, double d3, double d4) = p.K0234(inv, s, m);
        (double e0, double e2) = p.K02(inv, s, m);
        Assert.Equal(p.K0(inv, s, m), d0, 1e-14);
        Assert.Equal(p.K2(inv, s, m), d2, 1e-14);
        Assert.Equal(p.K3(inv, s, m), d3, 1e-14);
        Assert.Equal(p.K4(inv, s, m), d4, 1e-14);
        Assert.Equal(d0, e0, 1e-14);
        Assert.Equal(d2, e2, 1e-14);
    }

    [Fact]
    public void TheUnconditionalCumulantsReadTheExpectedLoss()
    {
        Probe p = Bound();

        // K(0) = 0, and K'(0) the expected loss over the notional: sum of N (1 - R) pd over 100
        double expected = Enumerable.Range(0, 10).Sum(i => 10.0 * 0.6 * Flat(Today, 0.01 + (0.002 * i)).DefaultProbability(D5)) / 100.0;
        Assert.Equal(0.0, p.UnconditionalK(D5, 0, 0.0), 1e-15);
        Assert.Equal(expected, p.UnconditionalK(D5, 1, 0.0), 1e-9);
        Assert.True(p.UnconditionalK(D5, 2, 0.0) > 0.0);
        Assert.True(double.IsFinite(p.UnconditionalK(D5, 3, 0.0)));
        Assert.True(double.IsFinite(p.UnconditionalK(D5, 4, 0.0)));
    }

    [Fact]
    public void TheConditionalStatisticsHoldTheirIdentities()
    {
        Probe p = Bound();
        double[] inv = p.Inverse(D5);
        double[] m = [0.5];
        double loss = 6.0;

        // the split adds up to the loss at its saddle point, found to its solver's accuracy
        double[] split = p.Split(inv, loss, m);
        double saddle = p.Saddle(inv, loss / 100.0, m);
        Assert.Equal(p.K1(inv, saddle, m) * 100.0, split.Sum(), 1e-12);
        Assert.Equal(loss, split.Sum(), 0.05 * loss);

        // the Gaussian shortfall split: sum of N_i esf_i is muTot Phi(u) + phi(u)
        double[] esf = p.EsfSplit(inv, loss, m);
        Assert.Equal(10, esf.Length);
        Assert.True(esf.All(double.IsFinite));

        // an equity tranche's shortfall is the portfolio's above the loss and above the detachment
        double tranche = p.EsfTranche(inv, loss, 0.9, m);
        Assert.Equal(p.EsfPortfolio(inv, loss, m) + p.EsfPortfolio(inv, p.Basket!.DetachmentAmount, m), tranche, 1e-12);

        // the first order tail, between 0 and 1
        double first = p.FirstOrder(inv, loss, m);
        Assert.InRange(first, 0.0, 1.0);
    }

    private static Probe Bound()
    {
        DefaultProbabilityTermStructure[] curves = [.. Enumerable.Range(0, 10).Select(i => (DefaultProbabilityTermStructure)Flat(Today, 0.01 + (0.002 * i)))];
        var basket = new Basket(Today, Names, Enumerable.Repeat(10.0, 10), PoolOf(Names, curves), Context, 0.0, 0.3);
        var lm = new GaussianConstantLossLM(0.3, [.. Enumerable.Repeat(0.4, 10)], LatentModelIntegrationType.GaussianQuadrature, 10);
        return (Probe)basket.SetLossModel(new Probe(lm)).LossModel!;
    }

    // The model's protected cumulants and conditional statistics.
    private sealed class Probe(ConstantLossLatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits> copula)
        : SaddlePointLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(copula)
    {
        private static readonly InverseCumulativeNormal Inv = new();

        public double[] Inverse(Date d) => [.. Basket!.RemainingProbabilities(d).Select(Inv.Evaluate)];

        public double K0(double[] i, double s, double[] m) => CumulantGeneratingCond(i, s, m);

        public double K1(double[] i, double s, double[] m) => CumGen1stDerivativeCond(i, s, m);

        public double K2(double[] i, double s, double[] m) => CumGen2ndDerivativeCond(i, s, m);

        public double K3(double[] i, double s, double[] m) => CumGen3rdDerivativeCond(i, s, m);

        public double K4(double[] i, double s, double[] m) => CumGen4thDerivativeCond(i, s, m);

        public (double, double, double, double) K0234(double[] i, double s, double[] m) => CumGen0234DerivCond(i, s, m);

        public (double, double) K02(double[] i, double s, double[] m) => CumGen02DerivCond(i, s, m);

        public double UnconditionalK(Date d, int order, double s) => order switch
        {
            0 => CumulantGenerating(d, s),
            1 => CumGen1stDerivative(d, s),
            2 => CumGen2ndDerivative(d, s),
            3 => CumGen3rdDerivative(d, s),
            _ => CumGen4thDerivative(d, s),
        };

        public double[] Split(double[] i, double loss, double[] m) => SplitLossCond(i, loss, m);

        public double Saddle(double[] i, double level, double[] m) => FindSaddle(i, level, m);

        public double[] EsfSplit(double[] i, double loss, double[] m) => ExpectedShortfallSplitCond(i, loss, m);

        public double EsfTranche(double[] i, double loss, double percentile, double[] m) => ExpectedShortfallTrancheCond(i, loss, percentile, m);

        public double EsfPortfolio(double[] i, double loss, double[] m) => ExpectedShortfallFullPortfolioCond(i, loss, m);

        public double FirstOrder(double[] i, double loss, double[] m) => ProbOverLossPortfCond1stOrder(i, loss, m);
    }
}
