// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A boundary's direction outside the layout. QuantLib's <c>FdmIndicesOnBoundary</c> writes <c>newDim[direction]</c>
/// (<c>fdmindicesonboundary.cpp:31-32</c>) and <c>FdmDirichletBoundary</c> reads <c>locations(direction)</c>, past
/// their vectors' ends: undefined behaviour. Here both refuse the direction on entry with
/// <see cref="ArgumentOutOfRangeException"/>, before any read; a direction in the layout is QuantLib's.
/// </summary>
public class FdmBoundaryDirectionTests
{
    [Fact]
    public void TheIndicesRefuseADirectionOutsideTheLayout()
    {
        var layout = new FdmLinearOpLayout([10]);
        foreach (int direction in new[] { 1, -1 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
                () => new FdmIndicesOnBoundary(layout, direction, BoundaryConditionSide.Lower));
            Assert.Equal("direction", e.ParamName);
        }

        Assert.Equal([9], new FdmIndicesOnBoundary(layout, 0, BoundaryConditionSide.Upper).Indices);
    }

    [Fact]
    public void TheDirichletBoundaryRefusesADirectionOutsideTheLayout()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 10));
        foreach (int direction in new[] { 1, -1 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
                () => new FdmDirichletBoundary(mesher, 0.0, direction, BoundaryConditionSide.Upper));
            Assert.Equal("direction", e.ParamName);
        }

        double[] values = [.. Enumerable.Repeat(1.0, 10)];
        new FdmDirichletBoundary(mesher, 0.5, 0, BoundaryConditionSide.Upper).ApplyAfterApplying(values);
        Assert.Equal([1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 0.5], values);
    }
}
