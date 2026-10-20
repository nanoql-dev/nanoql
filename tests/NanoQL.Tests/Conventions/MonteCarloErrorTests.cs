// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the time grid's, path's, Brownian bridge's, path generator's and processes' refusals
/// (docs/porting-playbook.md §2), which a vector's <c>{"throws": true}</c> can't tell apart: an argument a member
/// refuses throws an <see cref="ArgumentException"/> or an <see cref="ArgumentOutOfRangeException"/> naming it, with
/// QuantLib's message; a failure of the call, a <see cref="NanoQLException"/> with QuantLib's message.
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
}
