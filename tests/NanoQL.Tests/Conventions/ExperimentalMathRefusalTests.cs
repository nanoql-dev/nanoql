// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The experimental maths' refusals. A firefly algorithm with one or two differential evolution members is refused, as
/// no three distinct members exist to draw, where QuantLib's draws loop forever; a piecewise function with no values is
/// refused, where QuantLib's macro reads past them; a clubs topology whose minimum is 0, where QuantLib's best particle
/// leaves every club and reads particle 0's position; an annealing sampler on a lower bound above its upper, where
/// QuantLib's loops never end, and the ring's and mirror's bounds of different sizes; the very fast sampler from a
/// point no draw brings into its box, its width or farther from it, off a zero-width box's bound, or outside the box at
/// a temperature of 0, alone or as an annealing's start, where QuantLib's loop never ends, each run on a thread with a
/// time limit, while a point just within its width still draws; every optimizer, walk, sampler and generator takes
/// its seed in [1, 4294967295], QuantLib's default 0, which seeds from the clock, refused; and a Laplace
/// interpolation's coordinates of another count than the matrix's columns or rows, which QuantLib reads past.
/// </summary>
public class ExperimentalMathRefusalTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AFireflyAlgorithmWithOneOrTwoEvolutionMembersIsRefused(int Mde)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new FireflyAlgorithm(10, new ExponentialIntensity(1.0, 0.2, 1.0), new GaussianWalk(0.1, 0.9, 1), Mde, 1.0, 0.5, 1));
        Assert.Contains("three distinct members", e.Message, StringComparison.Ordinal);
        _ = new FireflyAlgorithm(10, new ExponentialIntensity(1.0, 0.2, 1.0), new GaussianWalk(0.1, 0.9, 1), 3, 1.0, 0.5, 1);
        _ = new FireflyAlgorithm(10, new ExponentialIntensity(1.0, 0.2, 1.0), new GaussianWalk(0.1, 0.9, 1), 0, 1.0, 0.5, 1);
    }

    [Fact]
    public void APiecewiseFunctionWithNoValuesIsRefused()
    {
        Assert.Throws<ArgumentException>(() => PiecewiseFunctionFunctions.Evaluate([1.0, 2.0], [], 1.5));

        // no breaks: the constant Y[0]; fewer values than intervals: the last holds
        Assert.Equal(7.0, PiecewiseFunctionFunctions.Evaluate([], [7.0, 8.0], 1e9));
        Assert.Equal(8.0, PiecewiseFunctionFunctions.Evaluate([1.0, 2.0, 3.0], [7.0, 8.0], 2.5));
    }

    [Fact]
    public void AClubsTopologyWithAMinimumOfNoClubsIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClubsTopology(2, 4, 3, 0, 5, 1));
        _ = new ClubsTopology(2, 4, 3, 1, 5, 1);
    }

    [Fact]
    public void AnUnboundClubsTopologyIsRefusedBeforeItsClubsAreRead()
    {
        // QuantLib's setSize makes the clubs its findSocialBest reads; unbound, the topology refuses as each read of
        // the swarm does.
        NanoQLException e = Assert.Throws<NanoQLException>(new ClubsTopology(2, 4, 3, 1, 5, 1).FindSocialBest);
        Assert.Equal("the topology isn't bound to a minimization's swarm", e.Message);
    }

    [Theory]
    [InlineData("ring")]
    [InlineData("mirror")]
    [InlineData("veryfast")]
    public void ASamplerOnAnInvertedBoxIsRefused(string kind)
    {
        var lower = new Vector(2, 0.0) { [1] = 1.0 };
        var upper = new Vector(2, 1.0) { [1] = 0.0 };
        Exception? e = ReturnsWithin(() => Sampler(kind, lower, upper).Sample(new Vector(2, 0.0), new Vector(2, 0.5), new Vector(2, 1.0)));
        Assert.IsType<ArgumentException>(e);
        Assert.Contains("above its upper", e.Message, StringComparison.Ordinal);

        // a zero-width dimension is a box
        Assert.Null(ReturnsWithin(() => Sampler(kind, new Vector(2, 0.5), new Vector(2, 0.5)).Sample(new Vector(2, 0.0), new Vector(2, 0.5), new Vector(2, 1.0))));
    }

    [Theory]
    [InlineData("ring")]
    [InlineData("mirror")]
    public void ARingOrMirrorSamplersBoundsOfDifferentSizesAreRefused(string kind)
    {
        Assert.Throws<ArgumentException>(() => Sampler(kind, new Vector(2, 0.0), new Vector(1, 1.0)));
        Assert.Throws<ArgumentException>(() => Sampler(kind, new Vector(1, 0.0), new Vector(1, 1.0)).Sample(new Vector(2, 0.0), new Vector(2, 0.5), new Vector(2, 1.0)));
    }

    [Theory]
    [InlineData(5.0, 0.0, 1.0, 1.0)]
    [InlineData(-4.0, 0.0, 1.0, 1.0)]
    [InlineData(2.0000001, 0.0, 1.0, 1.0)]
    [InlineData(2.0, 0.0, 1.0, 1.0)]
    [InlineData(-1.0, 0.0, 1.0, 1.0)]
    [InlineData(0.5, 1.0, 1.0, 1.0)]
    [InlineData(1.5, 1.0, 1.0, 1.0)]
    [InlineData(1.5, 0.0, 1.0, 0.0)]
    [InlineData(-0.5, 0.0, 1.0, 0.0)]
    public void TheVeryFastSamplerFromAPointNoDrawBringsIntoItsBoxIsRefused(double from, double lower, double upper, double t)
    {
        var sampler = new SamplerVeryFastAnnealing(new Vector(1, lower), new Vector(1, upper), 42);
        Exception? e = ReturnsWithin(() => sampler.Sample(new Vector(1, 0.0), new Vector(1, from), new Vector(1, t)));
        Assert.IsType<ArgumentException>(e);
        Assert.Contains("no draw reaches", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.5, 0.0, 1.0)]
    [InlineData(1.9, 0.0, 1.0)]
    [InlineData(-0.9, 0.0, 1.0)]
    [InlineData(1.999999, 0.0, 1.0)]
    [InlineData(-0.999999, 0.0, 1.0)]
    [InlineData(1.0, 1.0, 1.0)]
    public void TheVeryFastSamplerFromAPointWithinReachDraws(double from, double lower, double upper)
    {
        var sampler = new SamplerVeryFastAnnealing(new Vector(1, lower), new Vector(1, upper), 42);
        var point = new Vector(1, double.NaN);
        Assert.Null(ReturnsWithin(() => sampler.Sample(point, new Vector(1, from), new Vector(1, 1.0))));
        Assert.InRange(point[0], lower, upper);
    }

    [Fact]
    public void AnAnnealingStartedOutOfTheVeryFastSamplersReachIsRefused()
    {
        var annealing = new HybridSimulatedAnnealing<SamplerVeryFastAnnealing, ProbabilityBoltzmannDownhill, TemperatureVeryFastAnnealing, ReannealingTrivial>(
            new SamplerVeryFastAnnealing(new Vector(1, 0.0), new Vector(1, 1.0), 42),
            new ProbabilityBoltzmannDownhill(43),
            new TemperatureVeryFastAnnealing(1.0, 0.01, 100.0, 1),
            new ReannealingTrivial(),
            optimizeScheme: HybridSimulatedAnnealingLocalOptimizeScheme.NoLocalOptimize);
        var problem = new Problem(new Square(), new NoConstraint(), new Vector(1, 5.0).AsSpan());
        Exception? e = ReturnsWithin(() => annealing.Minimize(problem, new EndCriteria(100, 50, 1e-8, 1e-8, 1e-8)));
        Assert.IsType<ArgumentException>(e);
    }

    public static readonly TheoryData<string, Action> SeedZero = new()
    {
        { "ParticleSwarmOptimization", () => _ = new ParticleSwarmOptimization(5, new GlobalTopology(), new TrivialInertia(), 2.05, 2.05, 0) },
        { "SimpleRandomInertia", () => _ = new SimpleRandomInertia(0.5, 0) },
        { "LevyFlightInertia", () => _ = new LevyFlightInertia(1.5, 2, 0) },
        { "ClubsTopology", () => _ = new ClubsTopology(2, 4, 3, 1, 5, 0) },
        { "FireflyAlgorithm", () => _ = new FireflyAlgorithm(5, new ExponentialIntensity(1.0, 0.2, 1.0), new GaussianWalk(0.1, 0.9, 1), 0, 1.0, 0.5, 0) },
        { "GaussianWalk", () => _ = new GaussianWalk(0.1, 0.9, 0) },
        { "LevyFlightWalk", () => _ = new LevyFlightWalk(1.5, 0.5, 0.9, 0) },
        { "DecreasingGaussianWalk", () => _ = new DecreasingGaussianWalk(0.1, 0.9, 0) },
        { "IsotropicRandomWalk", () => _ = new IsotropicRandomWalk<LevyFlightDistribution>(new MersenneTwisterUniformRng(1), new LevyFlightDistribution(), 2, null, 0) },
        { "ZigguratRng", () => _ = new ZigguratRng(0) },
        { "SamplerGaussian", () => _ = new SamplerGaussian(0) },
        { "SamplerLogNormal", () => _ = new SamplerLogNormal(0) },
        { "SamplerRingGaussian", () => _ = new SamplerRingGaussian(new Vector(1, 0.0), new Vector(1, 1.0), 0) },
        { "SamplerMirrorGaussian", () => _ = new SamplerMirrorGaussian(new Vector(1, 0.0), new Vector(1, 1.0), 0) },
        { "SamplerCauchy", () => _ = new SamplerCauchy(0) },
        { "SamplerVeryFastAnnealing", () => _ = new SamplerVeryFastAnnealing(new Vector(1, 0.0), new Vector(1, 1.0), 0) },
        { "ProbabilityBoltzmann", () => _ = new ProbabilityBoltzmann(0) },
        { "ProbabilityBoltzmannDownhill", () => _ = new ProbabilityBoltzmannDownhill(0) },
    };

    [Fact]
    public void ALaplaceInterpolationsGivenCoordinatesMatchTheMatrix()
    {
        // QuantLib reads the coordinates at each row and column, past a shorter set's end; a longer set is refused too,
        // as its points have no row or column. With none given, the indices serve.
        var A = new Matrix(new double[,] { { 1.0, double.NaN, 4.0 }, { 6.0, 6.5, 7.0 } });
        ArgumentException e = Assert.Throws<ArgumentException>(() => LaplaceInterpolationFunctions.LaplaceInterpolation(A, [1.0, 2.0], [1.0, 3.0]));
        Assert.Equal("x", e.ParamName);
        Assert.StartsWith("2 x values given for 3 columns", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentException>(() => LaplaceInterpolationFunctions.LaplaceInterpolation(A, [1.0, 2.0, 4.0], [1.0, 3.0, 7.0]));
        Assert.Equal("y", e.ParamName);
        Assert.StartsWith("3 y values given for 2 rows", e.Message, StringComparison.Ordinal);

        Assert.True(double.IsFinite(LaplaceInterpolationFunctions.LaplaceInterpolation(A, [1.0, 2.0, 4.0], [1.0, 3.0])[0, 1]));
        Assert.True(double.IsFinite(LaplaceInterpolationFunctions.LaplaceInterpolation(A)[0, 1]));
    }

    [Theory]
    [MemberData(nameof(SeedZero))]
    public void SeedZeroIsRefused(string type, Action create)
    {
        Assert.True(type.Length > 0);
        Assert.Throws<ArgumentOutOfRangeException>(create);
    }

    private static IAnnealingSamplerProbe Sampler(string kind, Vector lower, Vector upper) => kind switch
    {
        "ring" => new Probe<SamplerRingGaussian>(new SamplerRingGaussian(lower, upper, 42)),
        "mirror" => new Probe<SamplerMirrorGaussian>(new SamplerMirrorGaussian(lower, upper, 42)),
        _ => new Probe<SamplerVeryFastAnnealing>(new SamplerVeryFastAnnealing(lower, upper, 42)),
    };

    // The action's exception, or null, asserting that it returns within ten seconds: QuantLib's loops never do.
    private static Exception? ReturnsWithin(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                caught = e;
            }
        })
        {
            IsBackground = true,
        };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "no return in 10 s");
        return caught;
    }

    private interface IAnnealingSamplerProbe
    {
        void Sample(Vector newPoint, Vector currentPoint, Vector temp);
    }

    private sealed class Probe<T>(T sampler) : IAnnealingSamplerProbe
        where T : IAnnealingSampler<T>
    {
        public void Sample(Vector newPoint, Vector currentPoint, Vector temp) => sampler.Sample(newPoint, currentPoint, temp);
    }

    private sealed class Square : CostFunction
    {
        public override double Value(Vector x) => (x[0] - 0.3) * (x[0] - 0.3);

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
