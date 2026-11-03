// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The credit portfolio's two routes to one value: the Student t policy against the Gaussian at a high order, the
/// convolved t against a simulation of its sum, the latent model's Gauss-Hermite integration against its trapezoids,
/// and the n-th-to-default on one name against a credit default swap on it.
/// </summary>
public class CreditPortfolioTwoRouteTests
{
    [Fact]
    public void TheStudentTPolicyTendsToTheGaussian()
    {
        // QuantLib 1.44: at order 171, cumulativeY(-2) 0.022841385023 against the normal's 0.022750131948
        var t = new TDefProbLM(0.3, 3, LatentModelIntegrationType.GaussianQuadrature, new TCopulaPolicyInitTraits([171, 171]));
        var g = new GaussianDefProbLM(0.3, 3, LatentModelIntegrationType.GaussianQuadrature);
        Basket basket = Three();

        DefaultLatentModel<TCopulaPolicy, TCopulaPolicyInitTraits> tb = t.ResetBasket(basket);
        DefaultLatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits> gb = g.ResetBasket(basket);

        Assert.Equal(0.022841385023, t.CumulativeY(-2.0, 0), 1e-11);
        Assert.Equal(gb.ProbAtLeastNEvents(2, D5), tb.ProbAtLeastNEvents(2, D5), 0.02 * gb.ProbAtLeastNEvents(2, D5));
        Assert.Equal(gb.ProbOfDefault(0, D5), tb.ProbOfDefault(0, D5), 1e-6);
    }

    [Fact]
    public void TheConvolvedStudentTIsItsSumsDistribution()
    {
        // 400,000 sums of two t's of order 5 on the T policy's normalized weights at correlation 0.3 (QuantLib's own
        // weights give 0.795562 against a simulation's 0.795340, +-0.000638)
        double vf = Math.Sqrt(3.0 / 5.0);
        double a = Math.Sqrt(0.3) * vf, b = Math.Sqrt(0.7) * vf;
        var c = new CumulativeBehrensFisher([5, 5], [a, b]);
        var rng = new MersenneTwisterUniformRng(42);
        const int N = 400000;
        int below = 0;
        for (int i = 0; i < N; i++)
        {
            double x = (a * Student5(rng)) + (b * Student5(rng));
            below += x <= 1.0 ? 1 : 0;
        }

        double p = c.Value(1.0);
        Assert.Equal(p, (double)below / N, 3 * Math.Sqrt(p * (1 - p) / N));
    }

    [Fact]
    public void TheQuadratureAndTheTrapezoidsIntegrateAlike()
    {
        var gh = new GaussianDefProbLM(0.3, 3, LatentModelIntegrationType.GaussianQuadrature).ResetBasket(Three());
        var tz = new GaussianDefProbLM(0.3, 3, LatentModelIntegrationType.Trapezoid).ResetBasket(Three());
        double unconditional = Flat(Today, 0.02).DefaultProbability(D5);

        Assert.Equal(unconditional, gh.ProbOfDefault(1, D5), 1e-8);
        Assert.Equal(gh.ProbOfDefault(1, D5), tz.ProbOfDefault(1, D5), 1e-6);
        Assert.Equal(gh.ProbAtLeastNEvents(2, D5), tz.ProbAtLeastNEvents(2, D5), 1e-6);
    }

    [Fact]
    public void AnNthToDefaultOnOneNameIsACreditDefaultSwap()
    {
        // QuantLib 1.44: NTD(1 name) 0.0119097655252, the CDS's fair spread on the integral engine 0.0119097655041,
        // both at daily steps on a 5% Actual/360 discount curve and a hazard of 0.02
        var discount = new FlatForward(Today, 0.05, new Actual360());
        Schedule s = new MakeSchedule().From(Today).To(new Date(31, Month.August, 2011))
            .WithTenor(new Period(3, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();
        Basket basket = new Basket(Today, ["N000"], [100.0], PoolOf(["N000"], [Flat(Today, 0.02)]), Context)
            .SetLossModel(Constant(1, 0.3));
        var ntd = new NthToDefault(basket, 1, ProtectionSide.Seller, s, 0.0, 0.02, new Actual360(), 100.0, true);
        var cds = new CreditDefaultSwap(
            ProtectionSide.Seller, 100.0, 0.02, s, BusinessDayConvention.Unadjusted, new Actual360(), true, true, Today);

        double ntdFair = IntegralNtdEngine.Calculate(ntd, Context, new Period(1, TimeUnit.Days), discount).FairPremium!.Value;
        double cdsFair = IntegralCdsEngine.Calculate(cds, Context, new Period(1, TimeUnit.Days), Flat(Today, 0.02), 0.4, discount).FairSpread!.Value;

        Assert.Equal(0.0119097655252, ntdFair, 1e-12);
        Assert.Equal(0.0119097655041, cdsFair, 1e-12);
        Assert.Equal(cdsFair, ntdFair, 1e-10);
    }

    // A t of order 5 from Bailey's polar method, the polar generator's, by its own uniforms.
    private static double Student5(MersenneTwisterUniformRng rng)
    {
        double u, v, r;
        do
        {
            v = (2 * rng.Next().Value) - 1;
            u = (2 * rng.Next().Value) - 1;
            r = (u * u) + (v * v);
        }
        while (r >= 1);
        return u * Math.Sqrt(5 * (Math.Pow(r, -2.0 / 5) - 1) / r);
    }

    private static Basket Three()
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.01), Flat(Today, 0.02), Flat(Today, 0.03)];
        return new Basket(Today, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves), Context);
    }
}
