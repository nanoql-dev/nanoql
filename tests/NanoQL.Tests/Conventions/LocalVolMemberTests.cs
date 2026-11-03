// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The members of the local-volatility units that QuantLib's cases and the vectors don't read: the local-volatility
/// density calculator's time grid, the discrete Simpson Fourier integration, the grid model's arguments and constraint,
/// and the piecewise variance surface's one-section constructor.
/// </summary>
public class LocalVolMemberTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void TheLocalVolDensityCalculatorsTimeGridIsTheOneItSolvesOn()
    {
        double[] times = [0.25, 0.5, 1.0];
        double[] strikes = [1.0, 20.0, 50.0, 100.0, 200.0, 500.0, 10000.0];
        var surface = new FixedLocalVolSurface(Today, times, strikes, new Matrix(7, 3, 0.25), Dc);
        var r = new FlatForward(Today, 0.02, Dc);
        var q = new FlatForward(Today, 0.01, Dc);

        var byCount = new LocalVolRNDCalculator(100.0, r, q, surface, 41, 11);
        Assert.Equal(12, byCount.TimeGrid.Size);
        Assert.Equal(1.0, byCount.TimeGrid[byCount.TimeGrid.Size - 1]);

        var grid = new TimeGrid(1.0, 10);
        Assert.Same(grid, new LocalVolRNDCalculator(100.0, r, q, surface, grid, 41).TimeGrid);
    }

    [Fact]
    public void TheDiscreteSimpsonFourierIntegrationIsSimpsonsIntegrator()
    {
        static double F(double x) => Math.Exp(-x);

        // on a bounded range, the integrator itself
        Assert.Equal(
            new DiscreteSimpsonIntegrator(1001).Integrate(F, 0.0, 20.0),
            FourierIntegration.DiscreteSimpson(1001).Calculate(1.0, F, 20.0));
        Assert.Equal(1.0 - Math.Exp(-20.0), FourierIntegration.DiscreteSimpson(1001).Calculate(1.0, F, 20.0), 1e-8);
    }

    [Fact]
    public void TheGridModelsArgumentsAreItsNodesEachPositive()
    {
        Date[] dates = [Today + 91, Today + 365];
        double[][] strikes = [[80.0, 100.0, 120.0], [70.0, 100.0, 130.0]];
        var grid = new GridModelLocalVolSurface(Today, dates, strikes, Dc);

        Assert.Equal(6, grid.Arguments.Length);
        Assert.All(grid.Arguments, a => Assert.Equal(1, a.Size));
        Assert.True(grid.Constraint.Test(new Vector([0.3, 0.3, 0.2, 0.2, 0.25, 0.25])));
        Assert.False(grid.Constraint.Test(new Vector([0.3, 0.3, -0.2, 0.2, 0.25, 0.25])));
    }

    [Fact]
    public void ThePiecewiseSurfacesOneSectionConstructorIsTheListsOfOne()
    {
        var section = new FlatSmileSection(Today + 365, 0.2, Dc, Today, 100.0);
        var one = new PiecewiseBlackVarianceSurface(Today, Today + 365, section, Dc);
        var list = new PiecewiseBlackVarianceSurface(Today, [Today + 365], [section], Dc);

        Assert.Equal(list.MaxDate, one.MaxDate);
        foreach (double t in new[] { 0.25, 0.5, 1.0 })
        {
            Assert.Equal(list.BlackVariance(t, 90.0), one.BlackVariance(t, 90.0));
        }
    }
}
