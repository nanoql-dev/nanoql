// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The n-dimensional solver's and spline's dimension at run time (the S-exo plan's ER3): the solver takes the mesher
/// layout's, one to four in the n-dimensional engine's use and any the spline carries, where QuantLib's is a template
/// parameter; the spline takes one to fifteen, QuantLib's list (six here: fifteen dimensions of four points hold 4¹⁵
/// values), and refuses none or sixteen and the other inputs its recursion can't read (a dimension of no point, values
/// or flags or a point of another count).
/// </summary>
public class FdmNdimSolverDimensionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void TheSolverTakesTheLayoutsDimension(int n)
    {
        Fdm1dMesher[] meshers = [.. Enumerable.Range(0, n).Select(_ => (Fdm1dMesher)new Uniform1dMesher(4.0, 5.2, 5))];
        var mesher = new FdmMesherComposite(meshers);
        var payoff = new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0), n);
        var calc = new FdmLogBasketInnerValue(payoff, mesher);
        var desc = new FdmSolverDesc(mesher, [], new FdmStepConditionComposite([], []), calc, 1.0, 5, 0);
        var solver = new FdmNdimSolver(desc, FdmSchemeDesc.Douglas(), new FdmWienerOp(mesher, null, [.. Enumerable.Repeat(0.04, n)]));
        double value = solver.InterpolateAt([.. Enumerable.Repeat(4.6, n)]);
        Assert.True(double.IsFinite(value));
        Assert.Throws<ArgumentException>(() => solver.InterpolateAt([.. Enumerable.Repeat(4.6, n + 1)]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void TheSplineTakesOneToFifteenDimensions(int n)
    {
        ImmutableArray<ImmutableArray<double>> grid = [.. Enumerable.Range(0, n).Select(_ => ImmutableArray.Create(0.0, 1.0, 2.0, 3.0))];
        double[] values = new double[(int)Math.Pow(4, n)];
        for (int i = 0; i < values.Length; ++i)
        {
            values[i] = i % 7;
        }

        var spline = new MultiCubicSpline(grid, values);
        Assert.Equal(n, spline.Dimension);
        Assert.Equal(15, MultiCubicSpline.MaxDimension);
        Assert.True(double.IsFinite(spline.Evaluate([.. Enumerable.Repeat(1.5, n)])));
    }

    [Fact]
    public void TheSplineRefusesWhatItsRecursionCantRead()
    {
        ImmutableArray<double> axis = [0.0, 1.0, 2.0, 3.0];
        Assert.Equal("grid", Assert.Throws<ArgumentException>(() => new MultiCubicSpline([], [])).ParamName);
        Assert.Equal(
            "grid",
            Assert.Throws<ArgumentException>(() => new MultiCubicSpline([.. Enumerable.Repeat(ImmutableArray.Create(0.0, 1.0), 16)], [])).ParamName);
        ArgumentException empty = Assert.Throws<ArgumentException>(() => new MultiCubicSpline([[]], []));
        Assert.Equal("Dimension 0 : empty grid (Parameter 'grid')", empty.Message);
        Assert.Equal("values", Assert.Throws<ArgumentException>(() => new MultiCubicSpline([axis], [1.0, 2.0])).ParamName);
        Assert.Equal(
            "extrapolation",
            Assert.Throws<ArgumentException>(() => new MultiCubicSpline([axis], [0, 1, 2, 3], [true, true])).ParamName);
        Assert.Equal("x", Assert.Throws<ArgumentException>(() => new MultiCubicSpline([axis], [0, 1, 2, 3]).Evaluate([1.0, 1.0])).ParamName);
        ArgumentException few = Assert.Throws<ArgumentException>(() => new MultiCubicSpline([[0.0, 1.0, 2.0]], [0, 1, 2]));
        Assert.Equal("Dimension 0 : not enough points for interpolation (Parameter 'grid')", few.Message);
        ArgumentException invalid = Assert.Throws<ArgumentException>(() => new MultiCubicSpline([[0.0, 1.0, 1.0, 2.0]], [0, 1, 2, 3]));
        Assert.Equal("Dimension 0 : invalid data (Parameter 'grid')", invalid.Message);
        ArgumentException outside = Assert.Throws<ArgumentException>(() => new MultiCubicSpline([axis], [0, 1, 2, 3]).Evaluate([-1.0]));
        Assert.Equal("Dimension 0: extrapolation is not allowed. (Parameter 'x')", outside.Message);
    }
}
