// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="SaddlePointTailLossModel{TCopula, TInitTraits}"/>, the saddle-point model's expected tranche loss read
/// from its own tail, against the recursive model on the same latent model (QuantLib's 25-node Gauss-Hermite), on 100
/// names of notional 100, hazard 1% and recovery 40%, at one and five years: each cell within its measured error. From
/// correlation 0.3 the tranches from 3% to 22% are within 2.5% (the worst measured 1.98%) and the equity tranche to
/// 0.75 within 0.75% (0.56%); at 0.9 the equity tranche reads about +9.1% at five years, a senior tranche reaching the
/// pool's largest loss −4.4%, and [22%, 60%] is refused, its tail below 0 near that loss; a thin attachment reads the
/// tail near zero loss, [0.5%, 3%] about +21% at a year and 0.6, and [0.1%, 3%] is refused there; at correlations 0 and
/// 0.1 the pool's binomial lattice, absolute where the tranche is small. The base's members are the base's.
/// </summary>
public class SaddlePointTailLossModelTests
{
    private const int Names = 100;
    private const double Notional = 100.0;
    private const double Recovery = 0.4;
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly string[] Ids = [.. Enumerable.Range(0, Names).Select(i => $"N{i}")];
    private static readonly Pool Portfolio = Build();

    /// <summary>Gets the cells: years, correlation, the tranche, and the error's bounds, relative or, for a small
    /// tranche, absolute.</summary>
    public static TheoryData<int, double, double, double, double, double, bool> Cells
    {
        get
        {
            var cells = new TheoryData<int, double, double, double, double, double, bool>();
            foreach (int years in new[] { 1, 5 })
            {
                foreach (double rho in new[] { 0.3, 0.45, 0.6, 0.75, 0.9 })
                {
                    foreach ((double a, double d) in new[] { (0.03, 0.06), (0.06, 0.09), (0.09, 0.12), (0.12, 0.22) })
                    {
                        cells.Add(years, rho, a, d, -0.025, 0.025, false);
                    }

                    if (rho <= 0.75)
                    {
                        cells.Add(years, rho, 0.0, 0.03, -0.0075, 0.0075, false);
                    }
                }
            }

            // the equity tranche at 0.9: +0.83% at a year, +9.12% at five
            cells.Add(1, 0.9, 0.0, 0.03, 0.0, 0.0125, false);
            cells.Add(5, 0.9, 0.0, 0.03, 0.0875, 0.095, false);

            // a senior tranche reaching the pool's largest loss at 0.9 and five years, −4.40%
            cells.Add(5, 0.9, 0.40, 0.70, -0.0465, -0.0415, false);

            // a thin attachment, its integral starting below one default's loss, 0.6% of the pool: [0.5%, 3%] at a year
            // and 0.6, +21.33%
            cells.Add(1, 0.6, 0.005, 0.03, 0.20, 0.23, false);

            // the binomial lattice: correlation 0 at five years (−0.60%, +3.63%, −0.23 and +0.0068 absolute); 0.1 at a
            // year on a 1.73 tranche (+0.155 absolute) and at five years (within 1.8%)
            cells.Add(5, 0.0, 0.0, 0.03, -0.0075, -0.0045, false);
            cells.Add(5, 0.0, 0.03, 0.06, 0.033, 0.04, false);
            cells.Add(5, 0.0, 0.06, 0.09, -0.25, -0.2, true);
            cells.Add(5, 0.0, 0.09, 0.12, 0.005, 0.0085, true);
            cells.Add(1, 0.1, 0.03, 0.06, 0.13, 0.18, true);
            foreach ((double a, double d) in new[] { (0.0, 0.03), (0.03, 0.06), (0.06, 0.09), (0.09, 0.12), (0.12, 0.22) })
            {
                cells.Add(5, 0.1, a, d, -0.02, 0.02, false);
            }

            return cells;
        }
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void TheTrancheLossIsTheRecursiveModelsWithinItsMeasuredError(
        int years, double rho, double attach, double detach, double low, double high, bool absolute)
    {
        Date horizon = Today + new Period(years, TimeUnit.Years);
        double tail = Tranche(attach, detach, new SaddlePointTailLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(Latent(rho)))
            .ExpectedTrancheLoss(horizon);
        double recursive = Tranche(attach, detach, new RecursiveLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(Latent(rho)))
            .ExpectedTrancheLoss(horizon);
        double error = absolute ? tail - recursive : (tail - recursive) / recursive;
        Assert.InRange(error, low, high);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void ATrancheWhoseNodesCrowdTheLargestLossIsRefusedAtHighCorrelation(int years)
    {
        // [22%, 60%] at 0.9: its nodes crowd the pool's largest loss, 60% at recovery 40%, where the expansion's tail
        // reads below 0 (−0.0044 at five years, −1.45 at a year)
        Basket basket = Tranche(0.22, 0.60, new SaddlePointTailLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(Latent(0.9)));
        var e = Assert.Throws<NanoQLException>(() => basket.ExpectedTrancheLoss(Today + new Period(years, TimeUnit.Years)));
        Assert.StartsWith("the saddle-point expansion reads a tail probability of -", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void AThinAttachmentIsRefusedWhereItReadsTheTailNearZeroLoss(int years)
    {
        // [0.1%, 3%] at 0.6: its integral starts below one default's loss, where the expansion's tail leaves [0, 1]
        Basket basket = Tranche(0.001, 0.03, new SaddlePointTailLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(Latent(0.6)));
        var e = Assert.Throws<NanoQLException>(() => basket.ExpectedTrancheLoss(Today + new Period(years, TimeUnit.Years)));
        Assert.StartsWith("the saddle-point expansion reads a tail probability of ", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBasesMembersAreTheBases()
    {
        Date horizon = Today + new Period(5, TimeUnit.Years);
        Basket tail = Tranche(0.03, 0.06, new SaddlePointTailLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(Latent(0.3)));
        Basket plain = Tranche(0.03, 0.06, new SaddlePointLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(Latent(0.3)));
        var tailModel = (SaddlePointLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>)tail.LossModel!;
        var plainModel = (SaddlePointLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>)plain.LossModel!;
        foreach (double loss in new[] { 300.0, 900.0, 2_000.0 })
        {
            Assert.Equal(plainModel.ProbOverPortfLoss(horizon, loss), tailModel.ProbOverPortfLoss(horizon, loss));
        }

        Assert.Equal(plain.ProbOverLoss(horizon, 0.5), tail.ProbOverLoss(horizon, 0.5));
        Assert.Equal(plain.Percentile(horizon, 0.9), tail.Percentile(horizon, 0.9));
    }

    [Fact]
    public void OnTheEvaluationDateTheLossIsZero()
    {
        foreach ((double a, double d) in new[] { (0.0, 0.03), (0.03, 0.06) })
        {
            Assert.Equal(0.0, Tranche(a, d, new SaddlePointTailLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(Latent(0.3))).ExpectedTrancheLoss(Today));
        }
    }

    private static Pool Build()
    {
        var key = new NorthAmericaCorpDefaultKey(new EURCurrency(), Seniority.SeniorSec, new Period(0, TimeUnit.Days), 1.0);
        var pool = new Pool();
        foreach (string id in Ids)
        {
            pool = pool.Add(id, new Issuer([(key, (DefaultProbabilityTermStructure)new FlatHazardRate(Today, 0.01, new Actual365Fixed()))], []), key);
        }

        return pool;
    }

    private static GaussianConstantLossLM Latent(double rho) =>
        new(rho, [.. Enumerable.Repeat(Recovery, Names)], LatentModelIntegrationType.GaussianQuadrature, Names);

    private static Basket Tranche(double attach, double detach, DefaultLossModel model) =>
        new Basket(Today, Ids, Enumerable.Repeat(Notional, Names), Portfolio, Context, attach, detach).SetLossModel(model);
}
