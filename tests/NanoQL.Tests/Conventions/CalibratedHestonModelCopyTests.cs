// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The time-dependent and rough Heston models are immutable: <c>WithParams</c> returns a new model of the receiver's
/// type with the parameters, QuantLib's <c>setParams</c>, and leaves the receiver unchanged. The time-dependent model
/// keeps its curves, spot and grid; the rough model builds its process from the given process's curves and spot and the
/// new parameters, where its constructor keeps the given process, QuantLib's.
/// </summary>
public class CalibratedHestonModelCopyTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly YieldTermStructure RTS = new FlatForward(Today, 0.04, new Actual365Fixed());
    private static readonly YieldTermStructure QTS = new FlatForward(Today, 0.02, new Actual365Fixed());

    [Fact]
    public void TheTimeDependentModelsCopyTakesTheParameters()
    {
        var grid = new TimeGrid([0.5, 2.0]);
        var model = new PiecewiseTimeDependentHestonModel(
            RTS,
            QTS,
            100.0,
            0.04,
            new ConstantParameter(0.05, new PositiveConstraint()),
            new PiecewiseConstantParameter([0.5], new PositiveConstraint()).SetParam(0, 1.5).SetParam(1, 0.8),
            new ConstantParameter(0.4, new PositiveConstraint()),
            new ConstantParameter(-0.6, new BoundaryConstraint(-1.0, 1.0)),
            grid);

        Assert.Equal([0.05, 1.5, 0.8, 0.4, -0.6, 0.04], model.Params());

        var copy = (PiecewiseTimeDependentHestonModel)model.WithParams(new Vector([0.06, 2.0, 0.5, 0.3, -0.4, 0.09]));

        Assert.Equal(0.06, copy.Theta(1.0));
        Assert.Equal(2.0, copy.Kappa(0.1));
        Assert.Equal(0.5, copy.Kappa(1.0));
        Assert.Equal(0.3, copy.Sigma(0.0));
        Assert.Equal(-0.4, copy.Rho(0.0));
        Assert.Equal(0.09, copy.V0);
        Assert.Same(grid, copy.TimeGrid);
        Assert.Same(RTS, copy.RiskFreeRate);
        Assert.Same(QTS, copy.DividendYield);
        Assert.Equal(100.0, copy.S0);

        // the receiver unchanged
        Assert.Equal([0.05, 1.5, 0.8, 0.4, -0.6, 0.04], model.Params());
        Assert.Equal(1.5, model.Kappa(0.1));
    }

    [Fact]
    public void TheRoughModelsCopyBuildsItsProcess()
    {
        var given = new HestonProcess(RTS, QTS, 100.0, 0.04, 1.5, 0.05, 0.4, -0.6, HestonProcessDiscretization.FullTruncation);
        var model = new RoughHestonModel(given, 0.1);

        // the constructor keeps the given process, its discretization included
        Assert.Same(given, model.Process);
        Assert.Equal([0.05, 1.5, 0.4, -0.6, 0.04, 0.1], model.Params());

        var copy = (RoughHestonModel)model.WithParams(new Vector([0.06, 2.0, 0.3, -0.4, 0.09, 0.3]));

        Assert.Equal((0.06, 2.0, 0.3, -0.4, 0.09, 0.3), (copy.Theta, copy.Kappa, copy.Sigma, copy.Rho, copy.V0, copy.Hurst));
        HestonProcess process = copy.Process;
        Assert.Equal((0.09, 2.0, 0.06, 0.3, -0.4), (process.V0, process.Kappa, process.Theta, process.Sigma, process.Rho));
        Assert.Same(RTS, process.RiskFreeRate);
        Assert.Same(QTS, process.DividendYield);
        Assert.Equal(100.0, process.S0);

        // the receiver unchanged
        Assert.Same(given, model.Process);
        Assert.Equal(0.1, model.Hurst);
    }
}
