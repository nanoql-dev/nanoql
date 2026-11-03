// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the time grid's, path's, Brownian bridge's, path generator's and processes' refusals
/// (docs/porting-playbook.md §2), which a vector's <c>{"throws": true}</c> can't tell apart: an argument a member
/// refuses throws an <see cref="ArgumentException"/> or an <see cref="ArgumentOutOfRangeException"/> naming it, with
/// QuantLib's message; a failure of the call, a <see cref="NanoQLException"/> with QuantLib's message. A state,
/// increments, a result span, an index or a path that QuantLib reads past its end are refused before the read, and a
/// call just inside the bound still computes.
/// </summary>
public class MonteCarloErrorTests
{
    [Fact]
    public void TimeGridRefusals()
    {
        ArgumentOutOfRangeException range = Assert.Throws<ArgumentOutOfRangeException>(() => new TimeGrid(0.0, 4));
        Assert.Equal("end", range.ParamName);
        Assert.StartsWith("negative times not allowed", range.Message);

        ArgumentException e = Assert.Throws<ArgumentException>(() => new TimeGrid(ReadOnlySpan<double>.Empty));
        Assert.Equal("times", e.ParamName);
        Assert.StartsWith("empty time sequence", e.Message);
        e = Assert.Throws<ArgumentException>(() => new TimeGrid([-3.0, 1.0, 4.0, 5.0]));
        Assert.Equal("times", e.ParamName);
        Assert.StartsWith("negative times not allowed", e.Message);
        e = Assert.Throws<ArgumentException>(() => new TimeGrid([0.0], 0));
        Assert.Equal("times", e.ParamName);
        Assert.StartsWith("not enough distinct points in time grid", e.Message);

        var tg = new TimeGrid([1.0, 2.0, 5.0]);
        NanoQLException n = Assert.Throws<NanoQLException>(() => tg.Index(-0.1));
        Assert.Equal(
            "using inadequate time grid: all nodes are later than the required time t = -0.1 (earliest node is t1 = 0)",
            n.Message);
        n = Assert.Throws<NanoQLException>(() => tg.Index(5.1));
        Assert.Equal(
            "using inadequate time grid: all nodes are earlier than the required time t = 5.1 (latest node is t1 = 5)",
            n.Message);
        n = Assert.Throws<NanoQLException>(() => tg.Index(2.9));
        Assert.Equal(
            "using inadequate time grid: the nodes closest to the required time t = 2.9 are t1 = 2 and t2 = 5",
            n.Message);
        n = Assert.Throws<NanoQLException>(() => new TimeGrid(1.0, 0).Index(0.0));
        Assert.Equal("using inadequate time grid: its first node is NaN", n.Message);
    }

    [Fact]
    public void MonteCarloPathRefusals()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new MonteCarloPath(new TimeGrid(1.0, 4), [1.0, 2.0]));
        Assert.Equal("values", e.ParamName);
        Assert.StartsWith("different number of times and asset values", e.Message);

        // Path::at is Array::at, with its message.
        var path = new MonteCarloPath(new TimeGrid(1.0, 2), [1.0, 2.0, 3.0]);
        ArgumentOutOfRangeException range = Assert.Throws<ArgumentOutOfRangeException>(() => path.At(9));
        Assert.Equal("i", range.ParamName);
        Assert.StartsWith("index (9) must be less than 3: array access out of range", range.Message);
    }

    [Fact]
    public void PathGeneratorRefusals()
    {
        var process = new OrnsteinUhlenbeckProcess(0.1, 0.2);
        InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal> generator =
            PseudoRandom.MakeSequenceGenerator(11, 42);

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new InvCumulativeMersenneTwisterPathGenerator(process, 10.0, 12, generator, false));
        Assert.Equal("generator", e.ParamName);
        Assert.StartsWith("sequence generator dimensionality (11) != timeSteps (12)", e.Message);
        e = Assert.Throws<ArgumentException>(
            () => new InvCumulativeMersenneTwisterPathGenerator(process, new TimeGrid(10.0, 12), generator, false));
        Assert.Equal("generator", e.ParamName);
        Assert.StartsWith("sequence generator dimensionality (11) != timeSteps (12)", e.Message);

        // QuantLib builds timeGrid_(length, timeSteps) first: a length that isn't positive is refused before no step.
        ArgumentOutOfRangeException range = Assert.Throws<ArgumentOutOfRangeException>(
            () => new InvCumulativeMersenneTwisterPathGenerator(process, 0.0, 0, generator, false));
        Assert.Equal("end", range.ParamName);
        Assert.StartsWith("negative times not allowed", range.Message);
    }

    [Fact]
    public void BrownianBridgeEmptyGridRefusal()
    {
        // QuantLib's size_(timeGrid.size()-1) wraps to the largest Size for the empty grid, and allocating the members
        // throws std::length_error (brownianbridge.cpp:51-52; the wheel's "vector too long").
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => new BrownianBridge(new TimeGrid()));
        Assert.Equal("timeGrid", e.ParamName);
        Assert.StartsWith("a Brownian bridge needs a time grid of at least two times, not 0", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BrownianBridgeTransformRefusal()
    {
        var bridge = new BrownianBridge(4);
        ArgumentException e = Assert.Throws<ArgumentException>(() => bridge.Transform([0.1, 0.2, 0.3], new double[4]));
        Assert.Equal("input", e.ParamName);
        Assert.StartsWith("incompatible sequence size", e.Message);
    }

    [Fact]
    public void OrnsteinUhlenbeckProcessVolatilityRefusal()
    {
        foreach (double vol in new[] { -0.01, double.NaN })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
                () => new OrnsteinUhlenbeckProcess(0.1, vol));
            Assert.Equal("vol", e.ParamName);
            Assert.StartsWith("negative volatility given", e.Message);
        }
    }

    [Fact]
    public void StochasticProcessTimeRefusals()
    {
        StochasticProcess process = new OrnsteinUhlenbeckProcess(0.1, 0.2);

        ArgumentException e = Assert.Throws<ArgumentException>(() => process.Time(default));
        Assert.Equal("date", e.ParamName);
        Assert.StartsWith("default(Date) is not a valid date", e.Message);

        NanoQLException n = Assert.Throws<NanoQLException>(() => process.Time(new Date(17, Month.October, 2026)));
        Assert.Equal("date/time conversion not supported", n.Message);
    }

    [Fact]
    public void MultiPathAndItsGeneratorRefusals()
    {
        ArgumentOutOfRangeException o = Assert.Throws<ArgumentOutOfRangeException>(() => new MultiPath(0, new TimeGrid(1.0, 2)));
        Assert.Equal("nAsset", o.ParamName);
        Assert.StartsWith("number of asset must be positive", o.Message);

        var array = new StochasticProcessArray(
            [new OrnsteinUhlenbeckProcess(0.1, 0.2), new OrnsteinUhlenbeckProcess(0.2, 0.1)],
            new Matrix(new[,] { { 1.0, 0.3 }, { 0.3, 1.0 } }));
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new MultiPathGenerator<IRsg>(array, new TimeGrid(1.0, 4), PseudoRandom.MakeSequenceGenerator(7, 42)));
        Assert.Equal("generator", e.ParamName);
        Assert.StartsWith(
            "dimension (7) is not equal to (2 * 4) the number of factors times the number of time steps", e.Message);
        e = Assert.Throws<ArgumentException>(
            () => new MultiPathGenerator<IRsg>(array, new TimeGrid([0.0]), PseudoRandom.MakeSequenceGenerator(1, 42)));
        Assert.Equal("generator", e.ParamName);
        Assert.StartsWith("dimension (1) is not equal to (2 * 0)", e.Message);
    }

    [Fact]
    public void MonteCarloModelAndSimulationRefusals()
    {
        var grid = new TimeGrid(1.0, 2);
        IPathGenerator<MonteCarloPath> generator = SingleVariate<PseudoRandom>.MakePathGenerator(
            new OrnsteinUhlenbeckProcess(0.1, 0.2), grid, PseudoRandom.MakeSequenceGenerator(2, 42), false);
        var model = new MonteCarloModel<MonteCarloPath, GeneralStatistics>(
            generator, new BackPricer(), new GeneralStatistics(), false);
        ArgumentOutOfRangeException o = Assert.Throws<ArgumentOutOfRangeException>(() => model.AddSamples(-1));
        Assert.Equal("samples", o.ParamName);
        o = Assert.Throws<ArgumentOutOfRangeException>(
            () => McSimulation<MonteCarloPath, GeneralStatistics>.Value(model, 0.1, -1));
        Assert.Equal("maxSamples", o.ParamName);
        o = Assert.Throws<ArgumentOutOfRangeException>(
            () => McSimulation<MonteCarloPath, GeneralStatistics>.Value(model, 0.1, 100, -1));
        Assert.Equal("minSamples", o.ParamName);
    }

    [Fact]
    public void StochasticProcessArrayRefusals()
    {
        var correlation = new Matrix(new[,] { { 1.0, 0.3 }, { 0.3, 1.0 } });
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new StochasticProcessArray([], new Matrix(new[,] { { 1.0 } })));
        Assert.Equal("processes", e.ParamName);
        Assert.StartsWith("no processes given", e.Message);
        e = Assert.Throws<ArgumentException>(
            () => new StochasticProcessArray([new OrnsteinUhlenbeckProcess(0.1, 0.2)], correlation));
        Assert.Equal("correlation", e.ParamName);
        Assert.StartsWith("mismatch between number of processes and size of correlation matrix", e.Message);
        e = Assert.Throws<ArgumentException>(
            () => new StochasticProcessArray([new OrnsteinUhlenbeckProcess(0.1, 0.2), null!], correlation));
        Assert.Equal("processes", e.ParamName);
        Assert.StartsWith("null 1-D stochastic process", e.Message);

        // The correlation's square root comes first, as QuantLib's member initialisers take it first.
        e = Assert.Throws<ArgumentException>(() => new StochasticProcessArray([], new Matrix()));
        Assert.Equal("matrix", e.ParamName);
        Assert.StartsWith("matrix must not be empty", e.Message);
    }

    [Fact]
    public void BatesProcessIncrementRefusals()
    {
        // The jumps read the two increments after Heston's, which QuantLib reads past a shorter vector's end.
        var curve = new FlatForward(new Date(15, Month.October, 2026), 0.05, new Actual365Fixed());
        var process = new BatesProcess(curve, curve, 100.0, 0.04, 1.5, 0.05, 0.5, -0.7, 2.0, -0.2, 0.1);
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => process.Evolve(0.0, new Vector([100.0, 0.04]), 0.1, new Vector([0.1, 0.2, 0.3])));
        Assert.Equal("dw", e.ParamName);
        Assert.StartsWith("3 increments given for 4 factors", e.Message, StringComparison.Ordinal);
        double[] result = new double[2];
        e = Assert.Throws<ArgumentException>(() => process.Evolve(0.0, [100.0, 0.04], 0.1, [0.1, 0.2, 0.3], result));
        Assert.Equal("dw", e.ParamName);
        Assert.StartsWith("3 increments given for 4 factors", e.Message, StringComparison.Ordinal);
        Assert.Equal(2, process.Evolve(0.0, new Vector([100.0, 0.04]), 0.1, new Vector([0.1, 0.2, 0.3, 0.4])).Size);
    }

    [Fact]
    public void StochasticProcessArrayStateRefusals()
    {
        // A state of fewer values than the processes, which QuantLib reads one per process, past its end; a longer one
        // is read as far as the processes go, as QuantLib reads it.
        var array = new StochasticProcessArray(
            [new OrnsteinUhlenbeckProcess(0.1, 0.2), new OrnsteinUhlenbeckProcess(0.2, 0.3)], new Matrix(new[,] { { 1.0, 0.3 }, { 0.3, 1.0 } }));
        var shorter = new Vector([0.5]);
        var two = new Vector([0.5, 1.0]);
        foreach ((string param, Action call) in new (string, Action)[]
                 {
                     ("x", () => array.Drift(0.0, shorter)),
                     ("x", () => array.Diffusion(0.0, shorter)),
                     ("x0", () => array.Expectation(0.0, shorter, 0.1)),
                     ("x0", () => array.StdDeviation(0.0, shorter, 0.1)),
                     ("x0", () => array.Covariance(0.0, shorter, 0.1)),
                     ("x0", () => array.Evolve(0.0, shorter, 0.1, two)),
                     ("x0", () => array.Apply(shorter, two)),
                     ("dx", () => array.Apply(two, shorter)),
                 })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(call);
            Assert.Equal(param, e.ParamName);
            Assert.StartsWith("1 values given for 2 processes", e.Message, StringComparison.Ordinal);
        }

        Assert.Equal(array.Drift(0.0, two).AsSpan().ToArray(), array.Drift(0.0, new Vector([0.5, 1.0, 2.0])).AsSpan().ToArray());
    }

    [Fact]
    public void ProcessStateIncrementAndResultRefusals()
    {
        // A state of fewer values than the process's size, fewer increments than its factors or a shorter result span,
        // which QuantLib reads past their ends; a state of the size still computes.
        var today = new Date(15, Month.October, 2026);
        var curve = new FlatForward(today, 0.05, new Actual365Fixed());
        var heston = new HestonProcess(curve, curve, 100.0, 0.04, 1.5, 0.05, 0.5, -0.7);
        var bates = new BatesProcess(curve, curve, 100.0, 0.04, 1.5, 0.05, 0.5, -0.7, 2.0, -0.2, 0.1);
        var slv = new HestonSLVProcess(heston, new LocalConstantVol(today, 1.1, new Actual365Fixed()), 0.7);
        HybridHestonHullWhiteProcess hybrid = Hybrid(curve);
        var gjr = new GJRGARCHProcess(curve, curve, 50.0, 2.0e-4, 2.0e-6, 0.024, 0.93, 0.059, 0.1, 365.0);
        var g2 = new G2Process(0.1, 0.01, 0.2, 0.015, -0.7);
        G2ForwardProcess g2Forward = new G2ForwardProcess(0.1, 0.01, 0.2, 0.013, -0.5, curve).WithForwardMeasureTime(5.0);
        var one = new Vector([100.0]);
        var two = new Vector([100.0, 0.04]);
        var three = new Vector([100.0, 0.04, 0.01]);
        const string Short = "1 values given for a state of 2";
        const string ShortOfThree = "2 values given for a state of 3";
        const string Fewer = "1 increments given for 2 factors";
        const string FewerOfThree = "2 increments given for 3 factors";
        const string Result = "the result is shorter than the process's state";
        AssertRefusals(
            ("result", Result, () => heston.InitialValues(new double[1])),
            ("x", Short, () => heston.Drift(0.0, one)),
            ("x", Short, () => heston.Diffusion(0.0, one)),
            ("x0", Short, () => heston.Apply(one, two)),
            ("dx", Short, () => heston.Apply(two, one)),
            ("x", Short, () => bates.Drift(0.0, one)),
            ("x", Short, () => slv.Drift(0.0, one)),
            ("x", Short, () => slv.Diffusion(0.0, one)),
            ("x0", Short, () => slv.Evolve(0.0, one, 0.1, two)),
            ("x0", Short, () => slv.Evolve(0.0, [100.0], 0.1, [0.1, 0.2], new double[2])),
            ("dw", Fewer, () => slv.Evolve(0.0, [100.0, 0.04], 0.1, [0.1], new double[2])),
            ("result", Result, () => slv.Evolve(0.0, [100.0, 0.04], 0.1, [0.1, 0.2], new double[1])),
            ("result", Result, () => hybrid.InitialValues(new double[2])),
            ("x", ShortOfThree, () => hybrid.Drift(0.0, two)),
            ("x", "1 values given for the Heston process's state of 2", () => hybrid.Diffusion(0.0, one)),
            ("x0", ShortOfThree, () => hybrid.Apply(two, three)),
            ("dx", ShortOfThree, () => hybrid.Apply(three, two)),
            ("x0", ShortOfThree, () => hybrid.Evolve(0.0, two, 0.1, three)),
            ("dw", FewerOfThree, () => hybrid.Evolve(0.0, three, 0.1, two)),
            ("x0", ShortOfThree, () => hybrid.Evolve(0.0, [100.0, 0.04], 0.1, [0.1, 0.2, 0.3], new double[3])),
            ("dw", FewerOfThree, () => hybrid.Evolve(0.0, [100.0, 0.04, 0.01], 0.1, [0.1, 0.2], new double[3])),
            ("result", Result, () => hybrid.Evolve(0.0, [100.0, 0.04, 0.01], 0.1, [0.1, 0.2, 0.3], new double[2])),
            ("x", ShortOfThree, () => hybrid.Numeraire(0.5, two)),
            ("result", Result, () => gjr.InitialValues(new double[1])),
            ("x", Short, () => gjr.Drift(0.0, one)),
            ("x", Short, () => gjr.Diffusion(0.0, one)),
            ("x0", Short, () => gjr.Apply(one, two)),
            ("dx", Short, () => gjr.Apply(two, one)),
            ("x0", Short, () => gjr.Evolve(0.0, one, 0.1, two)),
            ("dw", Fewer, () => gjr.Evolve(0.0, two, 0.1, one)),
            ("x0", Short, () => gjr.Evolve(0.0, [100.0], 0.1, [0.1, 0.2], new double[2])),
            ("dw", Fewer, () => gjr.Evolve(0.0, [100.0, 0.04], 0.1, [0.1], new double[2])),
            ("result", Result, () => gjr.Evolve(0.0, [100.0, 0.04], 0.1, [0.1, 0.2], new double[1])),
            ("x", Short, () => g2.Drift(0.0, one)),
            ("x0", Short, () => g2.Expectation(0.0, one, 0.1)),
            ("x0", Short, () => g2.StdDeviation(0.0, one, 0.1)),
            ("x0", Short, () => g2.Covariance(0.0, one, 0.1)),
            ("x", Short, () => g2Forward.Drift(0.0, one)),
            ("x0", Short, () => g2Forward.Expectation(0.0, one, 0.1)),
            ("x0", Short, () => g2Forward.StdDeviation(0.0, one, 0.1)),
            ("x0", Short, () => g2Forward.Covariance(0.0, one, 0.1)));

        // The check is the step's, before each scheme's own: the exact schemes draw a third increment.
        foreach (HestonProcessDiscretization d in Enum.GetValues<HestonProcessDiscretization>())
        {
            var p = new HestonProcess(curve, curve, 100.0, 0.04, 1.5, 0.05, 0.5, -0.7, d);
            int f = p.Factors;
            string fewer = $"{f - 1} increments given for {f} factors";
            AssertRefusals(
                ("x0", Short, () => p.Evolve(0.0, one, 0.1, new Vector(f, 0.1))),
                ("dw", fewer, () => p.Evolve(0.0, two, 0.1, new Vector(f - 1, 0.1))),
                ("x0", Short, () => p.Evolve(0.0, [100.0], 0.1, new double[f], new double[2])),
                ("dw", fewer, () => p.Evolve(0.0, [100.0, 0.04], 0.1, new double[f - 1], new double[2])),
                ("result", Result, () => p.Evolve(0.0, [100.0, 0.04], 0.1, new double[f], new double[1])));
            Assert.Equal(2, p.Evolve(0.0, two, 0.1, new Vector(f, 0.1)).Size);
        }

        double[] initial = new double[3];
        hybrid.InitialValues(initial);
        Assert.Equal(hybrid.InitialValues().ToArray(), initial);
        Assert.Equal(
            (2, 2, 3, 2, 2, 2),
            (heston.Drift(0.0, two).Size, slv.Diffusion(0.0, two).Rows, hybrid.Drift(0.0, three).Size,
                gjr.Apply(two, two).Size, g2.Expectation(0.0, two, 0.1).Size, g2Forward.StdDeviation(0.0, two, 0.1).Rows));
        Assert.True(hybrid.Numeraire(0.5, three) > 0.0);

        // The hybrid's diffusion reads the Heston process's two values, its third row the short rate's own.
        Assert.Equal(hybrid.Diffusion(0.0, three).AsSpan().ToArray(), hybrid.Diffusion(0.0, two).AsSpan().ToArray());
    }

    [Fact]
    public void OneDimensionalProcessVectorFormsRefuseAnEmptyState()
    {
        // StochasticProcess1D's vector forms read the state's first value and the first increment, which QuantLib reads
        // past an empty vector's end: through every 1-D process, and through both Euler discretizations.
        var ou = new OrnsteinUhlenbeckProcess(0.1, 0.2);
        var empty = new Vector(0);
        var one = new Vector([0.5]);
        var euler = new EulerDiscretization();
        var endEuler = new EndEulerDiscretization();
        const string NoState = "0 values given for a state of 1";
        AssertRefusals(
            ("x", NoState, () => ou.Drift(0.0, empty)),
            ("x", NoState, () => ou.Diffusion(0.0, empty)),
            ("x0", NoState, () => ou.Expectation(0.0, empty, 0.1)),
            ("x0", NoState, () => ou.StdDeviation(0.0, empty, 0.1)),
            ("x0", NoState, () => ou.Covariance(0.0, empty, 0.1)),
            ("x0", NoState, () => ou.Evolve(0.0, empty, 0.1, one)),
            ("dw", "0 increments given for 1 factors", () => ou.Evolve(0.0, one, 0.1, empty)),
            ("x0", NoState, () => ou.Apply(empty, one)),
            ("dx", NoState, () => ou.Apply(one, empty)),
            ("x", NoState, () => euler.Drift(ou, 0.0, empty, 0.1)),
            ("x", NoState, () => euler.Diffusion(ou, 0.0, empty, 0.1)),
            ("x", NoState, () => euler.Covariance(ou, 0.0, empty, 0.1)),
            ("x", NoState, () => endEuler.Drift(ou, 0.0, empty, 0.1)),
            ("x", NoState, () => endEuler.Diffusion(ou, 0.0, empty, 0.1)),
            ("x", NoState, () => endEuler.Covariance(ou, 0.0, empty, 0.1)));

        Assert.Equal([ou.Evolve(0.0, 0.5, 0.1, 0.3)], ou.Evolve(0.0, one, 0.1, new Vector([0.3])).ToArray());
        Assert.Equal([ou.Drift(0.0, 0.5) * 0.1], euler.Drift(ou, 0.0, one, 0.1).ToArray());
    }

    [Fact]
    public void JumpProcessStateIncrementAndResultRefusals()
    {
        // The extended Ornstein-Uhlenbeck process with jumps reads its state's two values, and its third increment only
        // when a jump falls in the step: fewer than its three increments are refused on every step, whether a jump
        // falls or not; its diffusion reads one value. Kluge's reads the jump process's state and factors before its
        // own last value and increment, and its diffusion the whole state.
        var power = new ExtOUWithJumpsProcess(new ExtendedOrnsteinUhlenbeckProcess(1.0, 0.5, 0.0, _ => 0.0), 0.5, 3.0, 1e6, 2.0);
        var calm = new ExtOUWithJumpsProcess(new ExtendedOrnsteinUhlenbeckProcess(1.0, 0.5, 0.0, _ => 0.0), 0.5, 3.0, 1e-6, 2.0);
        var kluge = new KlugeExtOUProcess(0.7, power, new ExtendedOrnsteinUhlenbeckProcess(4.45, 1.1, 0.0, _ => 0.0));
        var one = new Vector([0.5]);
        var two = new Vector([0.5, 0.1]);
        const string Result = "the result is shorter than the process's state";
        AssertRefusals(
            ("result", Result, () => power.InitialValues(new double[1])),
            ("x", "1 values given for a state of 2", () => power.Drift(0.0, one)),
            ("x", "no value given for the state", () => power.Diffusion(0.0, new Vector(0))),
            ("x0", "1 values given for a state of 2", () => power.Evolve(0.0, [0.5], 0.1, [0.1, 0.2, 0.3], new double[2])),
            ("x0", "1 values given for a state of 2", () => power.Evolve(0.0, one, 0.1, new Vector([0.1, 0.2, 0.3]))),
            ("dw", "2 increments given for 3 factors", () => power.Evolve(0.0, [0.5, 0.1], 0.1, [0.1, 0.2], new double[2])),
            ("dw", "2 increments given for 3 factors", () => calm.Evolve(0.0, [0.5, 0.1], 0.1, [0.1, 0.2], new double[2])),
            ("result", Result, () => power.Evolve(0.0, [0.5, 0.1], 0.1, [0.1, 0.2, 0.3], new double[1])),
            ("result", Result, () => kluge.InitialValues(new double[2])),
            ("x", "1 values given for the jump process's state of 2", () => kluge.Drift(0.0, one)),
            ("x", "2 values given for a state of 3", () => kluge.Diffusion(0.0, two)),
            ("x0", "1 values given for the jump process's state of 2", () => kluge.Evolve(0.0, [0.5], 0.1, [0.1, 0.2, 0.3, 0.4], new double[3])),
            ("dw", "2 increments given for the jump process's 3 factors", () => kluge.Evolve(0.0, [0.5, 0.1, 0.2], 0.1, [0.1, 0.2], new double[3])),
            ("result", Result, () => kluge.Evolve(0.0, [0.5, 0.1, 0.2], 0.1, [0.1, 0.2, 0.3, 0.4], new double[2])));

        // One value is the diffusion's whole read, two the Kluge drift's and step's; three increments step with a jump
        // and without one.
        Assert.Equal(2, power.Diffusion(0.0, one).Rows);
        Assert.Equal(3, kluge.Drift(0.0, two).Size);
        double[] jumped = new double[2];
        double[] still = new double[2];
        power.Evolve(0.0, [0.5, 0.1], 0.1, [0.1, 0.2, 0.3], jumped);
        calm.Evolve(0.0, [0.5, 0.1], 0.1, [0.1, 0.2, 0.3], still);
        Assert.True(jumped[1] > still[1]);
        double[] gas = new double[3];
        kluge.Evolve(0.0, [0.5, 0.1], 0.1, [0.1, 0.2, 0.3], gas);
        Assert.True(double.IsFinite(gas[2]));
    }

    [Fact]
    public void PathGridAndProcessArrayAccessorRefusals()
    {
        // QuantLib's unchecked operator[] and its kin beside a checked at: an index past the array, or a negative one,
        // is refused with Array::at's text, as the checked twins refuse it.
        var grid = new TimeGrid(1.0, 2);
        var path = new MonteCarloPath(grid, [1.0, 2.0, 3.0]);
        var multiPath = new MultiPath([path, path]);
        var array = new StochasticProcessArray(
            [new OrnsteinUhlenbeckProcess(0.1, 0.2), new OrnsteinUhlenbeckProcess(0.2, 0.3)], new Matrix(new[,] { { 1.0, 0.3 }, { 0.3, 1.0 } }));
        foreach ((string param, int index, int length, Action call) in new (string, int, int, Action)[]
                 {
                     ("i", 3, 3, () => _ = path[3]),
                     ("i", -1, 3, () => _ = path[-1]),
                     ("i", 3, 3, () => path.Value(3)),
                     ("i", -1, 3, () => path.Value(-1)),
                     ("i", 3, 3, () => path.Time(3)),
                     ("i", -1, 3, () => path.Time(-1)),
                     ("j", 2, 2, () => _ = multiPath[2]),
                     ("j", -1, 2, () => _ = multiPath[-1]),
                     ("i", 3, 3, () => _ = grid[3]),
                     ("i", -1, 3, () => _ = grid[-1]),
                     ("i", 2, 2, () => grid.Dt(2)),
                     ("i", -1, 2, () => grid.Dt(-1)),
                     ("i", 2, 2, () => array.Process(2)),
                     ("i", -1, 2, () => array.Process(-1)),
                 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(call);
            Assert.Equal(param, e.ParamName);
            Assert.StartsWith($"index ({index}) must be less than {length}: array access out of range", e.Message, StringComparison.Ordinal);
        }

        Assert.Equal((3.0, 3.0, 1.0, 1.0, 0.5), (path[2], path.Value(2), path.Time(2), grid[2], grid.Dt(1)));
        Assert.Equal(3.0, multiPath[1][2]);
        Assert.NotNull(array.Process(1));
    }

    [Fact]
    public void PathPricerRefusals()
    {
        // The pricers read the path at a time or a fixing that QuantLib reads unchecked, past its end; the hybrid's
        // reads its third asset, the short rate's.
        var path = new MonteCarloPath(new TimeGrid(1.0, 2), [1.0, 2.0, 3.0]);
        var two = new MultiPath([path, path]);
        var american = new AmericanPathPricer(new PlainVanillaPayoff(OptionType.Put, 1.0), 2, LsmBasisSystemPolynomialType.Monomial);
        var basket = new AmericanBasketPathPricer(2, new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 1.0)));
        foreach ((string message, Action call) in new (string, Action)[]
                 {
                     ("t ('3') must be less than '3'.", () => american.State(path, 3)),
                     ("t ('-1') must be a non-negative value.", () => american.State(path, -1)),
                     ("t ('3') must be less than '3'.", () => american.Evaluate(path, 3)),
                     ("t ('3') must be less than '3'.", () => basket.State(two, 3)),
                     ("t ('-1') must be a non-negative value.", () => basket.State(two, -1)),
                     ("t ('3') must be less than '3'.", () => basket.Evaluate(two, 3)),
                 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(call);
            Assert.Equal("t", e.ParamName);
            Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
        }

        Assert.Equal(2, basket.State(two, 2).Size);
        Assert.True(american.State(path, 2) > 0.0);

        var curve = new FlatForward(new Date(15, Month.October, 2026), 0.05, new Actual365Fixed());
        var hybrid = new HestonHullWhitePathPricer(1.0, new PlainVanillaPayoff(OptionType.Call, 1.0), Hybrid(curve));
        AssertRefusals(("path", "a path of 2 assets given for the hybrid's 3", () => hybrid.Evaluate(two)));
        Assert.True(double.IsFinite(hybrid.Evaluate(new MultiPath([path, path, path]))));

        // A fixing past the path's last point.
        ImmutableArray<int> past = [1, 2, 4];
        ImmutableArray<int> within = [1, 2];
        AssertRefusals(
            ("path", "the path has 3 points, the fixings reach 4", () => new ArithmeticAPOHestonPathPricer(OptionType.Call, 1.0, 0.95, past, 2.0, 2).Evaluate(two)),
            ("path", "the path has 3 points, the fixings reach 4", () => new GeometricAPOHestonPathPricer(OptionType.Call, 1.0, 0.95, past, 2.0, 2).Evaluate(two)));
        Assert.True(new ArithmeticAPOHestonPathPricer(OptionType.Call, 1.0, 0.95, within, 2.0, 2).Evaluate(two) > 0.0);
        Assert.True(new GeometricAPOHestonPathPricer(OptionType.Call, 1.0, 0.95, within, 2.0, 2).Evaluate(two) > 0.0);
    }

    private static HybridHestonHullWhiteProcess Hybrid(YieldTermStructure curve) =>
        new(
            new HestonProcess(curve, curve, 100.0, 0.04, 1.5, 0.05, 0.5, -0.7),
            new HullWhiteForwardProcess(curve, 0.1, 0.01).WithForwardMeasureTime(2.0),
            -0.4);

    // Each call refused with an ArgumentException naming the parameter, its message starting as given.
    private static void AssertRefusals(params (string Param, string Message, Action Call)[] cases)
    {
        foreach ((string param, string message, Action call) in cases)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(call);
            Assert.Equal(param, e.ParamName);
            Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
        }
    }

    private sealed class BackPricer : PathPricer<MonteCarloPath>
    {
        public override double Evaluate(MonteCarloPath path) => path.Back();
    }
}
