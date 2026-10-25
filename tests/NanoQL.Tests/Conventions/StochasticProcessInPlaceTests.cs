// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The in-place <see cref="StochasticProcess.InitialValues(Span{double})"/> and
/// <see cref="StochasticProcess.Evolve(double, ReadOnlySpan{double}, double, ReadOnlySpan{double}, Span{double})"/>
/// give the allocating forms' values bit for bit (the S-hes plan's HR2 (i), (ii)): on every discretization of
/// <see cref="HestonProcess"/>, the overrides for the truncation, reflection and quadratic-exponential schemes and the
/// fallback for the exact ones; on <see cref="BatesProcess"/>, its jumps added once; on every discretization of
/// <see cref="GJRGARCHProcess"/>; on both of <see cref="HybridHestonHullWhiteProcess"/>, its step terms below their cut
/// and above it; and, through the base's defaults, on landed processes that don't override them.
/// </summary>
public class StochasticProcessInPlaceTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);
    private static readonly YieldTermStructure Dividends = new FlatForward(Today, 0.02, A365);

    public static TheoryData<HestonProcessDiscretization> Discretizations() =>
        [.. Enum.GetValues<HestonProcessDiscretization>()];

    [Theory]
    [MemberData(nameof(Discretizations))]
    public void TheHestonInPlaceStepIsTheAllocatingOne(HestonProcessDiscretization d)
    {
        var process = new HestonProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.05, 0.5, -0.7, d);
        AssertSameSteps(process, 0.0, 0.25);
    }

    [Theory]
    [MemberData(nameof(Discretizations))]
    public void TheBatesInPlaceStepIsTheAllocatingOne(HestonProcessDiscretization d)
    {
        var process = new BatesProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.05, 0.5, -0.7, 2.0, -0.2, 0.1, d);
        AssertSameSteps(process, 0.0, 0.25);
    }

    [Theory]
    [InlineData(GJRGARCHProcessDiscretization.PartialTruncation)]
    [InlineData(GJRGARCHProcessDiscretization.FullTruncation)]
    [InlineData(GJRGARCHProcessDiscretization.Reflection)]
    public void TheGjrGarchInPlaceStepIsTheAllocatingOne(GJRGARCHProcessDiscretization d)
    {
        var process = new GJRGARCHProcess(RiskFree, Dividends, 50.0, 2.0e-4, 2.0e-6, 0.024, 0.93, 0.059, 0.1, 365.0, d);
        AssertSameSteps(process, 0.0, 1.0 / 365.0);
    }

    [Theory]
    [InlineData(HybridHestonHullWhiteProcessDiscretization.Euler, 0.05)]
    [InlineData(HybridHestonHullWhiteProcessDiscretization.Euler, 2.0)]
    [InlineData(HybridHestonHullWhiteProcessDiscretization.BSMHullWhite, 0.05)]
    [InlineData(HybridHestonHullWhiteProcessDiscretization.BSMHullWhite, 2.0)]
    public void TheHybridInPlaceStepIsTheAllocatingOne(HybridHestonHullWhiteProcessDiscretization d, double dt)
    {
        var process = new HybridHestonHullWhiteProcess(
            new HestonProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.05, 0.5, -0.7),
            new HullWhiteForwardProcess(RiskFree, 0.1, 0.01).WithForwardMeasureTime(20.0),
            -0.4,
            d);
        AssertSameSteps(process, 0.0, dt);
    }

    [Fact]
    public void LandedProcessesStepInPlaceThroughTheirAllocatingForms()
    {
        StochasticProcess[] processes =
        [
            new StochasticProcessArray(
                [new GeometricBrownianMotionProcess(100.0, 0.03, 0.2), new OrnsteinUhlenbeckProcess(0.3, 0.01, 0.02)],
                new Matrix(2, 2, 0.0) { [0, 0] = 1.0, [1, 1] = 1.0, [0, 1] = 0.3, [1, 0] = 0.3 }),
            new G2Process(0.1, 0.01, 0.2, 0.015, -0.7),
        ];
        foreach (StochasticProcess process in processes)
        {
            AssertSameSteps(process, 0.1, 0.05);
        }
    }

    // A few steps from the initial values, each from the last, by both forms, bit for bit.
    private static void AssertSameSteps(StochasticProcess process, double t0, double dt)
    {
        int n = process.Size;
        int factors = process.Factors;
        Vector allocating = process.InitialValues();
        double[] inPlace = new double[n];
        process.InitialValues(inPlace);
        Assert.Equal(allocating.AsSpan().ToArray(), inPlace);

        var rng = new InverseCumulativeRsg<MersenneTwisterUniformRsg, InverseCumulativeNormal>(
            new MersenneTwisterUniformRsg(factors, 1234), default);
        double t = t0;
        double[] next = new double[n];
        for (int step = 0; step < 6; ++step)
        {
            var dw = new Vector(rng.NextSequence().Value.Span);
            Vector expected;
            try
            {
                expected = process.Evolve(t, allocating, dt, dw);
            }
            catch (NanoQLException e)
            {
                // QuantLib's step refuses (Broadie and Kaya's trapezoidal root search): the in-place step the same way
                double[] state = [.. inPlace];
                NanoQLException again = Assert.Throws<NanoQLException>(() => process.Evolve(t, state, dt, dw.AsSpan(), next));
                Assert.Equal(e.Message, again.Message);
                return;
            }

            process.Evolve(t, inPlace, dt, dw.AsSpan(), next);

            Assert.Equal(expected.AsSpan().ToArray(), next);

            allocating = expected;
            (inPlace, next) = (next, inPlace);
            t += dt;
        }
    }
}
