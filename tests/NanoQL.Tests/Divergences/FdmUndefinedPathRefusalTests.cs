// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference core's inputs QuantLib leaves undefined, each refused before the undefined step. A mesher of
/// no point writes the first and last of an empty vector, and runs its loop to the unsigned size less one
/// (<c>uniform1dmesher.hpp:41-50</c>, <c>concentrating1dmesher.cpp:61-103</c>); a mesher of one point divides by zero
/// and leaves that point at its end and its distances null. A one-point splitting solve runs its back substitution from
/// the unsigned size less two (<c>triplebandlinearop.cpp:301-303</c>); the one-point operator's apply and its matrix
/// read and write its point's neighbours, which the layout reflects past the grid. An empty layout takes the element
/// before its dimensions' first and the last of an empty spacing (<c>fdmlinearoplayout.hpp:37-42</c>). A composite of
/// more meshers than its layout's dimensions reads past them (<c>fdmmeshercomposite.cpp:80-83</c>). Bands of different
/// sizes, added or multiplied, are read past the shorter's end (<c>triplebandlinearop.cpp:163-188</c>).
/// </summary>
public class FdmUndefinedPathRefusalTests
{
    [Fact]
    public void AUniformMesherOfNoPointIsRefused() =>
        Assert.Throws<ArgumentException>(() => new Uniform1dMesher(0.0, 1.0, 0));

    [Fact]
    public void AUniformMesherOfOnePointIsRefused() =>
        Assert.Throws<ArgumentException>(() => new Uniform1dMesher(0.0, 1.0, 1));

    [Fact]
    public void AConcentratingMesherOfNoPointIsRefused() =>
        Assert.Throws<ArgumentException>(() => new Concentrating1dMesher(0.0, 1.0, 0, (0.5, 0.1)));

    [Fact]
    public void AConcentratingMesherOfOnePointIsRefused() =>
        Assert.Throws<ArgumentException>(() => new Concentrating1dMesher(0.0, 1.0, 1, (0.5, 0.1)));

    [Fact]
    public void AOnePointSolveIsRefused()
    {
        // a layout of one point, on QuantLib's base mesher, which takes any size
        var op = new FirstDerivativeOp(0, new FdmMesherComposite(new Fdm1dMesher(1)));
        Assert.Throws<ArgumentException>(() => op.SolveSplitting(new Vector(1, 1.0), 0.5, 1.0));
    }

    [Fact]
    public void AOnePointApplyAndMatrixAreRefused()
    {
        // The operator builds on one point, as QuantLib's does; its neighbours, reflected past the grid, are read by
        // its apply and written by its matrix.
        var op = new FirstDerivativeOp(0, new FdmMesherComposite(new Fdm1dMesher(1)));
        ArgumentException e = Assert.Throws<ArgumentException>(() => op.Apply(new Vector(1, 1.0)));
        Assert.Equal("r", e.ParamName);
        Assert.StartsWith("a triple-band apply needs at least two points in its direction, not 1", e.Message, StringComparison.Ordinal);
        Assert.Equal(
            "a triple-band matrix needs at least two points in its direction, not 1",
            Assert.Throws<InvalidOperationException>(() => op.ToMatrix()).Message);
        e = Assert.Throws<ArgumentException>(() => op.SolveSplitting(new Vector(1, 1.0), 0.5, 1.0));
        Assert.StartsWith("a triple-band solve needs at least two points, not 1", e.Message, StringComparison.Ordinal);

        var two = new FirstDerivativeOp(0, new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 2)));
        Assert.Equal(2, two.Apply(new Vector(2, 1.0)).Size);
        Assert.Equal(2, two.ToMatrix().Size1);
    }

    [Fact]
    public void AnEmptyLayoutIsRefused() => Assert.Throws<ArgumentException>(() => new FdmLinearOpLayout([]));

    [Fact]
    public void MoreMeshersThanDimensionsAreRefused() =>
        Assert.Throws<ArgumentException>(() => new FdmMesherComposite(
            new FdmLinearOpLayout([5]), [new Uniform1dMesher(0.0, 1.0, 5), new Uniform1dMesher(0.0, 1.0, 4)]));

    [Fact]
    public void BandsOfDifferentSizesAreRefused()
    {
        var x = new FirstDerivativeOp(0, new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 5)));
        var y = new SecondDerivativeOp(0, new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 7)));
        Assert.Throws<ArgumentException>(() => x.Add(y));
        Assert.Throws<ArgumentException>(() => x.Mult(new Vector(4, 1.0)));
        Assert.Throws<ArgumentException>(() => x.Add(new Vector(4, 1.0)));
    }
}
