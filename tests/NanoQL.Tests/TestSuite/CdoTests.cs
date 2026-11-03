// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/cdo.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's CDO tests, <c>test-suite/cdo.cpp</c>.</summary>
/// <remarks>
/// QuantLib's case sets each model on each tranche's basket in turn; here each model is bound to each tranche's basket
/// and the CDO built on the bound basket. The random default models run on QuantLib's default seed, 2863311530.
/// </remarks>
[Trait("Oracle", "QuantLib")]
public class CdoTests
{
    private static readonly double[] HwAttachment = [0.00, 0.03, 0.06, 0.10];
    private static readonly double[] HwDetachment = [0.03, 0.06, 0.10, 1.00];

    // HW Table 7
    // corr, Nm, Nz, 0-3, 3-6, 6-10, 10-100
    private static readonly (double Correlation, int Nm, int Nz, double[] TrancheSpread)[] HwData7 =
    [
        (0.1, -1, -1, [2279, 450, 89, 1]),
        (0.3, -1, -1, [1487, 472, 203, 7]),

        // Opening the T, T&G tests too. The convolution is analytical
        //   now so it runs it a time comparable to the gaussian tests and
        //   has enough precission to pass the tests.
        // Below the T models are integrated with a quadrature, even if this
        //   is incorrect the test pass good enough, the quadrature gets to
        //   be worst as the kernel deviates from a normal, this is low
        //   orders of the T; here 5 is enough, 3 would not be.
        (0.3, -1, 5, [1766, 420, 161, 6]),
        (0.3, 5, -1, [1444, 408, 171, 10]),
        (0.3, 5, 5, [1713, 359, 136, 9]),
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void TestHW(int dataSet)
    {
        // Testing CDO premiums against Hull-White values for data set ...
        int poolSize = 100;
        double lambda = 0.01;

        // nBuckets and period determine the computation time
        int nBuckets = 200;

        // for MC engines
        int numSims = 5000;
        const long seed = 2863311530; // QuantLib's default

        double rate = 0.05;
        DayCounter daycount = new Actual360();
        Compounding cmp = Compounding.Continuous; // Simple;

        double recovery = 0.4;
        double[] nominals = [.. Enumerable.Repeat(100.0, poolSize)];
        double premium = 0.02;
        Schedule schedule = new MakeSchedule().From(new Date(1, September, 2006))
                                              .To(new Date(1, September, 2011))
                                              .WithTenor(new Period(3, TimeUnit.Months))
                                              .WithCalendar(new TARGET())
                                              .ToSchedule();

        var asofDate = new Date(31, August, 2006);
        var context = new EvaluationContext { EvaluationDate = asofDate };

        var yieldPtr = new FlatForward(asofDate, rate, daycount, cmp);
        var ptr = new FlatHazardRate(asofDate, lambda, new ActualActual(ActualActualConvention.ISDA));
        var pool = new Pool();
        var names = new List<string>();

        // probability key items
        var probabilities = new[]
        {
            ((DefaultProbKey)new NorthAmericaCorpDefaultKey(new EURCurrency(), Seniority.SeniorSec, new Period(0, TimeUnit.Weeks), 10.0), (DefaultProbabilityTermStructure)ptr),
        };
        for (int i = 0; i < poolSize; i++)
        {
            names.Add("issuer-" + i.ToString(CultureInfo.InvariantCulture));
            pool = pool.Add(
                names[^1],
                new Issuer(probabilities),
                new NorthAmericaCorpDefaultKey(new EURCurrency(), Seniority.SeniorSec, new Period(0, TimeUnit.Days), 1.0));
        }

        Assert.Equal(HwAttachment.Length, HwDetachment.Length);

        int i7 = dataSet;
        double correlation = HwData7[i7].Correlation;
        Assert.Equal(HwAttachment.Length, HwData7[i7].TrancheSpread.Length);
        var basketModels = new List<IDefaultLossModel>();
        var modelNames = new List<string>();
        var relativeToleranceMidp = new List<double>();
        var relativeTolerancePeriod = new List<double>();
        var absoluteTolerance = new List<double>();
        double[] recoveries = [.. Enumerable.Repeat(recovery, poolSize)];

        if (HwData7[i7].Nm == -1 && HwData7[i7].Nz == -1)
        {
            var gaussKtLossLM = new GaussianConstantLossLM(correlation, recoveries, LatentModelIntegrationType.GaussianQuadrature, poolSize);

            // 1.-Inhomogeneous gaussian
            modelNames.Add("Inhomogeneous gaussian");
            basketModels.Add(new IHGaussPoolLossModel(gaussKtLossLM, nBuckets, 5.0, -5, 15));
            absoluteTolerance.Add(1.0);
            relativeToleranceMidp.Add(0.04);
            relativeTolerancePeriod.Add(0.04);

            // 2.-homogeneous gaussian
            modelNames.Add("Homogeneous gaussian");
            basketModels.Add(new HomogGaussPoolLossModel(gaussKtLossLM, nBuckets, 5.0, -5, 15));
            absoluteTolerance.Add(1.0);
            relativeToleranceMidp.Add(0.04);
            relativeTolerancePeriod.Add(0.04);

            // 3.-random default gaussian
            modelNames.Add("Random default gaussian");
            basketModels.Add(new GaussianRandomDefaultLM(gaussKtLossLM, numSims, seed));
            absoluteTolerance.Add(1.0);
            relativeToleranceMidp.Add(0.07);
            relativeTolerancePeriod.Add(0.07);

            // SECOND MC
            // gaussian LHP
            modelNames.Add("Gaussian LHP");
            basketModels.Add(new GaussianLHPLossModel(correlation, recoveries));
            absoluteTolerance.Add(10.0);
            relativeToleranceMidp.Add(0.5);
            relativeTolerancePeriod.Add(0.5);

            // Binomial...
            // Saddle point...
            // Recursive ...
        }
        else
        {
            int[] orders = HwData7[i7].Nm > 0 && HwData7[i7].Nz > 0 ? [HwData7[i7].Nm, HwData7[i7].Nz]
                : HwData7[i7].Nm > 0 ? [HwData7[i7].Nm, 45] // T_{55} is pretty close to a gaussian
                : [45, HwData7[i7].Nz]; // pretty close to gaussian
            string kind = HwData7[i7].Nm > 0 && HwData7[i7].Nz > 0 ? "student"
                : HwData7[i7].Nm > 0 ? "student-gaussian"
                : "gaussian-student";
            var initTG = new TCopulaPolicyInitTraits([.. orders]);
            var tKtLossLM = new TConstantLossLM(correlation, recoveries, LatentModelIntegrationType.GaussianQuadrature, poolSize, initTG);

            // 1.-inhomogeneous
            modelNames.Add("Inhomogeneous " + kind);
            basketModels.Add(new IHStudentPoolLossModel(tKtLossLM, nBuckets, 5.0, -5.0, 15));
            absoluteTolerance.Add(1.0);
            relativeToleranceMidp.Add(0.04);
            relativeTolerancePeriod.Add(0.04);

            // 2.-homogeneous
            modelNames.Add("Homogeneous " + kind);
            basketModels.Add(new HomogTPoolLossModel(tKtLossLM, nBuckets, 5.0, -5.0, 15));
            absoluteTolerance.Add(1.0);
            relativeToleranceMidp.Add(0.04);
            relativeTolerancePeriod.Add(0.04);

            // 3.-random default
            modelNames.Add("Random default " + kind);
            basketModels.Add(new TRandomDefaultLM(tKtLossLM, numSims, seed));
            absoluteTolerance.Add(1.0);
            relativeToleranceMidp.Add(0.07);
            relativeTolerancePeriod.Add(0.07);

            // SECOND MC
            // Binomial...
            // Saddle point...
            // Recursive ...
        }

        for (int j = 0; j < HwAttachment.Length; j++)
        {
            var basket = new Basket(asofDate, names, nominals, pool, context, HwAttachment[j], HwDetachment[j]);
            string trancheId = string.Create(CultureInfo.InvariantCulture, $"[{HwAttachment[j]} , {HwDetachment[j]}]");

            for (int im = 0; im < basketModels.Count; im++)
            {
                var cdoe = new SyntheticCDO(
                    basket.SetLossModel(basketModels[im]), ProtectionSide.Seller, schedule, 0.0, premium, daycount, BusinessDayConvention.Following);

                Check(
                    i7,
                    j,
                    modelNames[im] + " with midp integration on " + trancheId,
                    MidPointCDOEngine.Calculate(cdoe, context, yieldPtr).FairPremium!.Value * 1e4,
                    HwData7[i7].TrancheSpread[j],
                    absoluteTolerance[im],
                    relativeToleranceMidp[im]);

                Check(
                    i7,
                    j,
                    modelNames[im] + " with step integration on " + trancheId,
                    IntegralCDOEngine.Calculate(cdoe, context, yieldPtr).FairPremium!.Value * 1e4,
                    HwData7[i7].TrancheSpread[j],
                    absoluteTolerance[im],
                    relativeTolerancePeriod[im]);
            }
        }
    }

    private static void Check(int i, int j, string desc, double found, double expected, double bpTolerance, double relativeTolerance)
    {
        double absDiff = found - expected;
        double relDiff = absDiff / expected;
        Assert.True(
            Math.Abs(relDiff) < relativeTolerance || Math.Abs(absDiff) < bpTolerance,
            string.Create(CultureInfo.InvariantCulture, $"case {i} {j} ({desc}): {found} vs. {expected}"));
    }
}
