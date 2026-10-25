// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The multi-dimensional Monte Carlo loop and the finite-difference Heston, Bates and Heston-Hull-White steps allocate
/// nothing (the S-hes
/// plan's HR2 (iii) and HD8, S3's closing ruling in S-sr's forms): the draw-price-add of
/// <see cref="EuropeanHestonPathPricer"/> on paths a <see cref="MultiPathGenerator{TGsg}"/> steps in place through the
/// process's in-place forms, on a <see cref="HestonProcess"/> under the quadratic-exponential martingale scheme and full
/// truncation and on a <see cref="BatesProcess"/>, each batch into an <see cref="IncrementalStatistics"/>, which keeps no
/// sample; the same of <see cref="EuropeanGJRGARCHPathPricer"/> on a <see cref="GJRGARCHProcess"/> and of
/// <see cref="HestonHullWhitePathPricer"/> on a <see cref="HybridHestonHullWhiteProcess"/> under both discretizations, its
/// step terms in double-double at a·dt 0.005; and a rollback of <see cref="FdmBackwardSolver"/> on
/// <see cref="FdmHestonOp"/>, plain and with a quanto helper and a leverage function, on <see cref="FdmBatesOp"/> and on
/// <see cref="FdmHestonHullWhiteOp"/>, under the engines' default scheme, Hundsdorfer's, measured on fresh values after a
/// warm-up (<see cref="AllocationAssert"/>) and giving the warm-up's values.
/// </summary>
public class HestonAllocationTests
{
    private const int First = 100;
    private const int Batch = 2000;

    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);
    private static readonly YieldTermStructure Dividends = new FlatForward(Today, 0.02, A365);
    private static readonly NanoQL.TimeGrid Grid = new(1.0, 20);

    [Fact]
    public void TheHestonAndBatesDrawPriceAddAllocatesNothingPerSample()
    {
        HestonProcess[] processes =
        [
            new HestonProcess(
                RiskFree, Dividends, 100.0, 0.04, 1.5, 0.04, 0.5, -0.7, HestonProcessDiscretization.QuadraticExponentialMartingale),
            new HestonProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.04, 0.5, -0.7, HestonProcessDiscretization.FullTruncation),
            new BatesProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.04, 0.5, -0.7, 2.0, -0.2, 0.1),
        ];
        foreach (HestonProcess process in processes)
        {
            foreach (bool antithetic in new[] { false, true })
            {
                AllocationAssert.AllocatesNothing(
                    () => Model(process, antithetic),
                    model => model.AddSamples(Batch));
            }
        }
    }

    [Fact]
    public void AHestonStepOnHundsdorferAllocatesNothing()
    {
        var process = new HestonProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.04, 0.5, -0.7);
        FdmMesherComposite mesher = Mesher(process);
        RollbackAllocatesNothing(new FdmHestonOp(mesher, process), mesher);

        var helper = new FdmQuantoHelper(
            RiskFree, new FlatForward(Today, 0.03, A365), new BlackConstantVol(Today, new NullCalendar(), 0.15, A365), -0.4, 1.0);
        RollbackAllocatesNothing(
            new FdmHestonOp(mesher, process, helper, new LocalConstantVol(Today, 1.2, A365), 0.9), mesher);
    }

    [Fact]
    public void ABatesStepOnHundsdorferAllocatesNothing()
    {
        var process = new BatesProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.04, 0.5, -0.7, 2.0, -0.2, 0.1);
        FdmMesherComposite mesher = Mesher(process);
        RollbackAllocatesNothing(new FdmBatesOp(mesher, process, [], 12), mesher);
    }

    [Fact]
    public void TheGjrGarchAndHybridDrawPriceAddAllocatesNothingPerSample()
    {
        var gjrGarch = new GJRGARCHProcess(RiskFree, Dividends, 50.0, 2.0e-4, 2.0e-6, 0.024, 0.93, 0.059, 0.1, 365.0);
        var heston = new HestonProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.04, 0.5, -0.7);
        HullWhiteForwardProcess hullWhite = new HullWhiteForwardProcess(RiskFree, 0.1, 0.01).WithForwardMeasureTime(2.0);
        foreach (bool antithetic in new[] { false, true })
        {
            AllocationAssert.AllocatesNothing(
                () => Model(gjrGarch, new EuropeanGJRGARCHPathPricer(OptionType.Call, 50.0, 0.95), antithetic),
                model => model.AddSamples(Batch));
            foreach (HybridHestonHullWhiteProcessDiscretization d in Enum.GetValues<HybridHestonHullWhiteProcessDiscretization>())
            {
                var hybrid = new HybridHestonHullWhiteProcess(heston, hullWhite, -0.4, d);
                AllocationAssert.AllocatesNothing(
                    () => Model(hybrid, new HestonHullWhitePathPricer(1.0, new PlainVanillaPayoff(OptionType.Call, 100.0), hybrid), antithetic),
                    model => model.AddSamples(Batch));
            }
        }
    }

    [Fact]
    public void AHestonHullWhiteStepOnHundsdorferAllocatesNothing()
    {
        var process = new HestonProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.04, 0.5, -0.7);
        var mesher = new FdmMesherComposite(
            new Uniform1dMesher(Math.Log(20.0), Math.Log(500.0), 11),
            new FdmHestonVarianceMesher(5, process, 1.0),
            new Uniform1dMesher(-0.1, 0.1, 5));
        RollbackAllocatesNothing(new FdmHestonHullWhiteOp(mesher, process, new HullWhiteProcess(RiskFree, 0.1, 0.01), -0.5), mesher);
    }

    private static FdmMesherComposite Mesher(HestonProcess process) =>
        new(new Uniform1dMesher(Math.Log(20.0), Math.Log(500.0), 21), new FdmHestonVarianceMesher(9, process, 1.0));

    private static MonteCarloModel<MultiPath, IncrementalStatistics> Model(HestonProcess process, bool antithetic)
    {
        IPathGenerator<MultiPath> generator = MultiVariate<PseudoRandom>.MakePathGenerator(
            process, Grid, PseudoRandom.MakeSequenceGenerator(process.Factors * (Grid.Size - 1), 42), false);
        var model = new MonteCarloModel<MultiPath, IncrementalStatistics>(
            generator, new EuropeanHestonPathPricer(OptionType.Call, 100.0, 0.95), new IncrementalStatistics(), antithetic);
        model.AddSamples(First);
        return model;
    }

    private static MonteCarloModel<MultiPath, IncrementalStatistics> Model(
        StochasticProcess process, PathPricer<MultiPath> pricer, bool antithetic)
    {
        IPathGenerator<MultiPath> generator = MultiVariate<PseudoRandom>.MakePathGenerator(
            process, Grid, PseudoRandom.MakeSequenceGenerator(process.Factors * (Grid.Size - 1), 42), false);
        var model = new MonteCarloModel<MultiPath, IncrementalStatistics>(generator, pricer, new IncrementalStatistics(), antithetic);
        model.AddSamples(First);
        return model;
    }

    private static void RollbackAllocatesNothing(FdmLinearOpComposite op, FdmMesher mesher)
    {
        double[] initial = new double[mesher.Layout.Size];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < initial.Length; i++, iter.Increment())
        {
            double x = Math.Exp(mesher.Location(iter, 0)) - 100.0;
            initial[i] = x > 0.0 ? x : 0.0;
        }

        var solver = new FdmBackwardSolver(op, ImmutableArray<BoundaryCondition<FdmLinearOp>>.Empty, null, FdmSchemeDesc.Hundsdorfer());
        double[] warm = [.. initial];
        solver.Rollback(warm, 1.0, 0.0, 20, 0);

        double[] values = AllocationAssert.AllocatesNothing(
            () => (double[])initial.Clone(),
            fresh => solver.Rollback(fresh, 1.0, 0.0, 20, 0));

        Assert.Equal(warm, values);
    }
}
