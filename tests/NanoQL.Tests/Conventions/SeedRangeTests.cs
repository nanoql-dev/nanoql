// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Seeds are QuantLib's 32-bit unsigned values taken as a <see cref="long"/>: every generator refuses a seed outside
/// [1, 4294967295] with <see cref="ArgumentOutOfRangeException"/>, naming the parameter, and takes both ends (seed 0,
/// QuantLib's clock seed, is <c>Divergences/SeedZeroTests</c>'s). The Mersenne Twister's key takes words, not seeds:
/// each in [0, 4294967295], at least one.
/// </summary>
public class SeedRangeTests
{
    private static readonly long[] OutsideTheRange = [-1, 4294967296, long.MinValue, long.MaxValue];

    private static readonly Func<long, object>[] Generators =
    [
        seed => new MersenneTwisterUniformRng(seed),
        seed => MersenneTwisterUniformRng.Create(seed),
        seed => RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(3, seed),
        seed => new MersenneTwisterUniformRsg(3, seed),
        seed => new SobolRsg(40, seed),
        seed => SobolRsg.Create(40, seed),
        seed => new HaltonRsg(3, seed, false, false),
        seed => HaltonRsg.Create(3, seed),
        seed => PseudoRandom.MakeSequenceGenerator(3, seed),
        seed => PoissonPseudoRandom.MakeSequenceGenerator(3, seed),
        seed => LowDiscrepancy.MakeSequenceGenerator(3, seed),
        seed => GenericLowDiscrepancy<HaltonRsg, InverseCumulativeNormal>.MakeSequenceGenerator(3, seed),
    ];

    [Fact]
    public void SeedsOutsideTheRangeAreRefused()
    {
        foreach (Func<long, object> generator in Generators)
        {
            foreach (long seed in OutsideTheRange)
            {
                ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => generator(seed));
                Assert.Equal("seed", e.ParamName);
                Assert.Equal(seed, e.ActualValue);
            }

            Assert.NotNull(generator(1));
            Assert.NotNull(generator(4294967295));
        }
    }

    [Fact]
    public void TheKeyRefusesNoWordsAndWordsOutsideTheRange()
    {
        ArgumentException empty = Assert.Throws<ArgumentException>(() => new MersenneTwisterUniformRng(ReadOnlySpan<long>.Empty));
        Assert.Equal("seeds", empty.ParamName);

        foreach (long word in OutsideTheRange)
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
                () => new MersenneTwisterUniformRng([0x123, word]));
            Assert.Equal("seeds", e.ParamName);
            Assert.Equal(word, e.ActualValue);
        }

        // Every word in [0, 4294967295] is a key's word, 0 among them.
        var key = new MersenneTwisterUniformRng([0, 4294967295]);
        Assert.InRange(key.NextInt32(), 0, 4294967295);
    }
}
