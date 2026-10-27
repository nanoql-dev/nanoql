// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The random-number generators' refusals, by type, message and parameter, which a vector's <c>{"throws": true}</c>
/// can't tell apart: an argument a member refuses throws <see cref="ArgumentOutOfRangeException"/> or
/// <see cref="ArgumentNullException"/>, naming the caller's parameter, with QuantLib's message where QuantLib refuses
/// it. The seeds' refusals are <c>SeedRangeTests</c>' and <c>Divergences/SeedZeroTests</c>'.
/// </summary>
public class RandomNumberErrorTests
{
    [Fact]
    public void SobolRsgRefusesItsDimensionalityIndexAndDirectionIntegers()
    {
        AssertRange("dimensionality", 0, "dimensionality must be greater than 0", () => new SobolRsg(0, 1));
        AssertRange(
            "dimensionality",
            21201,
            "dimensionality 21201 exceeds the number of available primitive polynomials modulo two (21200)",
            () => new SobolRsg(21201, 1));
        AssertRange("dimension", 0, "dimensionality must be greater than 0", () => SobolRsg.Create(0, 1));
        AssertRange(
            "dimension",
            21201,
            "dimensionality 21201 exceeds the number of available primitive polynomials modulo two (21200)",
            () => SobolRsg.Create(21201, 1));

        foreach (bool useGrayCode in new[] { true, false })
        {
            var rsg = new SobolRsg(8, 1, SobolRsgDirectionIntegers.Jaeckel, useGrayCode);
            AssertRange("n", -1L, "the sample's index (-1) must be in [0, 4294967295]", () => rsg.SkipTo(-1));
            AssertRange(
                "n", 4294967296L, "the sample's index (4294967296) must be in [0, 4294967295]", () => rsg.SkipTo(4294967296));
        }

        AssertRange(
            "directionIntegers",
            (SobolRsgDirectionIntegers)10,
            "unknown direction integers (10)",
            () => new SobolRsg(8, 1, (SobolRsgDirectionIntegers)10));
        AssertRange(
            "directionIntegers",
            (SobolRsgDirectionIntegers)(-1),
            "unknown direction integers (-1)",
            () => new SobolRsg(8, 1, (SobolRsgDirectionIntegers)(-1)));
    }

    [Fact]
    public void Burley2020SobolRsgRefusesItsDimensionalityIndexAndDirectionIntegers()
    {
        AssertRange(
            "dimensionality", 0, "dimensionality must be greater than 0", () => new Burley2020SobolRsg(0, 42, SobolRsgDirectionIntegers.Jaeckel, 43));
        AssertRange(
            "dimensionality",
            21201,
            "dimensionality 21201 exceeds the number of available primitive polynomials modulo two (21200)",
            () => new Burley2020SobolRsg(21201, 42, SobolRsgDirectionIntegers.Jaeckel, 43));
        var rsg = new Burley2020SobolRsg(8, 42, SobolRsgDirectionIntegers.Jaeckel, 43);
        AssertRange("n", -1L, "the sample's index (-1) must be in [0, 4294967295]", () => rsg.SkipTo(-1));
        AssertRange("n", 4294967296L, "the sample's index (4294967296) must be in [0, 4294967295]", () => rsg.SkipTo(4294967296));
        AssertRange(
            "directionIntegers",
            (SobolRsgDirectionIntegers)10,
            "unknown direction integers (10)",
            () => new Burley2020SobolRsg(8, 42, (SobolRsgDirectionIntegers)10, 43));

        // QuantLib's period: the last sample's draw wraps the counter
        NanoQLException period = Assert.Throws<NanoQLException>(() => rsg.SkipTo(4294967295));
        Assert.Equal("Burley2020SobolRsg::nextInt32Sequence(): period exceeded", period.Message);
    }

    [Fact]
    public void HaltonRsgRefusesADimensionalityBelowOne()
    {
        AssertRange("dimensionality", 0, "dimensionality must be greater than 0", () => new HaltonRsg(0, 1));
        AssertRange("dimensionality", -1, "dimensionality must be greater than 0", () => new HaltonRsg(-1, 1, false, false));
        AssertRange("dimension", 0, "dimensionality must be greater than 0", () => HaltonRsg.Create(0, 1));
    }

    [Fact]
    public void RandomSequenceGeneratorRefusesItsDimensionality()
    {
        // QuantLib's constructor from a generator refuses 0; its seed constructor checks nothing, so only a negative
        // dimensionality, which QuantLib's Size can't hold, is refused there.
        AssertRange("dimensionality", 0, "dimensionality must be greater than 0", () =>
            new RandomSequenceGenerator<MersenneTwisterUniformRng>(0, new MersenneTwisterUniformRng(1)));
        AssertRange("dimensionality", 0, "dimensionality must be greater than 0", () =>
            new MersenneTwisterUniformRsg(0, new MersenneTwisterUniformRng(1)));
        AssertRange("dimensionality", -1, "dimensionality ('-1') must be a non-negative value.", () =>
            RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(-1, 1));
        AssertRange("dimensionality", -1, "dimensionality ('-1') must be a non-negative value.", () =>
            new MersenneTwisterUniformRsg(-1, 1L));
    }

    [Fact]
    public void CompositesRefuseANullGenerator()
    {
        AssertNull("rng", () => new RandomSequenceGenerator<MersenneTwisterUniformRng>(3, null!));
        AssertNull("rng", () => new MersenneTwisterUniformRsg(3, null!));
        AssertNull("generator", () => RandomSequenceGenerator.NextInt32Sequence<MersenneTwisterUniformRng>(null!));
        AssertNull("uniformGenerator", () => new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(null!));
        AssertNull("uniformGenerator", () => new InvCumulativeMersenneTwisterGaussianRng(null!));
        AssertNull("uniformSequenceGenerator", () => new InverseCumulativeRsg<HaltonRsg, InverseCumulativeNormal>(null!));
        AssertNull("uniformSequenceGenerator", () =>
            new InverseCumulativeRsg<HaltonRsg, InverseCumulativeNormal>(null!, new InverseCumulativeNormal(0.5, 2.0)));
        AssertNull("uniformSequenceGenerator", () => new InvCumulativeSobolGaussianRsg(null!));
    }

    [Fact]
    public void TheTraitsRefuseTheirSequenceGeneratorsDimension()
    {
        // The pseudo-random traits' RandomSequenceGenerator takes any dimension QuantLib's Size holds; the
        // low-discrepancy ones refuse what their generator does.
        AssertRange("dimension", -1, "dimension ('-1') must be a non-negative value.", () =>
            PseudoRandom.MakeSequenceGenerator(-1, 1));
        AssertRange("dimension", -1, "dimension ('-1') must be a non-negative value.", () =>
            PoissonPseudoRandom.MakeSequenceGenerator(-1, 1, new InverseCumulativePoisson(4.0)));
        AssertRange("dimension", 0, "dimensionality must be greater than 0", () => LowDiscrepancy.MakeSequenceGenerator(0, 1));
        AssertRange(
            "dimension",
            21201,
            "dimensionality 21201 exceeds the number of available primitive polynomials modulo two (21200)",
            () => LowDiscrepancy.MakeSequenceGenerator(21201, 1, new InverseCumulativeNormal(0.5, 2.0)));
        AssertRange("dimension", 0, "dimensionality must be greater than 0", () =>
            GenericLowDiscrepancy<HaltonRsg, InverseCumulativeNormal>.MakeSequenceGenerator(0, 1));
    }

    private static void AssertRange(string paramName, object actualValue, string message, Func<object> act)
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal(paramName, e.ParamName);
        Assert.Equal(actualValue, e.ActualValue);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void AssertNull(string paramName, Func<object> act)
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith("Value cannot be null.", e.Message, StringComparison.Ordinal);
    }
}
