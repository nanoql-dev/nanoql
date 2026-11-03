// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="MakeMCVarianceSwapEngine{TRng, TStats}"/>'s seed is explicit: 0, QuantLib's seed from the clock, is
/// refused, naming the parameter. QuantLib's checks keep their texts: the steps given one way, the samples beside no
/// tolerance, a tolerance only where the traits estimate an error; a builder kept and reused is never changed; and the
/// engine's own checks are QuantLib's constructor's.
/// </summary>
public class MakeMcVarianceSwapEngineTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.01, A365), new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));

    private static readonly VarianceSwap Swap = new(PositionType.Long, 0.04, 50000.0, Today, Today + 365);

    [Fact]
    public void TheSeedIsRequiredAndZeroRefused()
    {
        Assert.Equal(
            "seed",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MakeMCVarianceSwapEngine<PseudoRandom, GeneralStatistics>(Process, 0)).ParamName);
        Assert.Equal(
            "seed",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MakeMCVarianceSwapEngine<PseudoRandom, GeneralStatistics>(Process, 42).WithSeed(0)).ParamName);
    }

    [Fact]
    public void QuantLibsChecksKeepTheirTexts()
    {
        var builder = new MakeMCVarianceSwapEngine<PseudoRandom, GeneralStatistics>(Process, 42);
        Assert.Equal("number of steps not given", Assert.Throws<NanoQLException>(() => builder.WithSamples(10).ToEngine()).Message);
        Assert.Equal(
            "number of steps overspecified",
            Assert.Throws<NanoQLException>(() => builder.WithSteps(2).WithStepsPerYear(4).ToEngine()).Message);
        Assert.Equal(
            "tolerance already set", Assert.Throws<NanoQLException>(() => builder.WithAbsoluteTolerance(0.1).WithSamples(10)).Message);
        Assert.Equal(
            "number of samples already set",
            Assert.Throws<NanoQLException>(() => builder.WithSamples(10).WithAbsoluteTolerance(0.1)).Message);
        Assert.Equal(
            "chosen random generator policy does not allow an error estimate",
            Assert.Throws<NanoQLException>(
                () => new MakeMCVarianceSwapEngine<LowDiscrepancy, GeneralStatistics>(Process, 42).WithAbsoluteTolerance(0.1)).Message);

        // kept and reused, the builder gives the same engine
        Func<VarianceSwap, EvaluationContext, VarianceSwapResults> engine = builder.WithSteps(12).WithSamples(255).ToEngine();
        Assert.Equal(engine(Swap, Context), builder.WithSteps(12).WithSamples(255).ToEngine()(Swap, Context));
        Assert.Equal(
            "neither tolerance nor number of samples set",
            Assert.Throws<NanoQLException>(() => builder.WithSteps(12).ToEngine()(Swap, Context)).Message);
    }

    [Fact]
    public void TheEnginesStepChecksAreQuantLibsConstructors()
    {
        ArgumentException none = Assert.Throws<ArgumentException>(() => MCVarianceSwapEngine.Calculate<PseudoRandom, GeneralStatistics>(
            Swap, Context, Process, null, null, false, false, 10, null, null, 42));
        Assert.StartsWith("no time steps provided", none.Message, StringComparison.Ordinal);
        ArgumentException both = Assert.Throws<ArgumentException>(() => MCVarianceSwapEngine.Calculate<PseudoRandom, GeneralStatistics>(
            Swap, Context, Process, 4, 12, false, false, 10, null, null, 42));
        Assert.StartsWith("both time steps and time steps per year were provided", both.Message, StringComparison.Ordinal);
        ArgumentException zero = Assert.Throws<ArgumentException>(() => MCVarianceSwapEngine.Calculate<PseudoRandom, GeneralStatistics>(
            Swap, Context, Process, 0, null, false, false, 10, null, null, 42));
        Assert.StartsWith("timeSteps must be positive, 0 not allowed", zero.Message, StringComparison.Ordinal);
    }
}
