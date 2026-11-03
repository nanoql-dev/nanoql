// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib reads a seed of 0 as "no seed" and draws one from its <c>SeedGenerator</c> singleton, itself seeded from
/// the clock; nanoql's seeds are explicit and no library code reads the clock, so every generator refuses 0 with
/// <see cref="ArgumentOutOfRangeException"/>. QuantLib's <c>testSeedGenerator</c>, which only draws from the singleton,
/// becomes this test. Two of QuantLib's seeds of 0 are deterministic, and nanoql refuses them too, one rule for every
/// generator: <see cref="SobolRsg"/> reads its seed only for the dimensions beyond its set's table of direction integers,
/// so <c>SobolRsg(32, 0)</c> draws what <c>SobolRsg(32, 1)</c> does; and <see cref="HaltonRsg"/> without a random start
/// or shift never reads it.
/// </summary>
public class SeedZeroTests
{
    [Fact]
    public void EveryGeneratorRefusesSeedZero()
    {
        AssertRefused(() => new MersenneTwisterUniformRng(0));
        AssertRefused(() => MersenneTwisterUniformRng.Create(0));
        AssertRefused(() => RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(3, 0));
        AssertRefused(() => new MersenneTwisterUniformRsg(3, 0));

        // QuantLib's 0 is deterministic here: 32 dimensions lie in Jäckel's table.
        AssertRefused(() => new SobolRsg(32, 0));
        AssertRefused(() => SobolRsg.Create(32, 0));

        // QuantLib's 0 is deterministic here: without a random start or shift the seed isn't read.
        AssertRefused(() => new HaltonRsg(3, 0, false, false));
        AssertRefused(() => new HaltonRsg(3, 0));
        AssertRefused(() => HaltonRsg.Create(3, 0));

        AssertRefused(() => PseudoRandom.MakeSequenceGenerator(3, 0));
        AssertRefused(() => PoissonPseudoRandom.MakeSequenceGenerator(3, 0));
        AssertRefused(() => LowDiscrepancy.MakeSequenceGenerator(3, 0));
        AssertRefused(() => GenericLowDiscrepancy<HaltonRsg, InverseCumulativeNormal>.MakeSequenceGenerator(3, 0));

        // Knuth's and L'Ecuyer's 0 is QuantLib's clock seed; Ranlux's 0 is the standard engines' default, 19780503,
        // which nanoql doesn't default to either; xoshiro's 0 is the clock seed among its 64-bit seeds; the randomized
        // sequences' seed constructors pass theirs on.
        AssertRefused(() => new KnuthUniformRng(0));
        AssertRefused(() => new KnuthUniformRsg(3, 0));
        AssertRefused(() => new LecuyerUniformRng(0));
        AssertRefused(() => new LecuyerUniformRsg(3, 0));
        AssertRefused(() => new Ranlux3UniformRng(0));
        AssertRefused(() => new Ranlux4UniformRng(0));
        AssertRefused(() => new Xoshiro256StarStarUniformRng(0));
        AssertRefused(() => new Xoshiro256StarStarUniformRsg(3, 0));
        AssertRefused(() => RandomizedLDS.Create<SobolRsg, MersenneTwisterUniformRng>(new SobolRsg(3, 1), 0));
        AssertRefused(() => RandomizedLDS.Create<SobolRsg, MersenneTwisterUniformRng>(3, 0, 1));
        AssertRefused(() => RandomizedLDS.Create<SobolRsg, MersenneTwisterUniformRng>(3, 1, 0));

        // A key's word of 0 is no seed: the constructor from a key takes it, as QuantLib's does.
        _ = new MersenneTwisterUniformRng([0L]);
    }

    private static void AssertRefused(Func<object> create)
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(create);
        Assert.Equal("seed", e.ParamName);
        Assert.Equal(0L, e.ActualValue);
        Assert.Contains("clock", e.Message, StringComparison.Ordinal);
    }
}
