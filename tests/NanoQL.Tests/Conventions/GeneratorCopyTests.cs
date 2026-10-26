// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Generators are working types whose states never alias (playbook §2.7): a composition copies the generator it is
/// given, as QuantLib's by-value constructors do, so the caller's later draws change nothing in it, nor its draws
/// anything in the caller's; and <c>Clone()</c> is deep, the copy's next draw the original's, into its own buffer.
/// </summary>
public class GeneratorCopyTests
{
    [Fact]
    public void RandomSequenceGeneratorCopiesItsGenerator()
    {
        var rng = new MersenneTwisterUniformRng(42);
        RandomSequenceGenerator<MersenneTwisterUniformRng>[] composites =
        [
            new RandomSequenceGenerator<MersenneTwisterUniformRng>(3, rng),
            new MersenneTwisterUniformRsg(3, rng),
        ];
        Draw(rng, 10);

        foreach (RandomSequenceGenerator<MersenneTwisterUniformRng> composite in composites)
        {
            var reference = RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(3, 42);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(reference.NextSequence().Value.ToArray(), composite.NextSequence().Value.ToArray());
            }
        }

        // Nor do the composites' draws change the caller's generator.
        var alone = new MersenneTwisterUniformRng(42);
        Draw(alone, 10);
        Assert.Equal(alone.NextInt32(), rng.NextInt32());
    }

    [Fact]
    public void InverseCumulativeRngCopiesItsGenerator()
    {
        var rng = new MersenneTwisterUniformRng(42);
        IRng[] composites =
        [
            new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(rng),
            new InvCumulativeMersenneTwisterGaussianRng(rng),
        ];
        Draw(rng, 10);

        foreach (IRng composite in composites)
        {
            var reference = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(
                new MersenneTwisterUniformRng(42));
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(reference.Next(), composite.Next());
            }
        }

        var alone = new MersenneTwisterUniformRng(42);
        Draw(alone, 10);
        Assert.Equal(alone.NextInt32(), rng.NextInt32());
    }

    [Fact]
    public void InverseCumulativeRsgCopiesItsGenerator()
    {
        var halton = new HaltonRsg(3, 42);
        var sobol = new SobolRsg(3, 42);
        IRsg[] composites =
        [
            new InverseCumulativeRsg<HaltonRsg, InverseCumulativeNormal>(halton),
            new InverseCumulativeRsg<HaltonRsg, InverseCumulativeNormal>(halton, new InverseCumulativeNormal(0.5, 2.0)),
            new InvCumulativeSobolGaussianRsg(sobol),
            new InvCumulativeSobolGaussianRsg(sobol, new InverseCumulativeNormal(0.5, 2.0)),
        ];
        for (int i = 0; i < 10; i++)
        {
            halton.NextSequence();
            sobol.NextSequence();
        }

        IRsg[] references =
        [
            new InverseCumulativeRsg<HaltonRsg, InverseCumulativeNormal>(new HaltonRsg(3, 42)),
            new InverseCumulativeRsg<HaltonRsg, InverseCumulativeNormal>(new HaltonRsg(3, 42), new InverseCumulativeNormal(0.5, 2.0)),
            new InvCumulativeSobolGaussianRsg(new SobolRsg(3, 42)),
            new InvCumulativeSobolGaussianRsg(new SobolRsg(3, 42), new InverseCumulativeNormal(0.5, 2.0)),
        ];
        for (int k = 0; k < composites.Length; k++)
        {
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(references[k].NextSequence().Value.ToArray(), composites[k].NextSequence().Value.ToArray());
            }
        }

        var alone = new HaltonRsg(3, 42);
        for (int i = 0; i < 10; i++)
        {
            alone.NextSequence();
        }

        Assert.Equal(alone.NextSequence().Value.ToArray(), halton.NextSequence().Value.ToArray());
    }

    [Fact]
    public void CloneIsDeep()
    {
        // Each scalar generator, a few draws in: the copy's next draws are the original's.
        IRng[] scalars =
        [
            new MersenneTwisterUniformRng(7),
            new InverseCumulativeRng<MersenneTwisterUniformRng, MoroInverseCumulativeNormal>(new MersenneTwisterUniformRng(7)),
            new MoroInvCumulativeMersenneTwisterGaussianRng(new MersenneTwisterUniformRng(7)),
        ];
        foreach (IRng original in scalars)
        {
            Draw(original, 3);
            IRng copy = original.Clone();
            Assert.NotSame(original, copy);
            Assert.IsType(original.GetType(), copy);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(original.Next(), copy.Next());
            }
        }

        // Each sequence generator, a few draws in: the copy's next draws are the original's, into its own buffer, and
        // the original's later draws change nothing in the copy.
        IRsg[] sequences =
        [
            RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(4, 7),
            new MersenneTwisterUniformRsg(4, 7),
            new SobolRsg(40, 7),
            new SobolRsg(40, 7, SobolRsgDirectionIntegers.Kuo, useGrayCode: false),
            new HaltonRsg(4, 7, true, true),
            PseudoRandom.MakeSequenceGenerator(4, 7),
            new MoroInvCumulativeHaltonGaussianRsg(new HaltonRsg(4, 7)),
            new SobolBrownianBridgeRsg(3, 4, SobolBrownianGeneratorBaseOrdering.Diagonal, 7),
        ];
        foreach (IRsg original in sequences)
        {
            for (int i = 0; i < 3; i++)
            {
                original.NextSequence();
            }

            IRsg copy = original.Clone();
            Assert.NotSame(original, copy);
            Assert.IsType(original.GetType(), copy);
            Assert.Equal(original.LastSequence().Value.ToArray(), copy.LastSequence().Value.ToArray());
            Sample<ReadOnlyMemory<double>> a = original.NextSequence(), b = copy.NextSequence();
            Assert.Equal(a.Value.ToArray(), b.Value.ToArray());
            Assert.Equal(a.Weight, b.Weight);
            Assert.False(a.Value.Span.Overlaps(b.Value.Span));

            double[] copied = b.Value.ToArray();
            double[] ahead = original.NextSequence().Value.ToArray();
            Assert.Equal(copied, copy.LastSequence().Value.ToArray());
            Assert.Equal(ahead, copy.NextSequence().Value.ToArray());
        }
    }

    private static void Draw(IRng rng, int n)
    {
        for (int i = 0; i < n; i++)
        {
            rng.Next();
        }
    }
}
