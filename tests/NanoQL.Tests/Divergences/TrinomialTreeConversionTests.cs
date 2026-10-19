// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The trinomial tree's node offset when it is NaN or outside the integers, and an empty grid. QuantLib converts
/// <c>std::floor((m - x0_)/dx_[i+1] + 0.5)</c> to an <c>Integer</c> (<c>trinomialtree.cpp:99</c>), which C++ leaves
/// undefined there; MSVC, the platform the vectors are generated on, gives the smallest integer, and the classical
/// regime's probability check then refuses, as the wheel does for a NaN reversion ("negative probability in trinomial
/// tree (unfloored regime) at step 0, node 0: p1=-nan(ind), p2=-nan(ind), p3=-nan(ind) (v=nan, dx=nan, e=-nan(ind))").
/// .NET's conversion saturates instead (NaN to 0, a large value to <see cref="int.MaxValue"/>), so nanoql converts as
/// MSVC does and refuses where QuantLib does, with its text, its NaNs spelled as .NET spells them. For a NaN the
/// offset never reaches the text, since the probabilities are NaN whatever it is; an offset beyond the integers does.
/// QuantLib's step count is unsigned, so an empty grid wraps it and fails allocating ("vector too long"); nanoql's
/// reaches QuantLib's own check, "null time steps for trinomial tree", which QuantLib gives a one-point grid.
/// </summary>
public class TrinomialTreeConversionTests
{
    private const string Prefix = "negative probability in trinomial tree (unfloored regime) at step 0, node 0: ";

    [Fact]
    public void ANaNReversionIsRefusedByTheProbabilityCheck()
    {
        var today = new Date(15, Month.January, 2026);
        HullWhite model = new HullWhite(new FlatForward(today, 0.05, new Actual365Fixed()))
            .SetParams(new Vector([double.NaN, 0.01]));

        NanoQLException e = Assert.Throws<NanoQLException>(() => model.Tree(new TimeGrid(1.0, 4)));
        Assert.Equal(Prefix + "p1=NaN, p2=NaN, p3=NaN (v=NaN, dx=NaN, e=NaN)", e.Message);
    }

    [Fact]
    public void AnOffsetBeyondTheIntegersConvertsToTheSmallestInteger()
    {
        // A tiny volatility: the root's expectation lies about 1.4e17 spacings above it.
        var model = new Vasicek(r0: 0.0, a: 0.1, b: 0.05, sigma: 1.0e-20);
        StochasticProcess1D process = model.Dynamics().Process;
        var grid = new TimeGrid(1.0, 4);

        double dt = grid.Dt(0);
        double v = Math.Sqrt(process.Variance(grid[0], 0.0, dt));
        double dx = v * Math.Sqrt(3.0);
        double x0 = process.X0;
        double m = process.Expectation(grid[0], x0, dt);
        Assert.True((m - x0) / dx > int.MaxValue);
        double e = m - (x0 + (int.MinValue * dx));

        NanoQLException refusal = Assert.Throws<NanoQLException>(() => model.Tree(grid));
        Assert.StartsWith(Prefix, refusal.Message, StringComparison.Ordinal);
        Assert.EndsWith($"(v={Show(v)}, dx={Show(dx)}, e={Show(e)})", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyGridIsRefusedWithQuantLibsOwnText()
    {
        StochasticProcess1D process = new Vasicek().Dynamics().Process;

        foreach (TimeGrid grid in (TimeGrid[])[new TimeGrid(), new TimeGrid([0.0])])
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => new TrinomialTree(process, grid));
            Assert.Equal("timeGrid", e.ParamName);
            Assert.StartsWith("null time steps for trinomial tree", e.Message, StringComparison.Ordinal);
        }
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
