// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The short-rate models' and their processes' public members that no vector or QuantLib case reaches, each against a
/// form the vectors check: the square-root process's standard deviation, Black-Karasinski's dynamics, G2's dynamics'
/// process and its bond option with a start date, and the two-dimensional tree's grid, which QuantLib refuses.
/// </summary>
public class ShortRateMemberTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void TheSquareRootProcessStandardDeviationIsItsVariancesRoot()
    {
        var process = new CoxIngersollRossProcess(0.3, 0.1, 0.04, 0.05);
        foreach ((double t0, double x0, double dt) in new[] { (0.0, 0.04, 0.25), (1.0, 0.02, 1.0), (2.5, 0.07, 0.01) })
        {
            Assert.Equal(Math.Sqrt(process.Variance(t0, x0, dt)), process.StdDeviation(t0, x0, dt));
        }
    }

    [Fact]
    public void BlackKarasinskisDynamicsMapARateToItsVariableAndBack()
    {
        // the fitting is set at the times of the 50-step tree over the curve's range, as QuantLib's
        YieldTermStructure curve = new ZeroCurve(
            [Today, Today + new Period(1, TimeUnit.Years), Today + new Period(10, TimeUnit.Years)], [0.02, 0.025, 0.03], new Actual365Fixed());
        var model = new BlackKarasinski(curve, 0.1, 0.1);
        OneFactorModelShortRateDynamics dynamics = model.Dynamics();
        var grid = new TimeGrid(curve.MaxTime, 50);
        foreach (double t in new[] { grid[0], grid[1], grid[10], grid[49] })
        {
            foreach (double r in new[] { 0.01, 0.03, 0.08 })
            {
                double back = dynamics.ShortRate(t, dynamics.Variable(t, r));
                Assert.True(Math.Abs((back / r) - 1.0) <= 1e-15, $"{t}, {r}: {back}");
            }
        }

        // the state variable is an Ornstein-Uhlenbeck process at the model's reversion and volatility
        var process = Assert.IsType<OrnsteinUhlenbeckProcess>(dynamics.Process);
        Assert.Equal(0.1, process.Speed);
        Assert.Equal(0.1, process.Volatility);
    }

    [Fact]
    public void G2sDynamicsProcessIsTheCorrelatedPair()
    {
        var model = new G2(Curve, 0.07, 0.011, 0.3, 0.008, -0.6);
        StochasticProcess process = model.Dynamics().Process();
        Assert.Equal(2, process.Size);
        Matrix covariance = process.Covariance(0.0, process.InitialValues(), 0.25);
        double correlation = covariance[0, 1] / Math.Sqrt(covariance[0, 0] * covariance[1, 1]);
        Assert.True(Math.Abs(correlation - (-0.6)) <= 1e-12, $"{correlation}");
    }

    [Fact]
    public void G2sBondOptionWithAStartIsTheOptionOnTheBond()
    {
        // QuantLib's AffineModel reads a bond option with a start date as the option on the bond, the start ignored
        var model = new G2(Curve, 0.07, 0.011, 0.3, 0.008, -0.6);
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            Assert.Equal(
                model.DiscountBondOption(type, 0.95, 1.0, 3.0),
                model.DiscountBondOption(type, 0.95, 1.0, 1.25, 3.0));
        }
    }

    [Fact]
    public void TheTwoDimensionalTreesGridIsRefused()
    {
        var model = new G2(Curve, 0.07, 0.011, 0.3, 0.008, -0.6);
        var tree = (TreeLattice2D)model.Tree(new TimeGrid(1.0, 10));
        Assert.Equal("not implemented", Assert.Throws<NanoQLException>(() => tree.Grid(0.5)).Message);
    }
}
