// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="MultiPathGenerator{TGsg}"/>'s conventions: the Brownian bridge QuantLib refuses at the first draw is
/// refused at construction, with QuantLib's text; a clone, its sequence generator copied, draws as its source from
/// there; and a <see cref="MultiPath"/> built from paths copies them, so the generator's next draw leaves it as it was.
/// </summary>
public class MultiPathGeneratorTests
{
    [Fact]
    public void ABrownianBridgeIsRefusedAtConstruction()
    {
        // multipathgenerator.hpp:106-108 throw "Brownian bridge not supported" at next(), after the constructor.
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new MultiPathGenerator<IRsg>(Array(), new TimeGrid(1.0, 4), PseudoRandom.MakeSequenceGenerator(8, 42), true));
        Assert.Equal("brownianBridge", e.ParamName);
        Assert.StartsWith("Brownian bridge not supported", e.Message);
    }

    [Fact]
    public void ACloneDrawsAsItsSource()
    {
        var generator = new GaussianMultiPathGenerator(Array(), new TimeGrid(1.0, 4), PseudoRandom.MakeSequenceGenerator(8, 42));
        generator.Next();
        GaussianMultiPathGenerator clone = generator.Clone();
        for (int k = 0; k < 3; k++)
        {
            double[] a = Last(generator.Next().Value), b = Last(clone.Next().Value);
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void AMultiPathBuiltFromPathsCopiesThem()
    {
        var generator = new MultiPathGenerator<IRsg>(Array(), new TimeGrid(1.0, 4), PseudoRandom.MakeSequenceGenerator(8, 42));
        MultiPath view = generator.Next().Value;
        var copy = new MultiPath([view[0], view[1]]);
        double[] before = Last(copy);
        generator.Next();
        Assert.Equal(before, Last(copy));
        Assert.NotEqual(before, Last(view));
        Assert.Equal(2, copy.AssetNumber);
        Assert.Equal(5, copy.PathSize());

        ArgumentOutOfRangeException o = Assert.Throws<ArgumentOutOfRangeException>(() => copy.At(2));
        Assert.Equal("j", o.ParamName);
        Assert.StartsWith("index (2) must be less than 2", o.Message);
        Assert.Same(copy[1], copy.At(1));
        Assert.Equal(0.0, new MultiPath(3, new TimeGrid(1.0, 2))[2][1]);
    }

    private static StochasticProcessArray Array() =>
        new(
            [new OrnsteinUhlenbeckProcess(0.1, 0.2), new GeometricBrownianMotionProcess(100.0, 0.03, 0.2)],
            new Matrix(new[,] { { 1.0, 0.5 }, { 0.5, 1.0 } }));

    private static double[] Last(MultiPath mp) =>
        [.. Enumerable.Range(0, mp.AssetNumber).Select(j => mp[j].Back())];
}
