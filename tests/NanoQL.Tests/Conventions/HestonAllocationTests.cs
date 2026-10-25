// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The multi-dimensional Monte Carlo loop and the finite-difference Heston and Bates steps allocate nothing (the S-hes
/// plan's HR2 (iii) and HD8, S3's closing ruling in S-sr's forms): the draw-price-add of
/// <see cref="EuropeanHestonPathPricer"/> on paths a <see cref="MultiPathGenerator{TGsg}"/> steps in place through the
/// process's in-place forms, on a <see cref="HestonProcess"/> under the quadratic-exponential martingale scheme and full
/// truncation and on a <see cref="BatesProcess"/>, each batch into an <see cref="IncrementalStatistics"/>, which keeps no
/// sample; and a rollback of <see cref="FdmBackwardSolver"/> on <see cref="FdmHestonOp"/>, plain and with a quanto helper
/// and a leverage function, and on <see cref="FdmBatesOp"/>, under the engines' default scheme, Hundsdorfer's, measured
/// on fresh values after a warm-up (<see cref="AllocationAssert"/>) and giving the warm-up's values.
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
