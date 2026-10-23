// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Monte Carlo Hull-White cap engine's seed is explicit (the S-sr plan's SSR1 (iv)): QuantLib's 0, a seed from the
/// clock and its builder's default, is refused by the engine, the builder's constructor and its <c>WithSeed</c>. And
/// its loop allocates nothing per sample (SSR1 (v)): the path's step, its bonds and its payoff read the curve's forward
/// rates without building an <see cref="InterestRate"/>, so a calculation into an <see cref="IncrementalStatistics"/>,
/// which keeps no sample, allocates as much at 3,000 samples as at 1,000, plain and antithetic.
/// </summary>
public class McHullWhiteCapFloorTests
{
    private static readonly Date Today = new(16, Month.June, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.04, new Actual365Fixed());
    private static readonly HullWhite Model = new(Curve, 0.1, 0.01);

    [Fact]
    public void SeedZeroIsRefused()
    {
        Cap cap = TwoYearCap();
        foreach (Action refused in new Action[]
                 {
                     () => MCHullWhiteCapFloorEngine.Calculate<PseudoRandom, GeneralStatistics>(
                         cap, Context, Model, false, false, 100, null, null, 0),
                     () => _ = new MakeMCHullWhiteCapFloorEngine<PseudoRandom, GeneralStatistics>(Model, 0),
                     () => new MakeMCHullWhiteCapFloorEngine<PseudoRandom, GeneralStatistics>(Model, 42).WithSeed(0),
                 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(refused);
            Assert.Equal("seed", e.ParamName);
        }

        // a seed in range prices
        Assert.NotNull(MCHullWhiteCapFloorEngine.Calculate<PseudoRandom, GeneralStatistics>(
            cap, Context, Model, false, false, 100, null, null, 1).NPV);
    }

    [Fact]
    public void TheLoopAllocatesNothingPerSample()
    {
        Cap cap = TwoYearCap();
        foreach (bool antithetic in new[] { false, true })
        {
            long Allocated(int samples)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                MCHullWhiteCapFloorEngine.Calculate<PseudoRandom, IncrementalStatistics>(
                    cap, Context, Model, false, antithetic, samples, null, null, 42);
                return GC.GetAllocatedBytesForCurrentThread() - before;
            }

            // 2,000 samples more allocate fewer than 2,000 bytes more, under a byte a sample, where one object a sample
            // is 48,000 bytes or more: a calculation's own bytes vary by a few hundred from call to call under the
            // suite's load, which an equality can't tell from a per-sample allocation; up to five measurements, as
            // AllocationAssert takes, the runtime allocating on the thread beside the loop at times
            Allocated(1000);
            var measured = new List<(long, long)>();
            bool bounded = false;
            for (int i = 0; i < 5 && !bounded; i++)
            {
                long few = Allocated(1000), many = Allocated(3000);
                measured.Add((few, many));
                bounded = many - few < 2000;
            }

            Assert.True(bounded, $"allocated (1,000 samples, 3,000 samples): {string.Join(", ", measured)} bytes");
        }
    }

    // A two-year cap on Euribor 6M from the spot date: four caplets, the first fixed today.
    private static Cap TwoYearCap()
    {
        var index = new Euribor6M(Curve);
        var schedule = new Schedule(
            new Date(18, Month.June, 2025), new Date(18, Month.June, 2027), new Period(6, TimeUnit.Months), new TARGET(),
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, index).WithNotionals([1.0]).ToLeg(Context);
        return new Cap(leg, [0.04]);
    }
}
