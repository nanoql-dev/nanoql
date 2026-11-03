// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A basket with realized defaults restated on its live names: the lost notional writes the tranche down from its
/// attachment and the recovered notional amortizes it from its detachment's side, the model prices the live names over
/// the restated tranche and the realized tranche loss is added; the event counts take the order less the realized
/// defaults. QuantLib's adds the whole settled loss to the expected tranche loss, tranches the live loss against min(D,
/// A + max(0, L − A)) where its header means max(0, A − L), and counts the defaulted name among the live ones
/// (<c>basket.cpp:292-299</c>, <c>:325-328</c>, <c>:345-369</c>). On A (10) and B (20) alive and C (30) defaulted and
/// settled at 0.4, QuantLib's values are recorded in the test; the checks are the tranche's loss enumerated over the
/// live names' defaults at independence, and QuantLib on the restated basket (the live names, the restated ratios).
/// </summary>
public class BasketRealizedDefaultsTests
{
    private static readonly Date Inception = new(1, January, 2006);

    [Theory]
    [InlineData(0.0, 1.0, 0.0, 30.0, 18.0)]
    [InlineData(0.1, 0.5, 0.0, 12.0, 12.0)]
    [InlineData(0.4, 0.8, 6.0, 30.0, 0.0)]
    public void TheTrancheIsRestatedOnTheLiveNames(double a, double d, double attach, double detach, double realized)
    {
        // QuantLib, after a statistic: remainingAttachmentAmount 18, 18 and 24, remainingDetachmentAmount 60, 30 and 48
        Basket basket = Realized(a, d);

        Assert.Equal(18.0, basket.SettledLoss(), 1e-13);
        Assert.Equal(attach, basket.RemainingAttachmentAmount(), 1e-13);
        Assert.Equal(detach, basket.RemainingDetachmentAmount(), 1e-13);
        Assert.Equal(realized, basket.RealizedTrancheLoss, 1e-13);
        Assert.Equal(new[] { "A", "B" }, basket.PricingBasket!.Names);
    }

    [Theory]
    [InlineData(0.0, 1.0, 19.713819)]
    [InlineData(0.1, 0.5, 13.659427)]
    [InlineData(0.4, 0.8, 0.625665)]
    public void TheExpectedTrancheLossIsTheTranchesLoss(double a, double d, double truth)
    {
        // QuantLib's recursive model at correlation 0 reads 18 on each tranche
        Basket basket = Realized(a, d).SetLossModel(new IndependentLossModel());

        double pd = Flat(Today, 0.02).DefaultProbability(D5);
        double enumerated = 0.0;
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                double p = (x == 1 ? pd : 1 - pd) * (y == 1 ? pd : 1 - pd);
                double loss = 18 + (6 * x) + (12 * y);
                enumerated += p * Math.Min(Math.Max(loss - (a * 60), 0.0), (d - a) * 60);
            }
        }

        Assert.Equal(truth, enumerated, 1e-6);
        Assert.Equal(enumerated, basket.ExpectedTrancheLoss(D5), 1e-12);
    }

    [Fact]
    public void TheEventCountsTakeTheOrderLessTheRealizedDefaults()
    {
        // QuantLib on the realized basket: P(n >= 1, 2, 3) 0.23198438198843929, 0.047364943691920187,
        // 0.0062871571236788396, and its NTD of order 2 0.0056108156745566067; on the restated basket P(n >= 1, 2)
        // 0.17044456922293286, 0.019979752646425944, and its NTD of order 1 0.022284459334649304
        Basket basket = Realized(0.4, 0.8).SetLossModel(Constant(3, 0.3));

        Assert.Equal(1.0, basket.ProbAtLeastNEvents(1, D5));
        Assert.Equal(0.17044456922293286, basket.ProbAtLeastNEvents(2, D5), 1e-15);
        Assert.Equal(0.019979752646425944, basket.ProbAtLeastNEvents(3, D5), 1e-15);
        Assert.Equal(0.022284459334649304, Ntd(basket, 2, 30.0).FairPremium!.Value, 1e-15);
        Assert.Equal(new[] { 0.0, 0.0 }, basket.ProbsBeingNthEvent(1, D5));
    }

    [Fact]
    public void TheNameReadsTakePoolPositions()
    {
        Basket basket = Realized(0.4, 0.8).SetLossModel(Constant(3, 0.3));

        Assert.Equal(0.4, basket.RecoveryRate(D5, 2));
        Assert.Equal(0.4, basket.RecoveryRate(D5, 0));
        Assert.Equal(Constant(2, 0.3).ResetBasket(basket.PricingBasket!).DefaultCorrelation(D5, 0, 1), basket.DefaultCorrelation(D5, 0, 1));
        Assert.Throws<NanoQLException>(() => basket.DefaultCorrelation(D5, 0, 2));
    }

    [Fact]
    public void AFirstNameDefaultedAtItsOwnRecoveryLeavesTheLiveNamesPrices()
    {
        // A defaulted in June and settled at 0.1, a recovery no live name holds: the order-2 swap is QuantLib's order-1
        // swap on the restated basket of B and C, 0.022284459334649304, where a read of the first name's recovery
        // prices each later default at 0.1 (1.5 times: 0.0334266890019740); the name reads take pool positions
        Basket basket = FirstDefaulted(0.4, 0.8).SetLossModel(Constant(3, 0.3));

        Assert.Equal(0.022284459334649304, Ntd(basket, 2, 30.0).FairPremium!.Value, 1e-15);
        Assert.Equal(0.1, basket.RecoveryRate(D5, 0));

        var unequal = new ConstantLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(
            0.3, [0.3, 0.45, 0.5], LatentModelIntegrationType.GaussianQuadrature, 3);
        Basket priced = FirstDefaulted(0.4, 0.8).SetLossModel(unequal);
        Assert.Equal(0.1, priced.RecoveryRate(D5, 0));
        Assert.Equal(0.45, priced.RecoveryRate(D5, 1));
        Assert.Equal(0.5, priced.RecoveryRate(D5, 2));

        // the live names' correlation through the restricted model, B and C its 0 and 1
        Assert.Equal(Constant(2, 0.3).ResetBasket(basket.PricingBasket!).DefaultCorrelation(D5, 0, 1), basket.DefaultCorrelation(D5, 1, 2));
        Assert.Throws<NanoQLException>(() => basket.DefaultCorrelation(D5, 0, 1));
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.6)]
    [InlineData(0.9)]
    public void TheProbabilityOverALossReadsTheWholeTranche(double fraction)
    {
        // on [10%, 50%], A = 6 and D = 30: the realized tranche loss is 12, so a level at or below it is certain
        Basket basket = Realized(0.1, 0.5).SetLossModel(new IndependentLossModel());

        double level = fraction * 24;
        double truth = Outcomes(0.1, 0.5).Where(o => o.Tranche >= level).Sum(o => o.P);

        Assert.Equal(truth, basket.ProbOverLoss(D5, fraction), 1e-14);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.95)]
    [InlineData(0.9999)]
    public void ThePercentileAndShortfallAddTheRealizedTrancheLoss(double p)
    {
        Basket basket = Realized(0.1, 0.5).SetLossModel(new IndependentLossModel());
        (double P, double Tranche)[] outcomes = [.. Outcomes(0.1, 0.5).OrderBy(o => o.Tranche)];

        double var = IndependentLossModel.Quantile(outcomes, p);
        double tail = outcomes.Where(o => o.Tranche >= var).Sum(o => o.P * o.Tranche) / outcomes.Where(o => o.Tranche >= var).Sum(o => o.P);

        Assert.Equal(var, basket.Percentile(D5, p), 1e-12);
        Assert.Equal(tail, basket.ExpectedShortfall(D5, p), 1e-12);
    }

    [Fact]
    public void TheLossDistributionIsThePools()
    {
        // the pool's loss distribution: the settled 18 and the live names' losses
        Basket basket = Realized(0.1, 0.5).SetLossModel(new IndependentLossModel());
        double pd = Flat(Today, 0.02).DefaultProbability(D5);

        ImmutableSortedDictionary<double, double> dist = basket.LossDistribution(D5);

        Assert.Equal(new[] { 18.0, 24.0, 30.0, 36.0 }, dist.Keys);
        Assert.Equal((1 - pd) * (1 - pd), dist[18.0], 1e-15);
        Assert.Equal(1.0, dist[36.0], 1e-15);
    }

    [Fact]
    public void AProgrammedDefaultIsntRealized()
    {
        // a default after the evaluation date: the statistics are the evaluation date's, the date reads count it
        var later = new Experimental.BankruptcyEvent(Today + 100, new Currencies.EURCurrency(), Seniority.SeniorSec, Today + 120, 0.4);
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.02), Flat(Today, 0.02)];
        Basket programmed = new Basket(Inception, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves, [null, null, [later]]), Context, 0.4, 0.8)
            .SetLossModel(Constant(3, 0.3));
        Basket none = new Basket(Inception, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves), Context, 0.4, 0.8)
            .SetLossModel(Constant(3, 0.3));

        Assert.Equal(3, programmed.RemainingSize());
        Assert.Equal(none.ProbAtLeastNEvents(2, D5), programmed.ProbAtLeastNEvents(2, D5));
        Assert.Equal(new[] { 0, 1 }, programmed.LiveList(Today + 200));
        Assert.Equal(18.0, programmed.SettledLoss(Today + 200), 1e-13);
    }

    // A defaulted and settled at 0.1, B and C alive.
    private static Basket FirstDefaulted(double a, double d)
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.02), Flat(Today, 0.02)];
        var atTenth = new BankruptcyEvent(new Date(1, June, 2006), new EURCurrency(), Seniority.SeniorSec, new Date(1, July, 2006), 0.1);
        Pool pool = PoolOf(["A", "B", "C"], curves, [[atTenth], null, null]);
        return new Basket(Inception, ["A", "B", "C"], [10.0, 20.0, 30.0], pool, Context, a, d);
    }

    // The inception tranche's loss over the live names' defaults at independence, C's 18 realized: each outcome's
    // probability and loss.
    private static IEnumerable<(double P, double Tranche)> Outcomes(double a, double d)
    {
        double pd = Flat(Today, 0.02).DefaultProbability(D5);
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                double p = (x == 1 ? pd : 1 - pd) * (y == 1 ? pd : 1 - pd);
                double loss = 18 + (6 * x) + (12 * y);
                yield return (p, Math.Min(Math.Max(loss - (a * 60), 0.0), (d - a) * 60));
            }
        }
    }

    private static Basket Realized(double a, double d)
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.02), Flat(Today, 0.02)];
        Pool pool = PoolOf(["A", "B", "C"], curves, [null, null, [June2006Bankruptcy()]]);
        return new Basket(Inception, ["A", "B", "C"], [10.0, 20.0, 30.0], pool, Context, a, d);
    }

    // The bound basket's tranche loss at independence, its names' defaults enumerated, every recovery 0.4.
    private sealed class IndependentLossModel : DefaultLossModel
    {
        public override double ExpectedTrancheLoss(Date d)
        {
            Basket b = BoundBasket;
            double[] p = [.. b.Probabilities(d)];
            double etl = 0.0;
            for (int mask = 0; mask < 1 << b.Size; mask++)
            {
                double pr = 1.0, loss = 0.0;
                for (int i = 0; i < b.Size; i++)
                {
                    bool defaulted = ((mask >> i) & 1) != 0;
                    pr *= defaulted ? p[i] : 1 - p[i];
                    loss += defaulted ? b.Notionals[i] * 0.6 : 0.0;
                }

                etl += pr * Math.Min(Math.Max(loss - b.AttachmentAmount, 0.0), b.DetachmentAmount - b.AttachmentAmount);
            }

            return etl;
        }

        // QuantLib's 'probability of losing more or equal to' a fraction of the live tranche, refused below 0 as the LHP
        // model's
        public override double ProbOverLoss(Date d, double lossFraction)
        {
            if (lossFraction < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(lossFraction), lossFraction, "Incorrect loss fraction.");
            }

            Basket b = BoundBasket;
            double level = lossFraction * (b.DetachmentAmount - b.AttachmentAmount);
            return Live(d).Where(o => o.Tranche >= level).Sum(o => o.P);
        }

        public override double Percentile(Date d, double percentile) =>
            Quantile([.. Live(d).OrderBy(o => o.Tranche)], percentile);

        public override double ExpectedShortfall(Date d, double percentile)
        {
            (double P, double Tranche)[] outcomes = [.. Live(d).OrderBy(o => o.Tranche)];
            double var = Quantile(outcomes, percentile);
            return outcomes.Where(o => o.Tranche >= var).Sum(o => o.P * o.Tranche) / outcomes.Where(o => o.Tranche >= var).Sum(o => o.P);
        }

        // the live pool's loss, untranched: each loss and the probability of it or less
        public override ImmutableSortedDictionary<double, double> LossDistribution(Date d)
        {
            var losses = new SortedDictionary<double, double>();
            foreach ((double p, double loss) in Pool(d, BoundBasket))
            {
                losses[loss] = losses.GetValueOrDefault(loss) + p;
            }

            var cumulative = ImmutableSortedDictionary.CreateBuilder<double, double>();
            double sum = 0.0;
            foreach (KeyValuePair<double, double> kv in losses)
            {
                sum += kv.Value;
                cumulative[kv.Key] = sum;
            }

            return cumulative.ToImmutable();
        }

        // the smallest loss whose cumulative probability reaches the level
        internal static double Quantile((double P, double Tranche)[] sorted, double level)
        {
            double sum = 0.0;
            foreach ((double p, double tranche) in sorted)
            {
                sum += p;
                if (sum >= level)
                {
                    return tranche;
                }
            }

            return sorted[^1].Tranche;
        }

        protected override void ResetModel()
        {
        }

        // each outcome of the live names at independence: its probability and its loss, every recovery 0.4
        private static IEnumerable<(double P, double Loss)> Pool(Date d, Basket b)
        {
            double[] p = [.. b.Probabilities(d)];
            for (int mask = 0; mask < 1 << b.Size; mask++)
            {
                double pr = 1.0, loss = 0.0;
                for (int i = 0; i < b.Size; i++)
                {
                    bool defaulted = ((mask >> i) & 1) != 0;
                    pr *= defaulted ? p[i] : 1 - p[i];
                    loss += defaulted ? b.Notionals[i] * 0.6 : 0.0;
                }

                yield return (pr, loss);
            }
        }

        private IEnumerable<(double P, double Tranche)> Live(Date d)
        {
            Basket b = BoundBasket;
            return Pool(d, b).Select(o => (o.P, Math.Min(Math.Max(o.Loss - b.AttachmentAmount, 0.0), b.DetachmentAmount - b.AttachmentAmount)));
        }
    }
}
