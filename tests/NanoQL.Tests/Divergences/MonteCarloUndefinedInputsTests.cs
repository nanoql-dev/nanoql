// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Inputs for which QuantLib's time grid, path, Brownian bridge and path generator are undefined (they read or write
/// outside a vector, or call std::sort or std::lround outside their contracts) are refused explicitly in nanoql: an
/// <see cref="ArgumentOutOfRangeException"/> naming the caller's argument, or a <see cref="NanoQLException"/> where
/// the object itself is empty.
/// </summary>
public class MonteCarloUndefinedInputsTests
{
    [Fact]
    public void ABrownianBridgeOfNoStepOrAShortOutputIsRefused()
    {
        // brownianbridge.cpp:34-57 build size_ steps, 0 for BrownianBridge(0), an empty vector or a grid of one time,
        // and initialize() then reads sqrtdt_[0] and t_[size_-1] (lines 62, 73, 77); for the empty grid,
        // size_(timeGrid.size()-1) (line 51) wraps.
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => new BrownianBridge(0));
        Assert.Equal("steps", e.ParamName);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new BrownianBridge(-3));
        Assert.Equal("steps", e.ParamName);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new BrownianBridge(ReadOnlySpan<double>.Empty));
        Assert.Equal("times", e.ParamName);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new BrownianBridge(new TimeGrid([0.0])));
        Assert.Equal("timeGrid", e.ParamName);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new BrownianBridge(new TimeGrid()));
        Assert.Equal("timeGrid", e.ParamName);

        // brownianbridge.hpp:114-136 write output[0] to output[size_-1] unchecked.
        var bridge = new BrownianBridge(4);
        ArgumentException a = Assert.Throws<ArgumentException>(() => bridge.Transform([0.1, 0.2, 0.3, 0.4], new double[3]));
        Assert.Equal("output", a.ParamName);
    }

    [Fact]
    public void ATimeGridsUndefinedInputsAreRefused()
    {
        // timegrid.hpp:58 and 89 sort the times with std::sort, whose ordering a NaN breaks.
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => new TimeGrid([1.0, double.NaN]));
        Assert.Equal("times", e.ParamName);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new TimeGrid([1.0, double.NaN], 4));
        Assert.Equal("times", e.ParamName);

        // timegrid.hpp:129 rounds each period's steps with std::lround, outside its range for an infinite time (one
        // that close_enough keeps, as after 0: after a positive time it counts as a repeat) or, with no steps, a
        // shortest distance too small for the longest period.
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new TimeGrid([0.0, double.PositiveInfinity], 4));
        Assert.Equal("times", e.ParamName);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new TimeGrid([double.PositiveInfinity], 0));
        Assert.Equal("times", e.ParamName);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new TimeGrid([1e-300, 1.0], 0));
        Assert.Equal("times", e.ParamName);

        // timegrid.cpp:61-68 take j = i-1 for a NaN, which is neither before nor after the grid, at i = 0.
        var grid = new TimeGrid([1.0, 2.0, 5.0]);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => grid.Index(double.NaN));
        Assert.Equal("t", e.ParamName);

        // timegrid.cpp:62-75, again, on a grid whose first time is NaN, 0 times an infinite step: TimeGrid(1, 0) holds
        // [NaN] and TimeGrid(inf, 3) starts with it. index takes closestIndex 0, finds t neither before nor after the
        // NaN, and reads times_[Size(-1)], whatever t is. closestIndex (0), closestTime (the NaN) and dt are defined
        // there, as QuantLib's are.
        Assert.Throws<NanoQLException>(() => new TimeGrid(1.0, 0).Index(0.0));
        var infinite = new TimeGrid(double.PositiveInfinity, 3);
        Assert.Throws<NanoQLException>(() => infinite.Index(double.PositiveInfinity));
        Assert.Throws<NanoQLException>(() => infinite.Index(5.0));
        Assert.Equal(0, infinite.ClosestIndex(5.0));
        Assert.True(double.IsNaN(infinite.ClosestTime(5.0)));
        Assert.Equal(double.PositiveInfinity, infinite.Dt(0));

        // An empty grid's element reads: timegrid.cpp:45 (index), timegrid.hpp:154 (closestTime), 175-176 (front,
        // back). closestIndex, which reads no time, returns 0 as QuantLib's does.
        var empty = new TimeGrid();
        Assert.Throws<NanoQLException>(() => empty.Index(0.0));
        Assert.Throws<NanoQLException>(() => empty.ClosestTime(0.0));
        Assert.Throws<NanoQLException>(() => empty.Front());
        Assert.Throws<NanoQLException>(() => empty.Back());
        Assert.Equal(0, empty.ClosestIndex(1.0));
    }

    [Fact]
    public void AnEmptyPathsEndsAreRefused()
    {
        // path.hpp:122-124 and 130-132 read values_[0] and values_[values_.size()-1] of an empty Array.
        var path = new MonteCarloPath(new TimeGrid());
        Assert.Equal(0, path.Length);
        Assert.Throws<NanoQLException>(() => path.Front());
        Assert.Throws<NanoQLException>(() => path.Back());
    }

    [Fact]
    public void APathGeneratorOfNoStepIsRefused()
    {
        // pathgenerator.hpp:89 and 103 build bb_(timeGrid_), the Brownian bridge over the grid, before the dimension
        // check (lines 90-92 and 104-106): undefined for a grid of no step, whatever the generator's dimension.
        var process = new OrnsteinUhlenbeckProcess(0.1, 0.2);
        InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal> generator =
            PseudoRandom.MakeSequenceGenerator(12, 42);

        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new PathGenerator<InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal>>(
                process, 10.0, 0, generator, false));
        Assert.Equal("timeSteps", e.ParamName);
        e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new PathGenerator<InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal>>(
                process, new TimeGrid([0.0]), generator, false));
        Assert.Equal("timeGrid", e.ParamName);
        e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new PathGenerator<InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal>>(
                process, new TimeGrid(), generator, true));
        Assert.Equal("timeGrid", e.ParamName);
    }
}
