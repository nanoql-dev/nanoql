// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The types, messages and parameter names of C1's refusals of QuantLib's undefined paths (master §2.2's row; the
/// divergences name QuantLib's lines): an empty basket's extreme (<c>Divergences/BasketPayoffEmptyTests.cs</c>), an
/// average's price count other than its weights' (<c>Divergences/AverageBasketPayoffWeightCountTests.cs</c>), the
/// Wiener operator's direction past its count (<c>Divergences/FdmWienerOpDirectionTests.cs</c>), and the spline's NaN
/// coordinate (<c>Divergences/MultiCubicSplineBoundaryTests.cs</c>).
/// </summary>
public class BasketUndefinedPathRefusalTests
{
    [Fact]
    public void EachRefusalsTypeMessageAndParameter()
    {
        var call = new PlainVanillaPayoff(OptionType.Call, 100.0);

        ArgumentException min = Assert.Throws<ArgumentException>(() => new MinBasketPayoff(call).Accumulate([]));
        Assert.Equal("empty basket given (Parameter 'a')", min.Message);
        ArgumentException max = Assert.Throws<ArgumentException>(() => new MaxBasketPayoff(call).Accumulate([]));
        Assert.Equal("a", max.ParamName);

        ArgumentException count = Assert.Throws<ArgumentException>(
            () => new AverageBasketPayoff(call, [0.5, 0.5]).Accumulate([100.0]));
        Assert.Equal("wrong number of weights arguments in payoff (Parameter 'a')", count.Message);

        ArgumentException spread = Assert.Throws<ArgumentException>(() => new SpreadBasketPayoff(call).Accumulate([100.0]));
        Assert.Equal("payoff is only defined for two underlyings (Parameter 'a')", spread.Message);

        var op = new FdmWienerOp(new FdmMesherComposite(new Uniform1dMesher(-1.0, 1.0, 5)), null, [0.04]);
        ArgumentOutOfRangeException direction = Assert.Throws<ArgumentOutOfRangeException>(
            () => op.ApplyDirection(1, new double[5], new double[5]));
        Assert.Equal("direction", direction.ParamName);
        Assert.StartsWith("direction 1 out of the operator's 1", direction.Message, StringComparison.Ordinal);

        var spline = new MultiCubicSpline([[0.0, 1.0, 2.0, 3.0]], [0.0, 1.0, 4.0, 9.0], [true]);
        ArgumentException nan = Assert.Throws<ArgumentException>(() => spline.Evaluate([double.NaN]));
        Assert.Equal("Dimension 0: the coordinate is not a number (Parameter 'x')", nan.Message);
    }
}
