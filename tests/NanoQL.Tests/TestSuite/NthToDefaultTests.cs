// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/nthtodefault.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's n-th-to-default tests, <c>test-suite/nthtodefault.cpp</c>.</summary>
/// <remarks>
/// QuantLib's cases set one loss model on the basket and change its correlation quote between pricings, the swaps
/// repricing through the observers; here a model is built and bound per correlation, and the swaps on the bound
/// basket (the S-xcr plan's XR1 (d)).
/// </remarks>
[Trait("Oracle", "QuantLib")]
public class NthToDefaultTests
{
    // Spread (bp p.a.) to buy protection for the nth to default from
    // a basket of 10 names. All pairs have same correlation, 0 in
    // column 2, 0.3 in column 3, 0.6 in column 4. Default intensity
    // for all names is constant at 0.01, maturity 5 years, equal
    // notional amounts.
    private static readonly (int Rank, double[] Spread)[] HwData =
    [
        (1, [603, 440, 293]),
        (2, [98, 139, 137]),
        (3, [12, 53, 79]),
        (4, [1, 21, 49]),
        (5, [0, 8, 31]),
        (6, [0, 3, 19]),
        (7, [0, 1, 12]),
        (8, [0, 0, 7]),
        (9, [0, 0, 3]),
        (10, [0, 0, 1]),
    ];

    private static readonly double[] HwCorrelation = [0.0, 0.3, 0.6];

    // HW Table 3, Nth to Default Basket
    // corr = 0.3
    // NM/NZ
    // rank inf/inf 5/inf inf/5 5/5
    private static readonly (int Rank, double[] Spread)[] HwDataDist =
    [
        (1, [440, 419, 474, 455]),
        (2, [139, 127, 127, 116]),
        (3, [53, 51, 44, 44]),
        (4, [21, 24, 18, 22]),
        (5, [8, 13, 7, 13]),
        (6, [3, 8, 3, 8]),
        (7, [1, 5, 1, 5]),
        (8, [0, 3, 0, 4]),
        (9, [0, 2, 0, 2]),
        (10, [0, 1, 0, 1]),
    ];

    [Fact]
    public void TestGauss()
    {
        // Testing nth-to-default against Hull-White values with Gaussian copula...

        /*************************
         * Tolerances
         */
        double relTolerance = 0.015; // relative difference
        double absTolerance = 1; // absolute difference in bp

        Setup setup = Build();
        for (int j = 0; j < HwCorrelation.Length; j++)
        {
            var copula = new ConstantLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(
                HwCorrelation[j], Enumerable.Repeat(setup.Recovery, setup.Names).ToArray(), LatentModelIntegrationType.GaussianQuadrature, setup.Names);
            Basket basket = setup.Basket.SetLossModel(copula);
            for (int i = 0; i < setup.Names; i++)
            {
                NthToDefault ntd = setup.Ntd(basket, i + 1);
                Assert.Equal(HwData[i].Rank, ntd.Rank);
                double diff = (1e4 * setup.FairPremium(ntd)) - HwData[i].Spread[j];
                Assert.True(
                    Math.Abs(diff / HwData[i].Spread[j]) < relTolerance || Math.Abs(diff) < absTolerance,
                    $"tolerance {relTolerance}|{absTolerance} exceeded");
            }
        }
    }

    [Fact]
    public void TestStudent()
    {
        // Testing nth-to-default against Hull-White values with Student copula...

        /*************************
         * Tolerances
         */
        double relTolerance = 0.017; // relative difference
        double absTolerance = 1; // absolute difference in bp

        Setup setup = Build();
        var iniT = new TCopulaPolicyInitTraits([5, 5]);

        // This is the necessary code, but a proper hwData for the t copula is needed.
        // instead of this BEGIN
        var copula = new ConstantLossModel<TCopulaPolicy, TCopulaPolicyInitTraits>(
            0.3, Enumerable.Repeat(setup.Recovery, setup.Names).ToArray(), LatentModelIntegrationType.GaussianQuadrature, setup.Names, iniT);
        Basket basket = setup.Basket.SetLossModel(copula);
        for (int i = 0; i < setup.Names; i++)
        {
            NthToDefault ntd = setup.Ntd(basket, i + 1);
            Assert.Equal(HwDataDist[i].Rank, ntd.Rank);
            double diff = (1e4 * setup.FairPremium(ntd)) - HwDataDist[i].Spread[3];
            Assert.True(
                Math.Abs(diff / HwDataDist[i].Spread[3]) < relTolerance || Math.Abs(diff) < absTolerance,
                $"tolerance {relTolerance}|{absTolerance} exceeded{i}|{Math.Abs(diff)}|{HwDataDist[i].Spread[3]}");
        }

        // END
    }

    // The two cases' common market, pool, basket and swaps.
    private static Setup Build()
    {
        Period timeUnit = new(1, TimeUnit.Weeks); // required to reach accuracy

        int names = 10;
        Assert.Equal(names, HwData.Length);

        double rate = 0.05;
        DayCounter dc = new Actual365Fixed();
        Compounding cmp = Compounding.Continuous; // Simple;

        double recovery = 0.4;
        double[] lambda = [.. Enumerable.Repeat(0.01, names)];

        double namesNotional = 100.0;

        var asofDate = new Date(31, August, 2006);
        var context = new EvaluationContext { EvaluationDate = asofDate };

        Schedule schedule = new MakeSchedule().From(new Date(1, September, 2006))
                                              .To(new Date(1, September, 2011))
                                              .WithTenor(new Period(3, TimeUnit.Months))
                                              .WithCalendar(new TARGET())
                                              .ToSchedule();

        var yieldPtr = new FlatForward(asofDate, rate, dc, cmp);

        var probabilities = new List<DefaultProbabilityTermStructure>();
        foreach (double i in lambda)
        {
            probabilities.Add(new FlatHazardRate(asofDate, i, dc));
        }

        // Set up pool and basket
        var namesIds = new List<string>(names);
        for (int i = 0; i < names; i++)
        {
            namesIds.Add("Name" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var issuers = new List<Issuer>();
        for (int i = 0; i < names; i++)
        {
            var curves = new[]
            {
                ((DefaultProbKey)new NorthAmericaCorpDefaultKey(new EURCurrency(), Seniority.SeniorSec, new Period(0, TimeUnit.Days), 1.0), probabilities[i]),
            };
            issuers.Add(new Issuer(curves));
        }

        var thePool = new Pool();
        for (int i = 0; i < names; i++)
        {
            thePool = thePool.Add(namesIds[i], issuers[i], new NorthAmericaCorpDefaultKey(new EURCurrency(), Seniority.SeniorSec, new Period(0, TimeUnit.Days), 1.0));
        }

        var basket = new Basket(
            asofDate, namesIds, Enumerable.Repeat(namesNotional / names, names), thePool, context, 0.0, 1.0);

        return new Setup(names, recovery, namesNotional, schedule, timeUnit, yieldPtr, basket, context);
    }

    private sealed record Setup(
        int Names,
        double Recovery,
        double NamesNotional,
        Schedule Schedule,
        Period TimeUnit,
        YieldTermStructure Yield,
        Basket Basket,
        EvaluationContext Context)
    {
        public NthToDefault Ntd(Basket basket, int i) =>
            new(basket, i, ProtectionSide.Seller, Schedule, 0.0, 0.02, new Actual360(), NamesNotional * Names, true);

        public double FairPremium(NthToDefault ntd) =>
            IntegralNtdEngine.Calculate(ntd, Context, TimeUnit, Yield).FairPremium!.Value;
    }
}
