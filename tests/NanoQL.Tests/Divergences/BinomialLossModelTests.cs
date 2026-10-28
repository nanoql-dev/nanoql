// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The adjusted binomial's weight (the S-xcr plan's XR8 (xviii)): mixed with the two-point mass at the floor and
/// ceiling of the expected count, the binomial is weighted so that the conditional distribution has the pool's mean
/// and variance, O'Kane's defining property; QuantLib's weight takes −(c − m)² − ((f − m)² − c²)(c − m) for the
/// two-point mass's variance, so its distribution misses the variance it exists to match. On three names (notionals 10,
/// 20, 30, hazards 0.02, 0.03, 0.015, recoveries 0.4, 0.3, 0.5, correlation 0.3) QuantLib's binomial reads the whole
/// pool's expected loss 3.6067712419499616 at five years, its mean, which no weight moves, and the [10%, 40%] tranche's
/// 2.0417230578123919, where the exact recursive model reads 1.8818433951861697 (the S-xcr run's
/// <c>c3_facts.cpp</c>). And the loss distribution integrates a vector over the factor, where QuantLib's quadrature
/// leaves its last node unweighted (XR8 (v), C1's fix): nanoql's is the scalar route's, point by point.
/// </summary>
public class BinomialLossModelTests
{
    private static readonly string[] Abc = ["A", "B", "C"];

    [Theory]
    [InlineData(-2.0)]
    [InlineData(0.0)]
    [InlineData(1.5)]
    [InlineData(3.0)]
    public void TheConditionalDistributionHasThePoolsMeanAndVariance(double factor)
    {
        var bound = (Probe)Bound().LossModel!;
        (double[] density, double mean, double variance) = bound.At(D5, factor);

        double total = density.Sum();
        double m = Enumerable.Range(0, density.Length).Sum(k => k * density[k]);
        double v = Enumerable.Range(0, density.Length).Sum(k => k * k * density[k]) - (m * m);
        Assert.Equal(1.0, total, 1e-13);
        Assert.Equal(mean, m, 1e-13);
        Assert.Equal(variance, v, 1e-12);
    }

    [Fact]
    public void QuantLibsWeightMissesThePoolsVariance()
    {
        // QuantLib's weight at the same node: the conditional variance it gives isn't the pool's
        var bound = (Probe)Bound().LossModel!;
        (double[] density, double mean, double variance) = bound.At(D5, 1.5);
        double alpha = bound.Alpha;
        double quantLibs = bound.QuantLibAlpha;
        Assert.NotEqual(quantLibs, alpha, 1e-6);

        // the variance with QuantLib's weight, from the same binomial and two-point mass
        double n = density.Length - 1;
        double f = Math.Min(n - 1, Math.Floor(mean)), c = f + 1;
        double binomialVariance = n * (mean / n) * (1 - (mean / n));
        double twoPoint = (c - mean) * (mean - f);
        double quantLibVariance = (quantLibs * binomialVariance) + ((1 - quantLibs) * twoPoint);
        Assert.Equal(variance, (alpha * binomialVariance) + ((1 - alpha) * twoPoint), 1e-12);
        Assert.True(Math.Abs(quantLibVariance - variance) > 1e-3 * variance);
    }

    [Fact]
    public void TheWholePoolsExpectedLossIsTheMeansWhateverTheWeight()
    {
        // both weights keep the conditional mean, the whole pool's expected loss: QuantLib's 3.6067712419499616
        double etl = Abc3().SetLossModel(new GaussianBinomialLossModel(Latent())).ExpectedTrancheLoss(D5);
        Assert.Equal(3.6067712419499616, etl, 1e-13);
    }

    [Fact]
    public void ATranchesExpectedLossReadsTheWeight()
    {
        // [10%, 40%]: QuantLib's binomial 2.0417230578123919, the exact recursive model 1.8818433951861697
        double etl = Abc3(0.1, 0.4).SetLossModel(new GaussianBinomialLossModel(Latent())).ExpectedTrancheLoss(D5);
        double exact = Abc3(0.1, 0.4).SetLossModel(new RecursiveGaussLossModel(Latent())).ExpectedTrancheLoss(D5);
        Assert.Equal(1.8818433951861697, exact, 1e-12);
        Assert.NotEqual(2.0417230578123919, etl, 1e-6);
    }

    [Fact]
    public void TheLossDistributionIsTheScalarRoutes()
    {
        var bound = (Probe)Bound().LossModel!;
        double[] vectorRoute = bound.Distribution(D5);
        for (int k = 0; k < vectorRoute.Length; k++)
        {
            Assert.Equal(bound.ScalarRoute(D5, k), vectorRoute[k], 1e-14);
        }
    }

    [Theory]
    [InlineData(0.0, 0.3, 15.0)]
    [InlineData(0.1, 0.3, 10.0)]
    public void ThePercentileAtOneIsATrancheLoss(double a, double d, double width)
    {
        // QuantLib: 29.999999999976612 at 1, the pool's largest loss; just below 1 the tranche's width, its own
        // (the S-xcr code review's C3 O1-1)
        Basket basket = FiveNames(a, d).SetLossModel(new GaussianBinomialLossModel(Latent04()));
        Assert.Equal(width, basket.Percentile(D5, 1.0), 1e-12);
        Assert.Equal(width, basket.Percentile(D5, 0.999999), 1e-9);
    }

    [Fact]
    public void ARealizedDefaultLeavesTheLiveNames()
    {
        (Basket realized, Basket restated) = FirstDefaulted();
        double onRealized = realized.SetLossModel(new GaussianBinomialLossModel(
            new GaussianConstantLossLM(0.3, [0.3, 0.5, 0.6], LatentModelIntegrationType.GaussianQuadrature, 3))).ExpectedTrancheLoss(D5);
        double onRestated = restated.SetLossModel(new GaussianBinomialLossModel(
            new GaussianConstantLossLM(0.3, [0.5, 0.6], LatentModelIntegrationType.GaussianQuadrature, 2))).ExpectedTrancheLoss(D5);
        Assert.Equal(onRestated + 1.5, onRealized, 1e-13);
    }

    // five names of 10 at hazards 0.01 to 0.03, recovery 0.4, correlation 0.4, over a tranche
    private static Basket FiveNames(double a, double d)
    {
        string[] names = ["N0", "N1", "N2", "N3", "N4"];
        DefaultProbabilityTermStructure[] curves = [.. new[] { 0.01, 0.015, 0.02, 0.025, 0.03 }.Select(h => (DefaultProbabilityTermStructure)Flat(Today, h))];
        return new Basket(Today, names, Enumerable.Repeat(10.0, 5), PoolOf(names, curves), Context, a, d);
    }

    private static GaussianConstantLossLM Latent04() =>
        new(0.4, [.. Enumerable.Repeat(0.4, 5)], LatentModelIntegrationType.GaussianQuadrature, 5);
    // A (10) defaulted in June 2006 at 0.25, B (20) and C (30) alive, [10%, 50%] from 1 January 2006, and its restated
    // basket, B and C over [0, 45%]; the realized tranche loss 1.5
    private static (Basket Realized, Basket Restated) FirstDefaulted()
    {
        var inception = new Date(1, Month.January, 2006);
        string[] abc = ["A", "B", "C"];
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.03), Flat(Today, 0.015)];
        var a = new BankruptcyEvent(new Date(1, Month.June, 2006), new EURCurrency(), Seniority.SeniorSec, new Date(1, Month.July, 2006), 0.25);
        var realized = new Basket(inception, abc, [10.0, 20.0, 30.0], PoolOf(abc, curves, [[a], null, null]), Context, 0.1, 0.5);
        string[] bc = ["B", "C"];
        var restated = new Basket(inception, bc, [20.0, 30.0], PoolOf(bc, [Flat(Today, 0.03), Flat(Today, 0.015)]), Context, 0.0, 0.45);
        return (realized, restated);
    }

    private static Basket Abc3(double a = 0.0, double d = 1.0)
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.03), Flat(Today, 0.015)];
        return new Basket(Today, Abc, [10.0, 20.0, 30.0], PoolOf(Abc, curves), Context, a, d);
    }

    private static GaussianConstantLossLM Latent() =>
        new(0.3, [0.4, 0.3, 0.5], LatentModelIntegrationType.GaussianQuadrature, 3);

    private static Basket Bound() => Abc3().SetLossModel(new Probe(Latent()));

    // The model's conditional distribution at a factor value, its weight and QuantLib's, read through its protected
    // members.
    private sealed class Probe(ConstantLossLatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits> copula)
        : BinomialLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(copula)
    {
        private static readonly InverseCumulativeNormal Inverse = new();
        private static readonly CumulativeNormalDistribution Phi = new();
        private readonly ConstantLossLatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits> latent = copula;

        public double Alpha { get; private set; }

        public double QuantLibAlpha { get; private set; }

        // The conditional density and the pool's conditional mean and variance in counts of the average loss.
        public (double[] Density, double Mean, double Variance) At(Date d, double factor)
        {
            Basket b = Basket!;
            ImmutableArray<double> notionals = b.RemainingNotionals();
            double[] inv = [.. b.RemainingProbabilities(d).Select(Inverse.Evaluate)];
            double[] density = LossProbability(d, notionals, inv, [factor]);
            double[] lgd = [.. notionals.Select((n, i) => n * (1 - latent.Recoveries[i]))];
            double avg = lgd.Sum() / lgd.Length;
            double[] p = [.. inv.Select(x => Phi.Evaluate((x - (Math.Sqrt(0.3) * factor)) / Math.Sqrt(0.7)))];
            double mean = p.Select((pi, i) => pi * lgd[i] / avg).Sum();
            double variance = p.Select((pi, i) => pi * (1 - pi) * (lgd[i] / avg) * (lgd[i] / avg)).Sum();

            // the weights: (V - V2) / (VB - V2), with the two-point mass's variance and QuantLib's term for it
            int n = lgd.Length;
            double f = Math.Min(n - 1, Math.Floor(mean)), c = f + 1;
            double pb = mean / n;
            double v2 = (c - mean) * (mean - f);
            Alpha = (variance - v2) / ((n * pb * (1 - pb)) - v2);
            double s = -Math.Pow(c - mean, 2) - ((Math.Pow(f - mean, 2) - (c * c)) * (c - mean));
            QuantLibAlpha = ((variance / n) + s) / ((pb * (1 - pb)) + s);
            return (density, mean, variance);
        }

        public double[] Distribution(Date d) => ExpectedDistribution(d);

        public double ScalarRoute(Date d, int k)
        {
            Basket b = Basket!;
            ImmutableArray<double> notionals = b.RemainingNotionals();
            double[] inv = [.. b.RemainingProbabilities(d).Select(Inverse.Evaluate)];
            return latent.IntegratedExpectedValue(m => LossProbability(d, notionals, inv, m)[k]);
        }
    }
}
