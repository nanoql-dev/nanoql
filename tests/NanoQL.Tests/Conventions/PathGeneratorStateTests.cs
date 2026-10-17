// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using Gsg = NanoQL.Numerics.InverseCumulativeRsg<
    NanoQL.Numerics.RandomSequenceGenerator<NanoQL.Numerics.MersenneTwisterUniformRng>,
    NanoQL.Numerics.InverseCumulativeNormal>;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="PathGenerator{TGsg}"/> is a working type with QuantLib's buffer semantics (docs/porting-playbook.md
/// §2.7): it copies the sequence generator it is given, as QuantLib's by-value constructor does, so the caller's
/// later draws from that generator change nothing in its paths; a draw's sample holds the generator's own path, a
/// view the next draw, or the antithetic, overwrites, as QuantLib's <c>const sample_type&amp;</c> is; and
/// <c>Clone()</c> is a deep copy, as QuantLib's copy constructor is.
/// </summary>
public class PathGeneratorStateTests
{
    private const int Steps = 12;

    [Fact]
    public void PathGeneratorCopiesItsGenerator()
    {
        var process = new OrnsteinUhlenbeckProcess(0.1, 0.2);
        Gsg callers = Generator(), untouched = Generator();
        var generator = new PathGenerator<Gsg>(process, 10.0, Steps, callers, false);
        var reference = new PathGenerator<Gsg>(process, 10.0, Steps, untouched, false);

        // The caller draws from its own generator, before and between the path generator's draws.
        for (int k = 0; k < 5; k++)
        {
            callers.NextSequence();
        }

        for (int k = 0; k < 3; k++)
        {
            double[] drawn = Values(generator.Next().Value);
            callers.NextSequence();
            Assert.Equal(Values(reference.Next().Value), drawn);
        }

        // Its own generator moved only with its draws: the caller's is 5 + 3 draws ahead of a fresh one.
        Gsg fresh = Generator();
        for (int k = 0; k < 8; k++)
        {
            fresh.NextSequence();
        }

        Assert.Equal(fresh.NextSequence().Value.ToArray(), callers.NextSequence().Value.ToArray());
    }

    [Fact]
    public void APathSampleIsTheGeneratorsOwnPath()
    {
        var generator = new PathGenerator<Gsg>(new OrnsteinUhlenbeckProcess(0.1, 0.2), 10.0, Steps, Generator(), false);

        Sample<MonteCarloPath> first = generator.Next();
        double[] firstValues = Values(first.Value);
        Sample<MonteCarloPath> second = generator.Next();

        // The same path, overwritten by the second draw.
        Assert.Same(first.Value, second.Value);
        Assert.Equal(Values(second.Value), Values(first.Value));
        Assert.NotEqual(firstValues, Values(first.Value));

        // The antithetic overwrites it too, with the second draw's increments negated.
        double[] secondValues = Values(second.Value);
        Sample<MonteCarloPath> antithetic = generator.Antithetic();
        Assert.Same(second.Value, antithetic.Value);
        Assert.NotEqual(secondValues, Values(second.Value));

        // A caller that keeps a path copies it: the copy keeps its values through later draws.
        var kept = new MonteCarloPath(antithetic.Value.TimeGrid, [.. antithetic.Value]);
        double[] keptValues = Values(kept);
        generator.Next();
        Assert.Equal(keptValues, Values(kept));
    }

    [Fact]
    public void CloneIsDeep()
    {
        var original = new PathGenerator<Gsg>(new OrnsteinUhlenbeckProcess(0.1, 0.2), 10.0, Steps, Generator(), true);
        original.Next();
        PathGenerator<Gsg> clone = original.Clone();
        Assert.Same(original.TimeGrid, clone.TimeGrid);
        Assert.Equal(original.Size, clone.Size);

        // The clone's antithetic and next paths equal the original's, each into its own path.
        Sample<MonteCarloPath> originalAntithetic = original.Antithetic(), cloneAntithetic = clone.Antithetic();
        Assert.NotSame(originalAntithetic.Value, cloneAntithetic.Value);
        Assert.Equal(Values(originalAntithetic.Value), Values(cloneAntithetic.Value));
        Sample<MonteCarloPath> originalNext = original.Next(), cloneNext = clone.Next();
        Assert.NotSame(originalNext.Value, cloneNext.Value);
        Assert.Equal(Values(originalNext.Value), Values(cloneNext.Value));

        // Drawing from one leaves the other unchanged: its path, and its next draw, which equals the other's.
        double[] cloneValues = Values(cloneNext.Value);
        double[][] originalDraws = [.. Enumerable.Range(0, 3).Select(_ => Values(original.Next().Value))];
        Assert.Equal(cloneValues, Values(cloneNext.Value));
        foreach (double[] drawn in originalDraws)
        {
            Assert.Equal(drawn, Values(clone.Next().Value));
        }

        // A named class's clone keeps its type.
        var named = new InvCumulativeMersenneTwisterPathGenerator(new OrnsteinUhlenbeckProcess(0.1, 0.2), 10.0, Steps, Generator(), false);
        InvCumulativeMersenneTwisterPathGenerator namedClone = named.Clone();
        Assert.Equal(Values(named.Next().Value), Values(namedClone.Next().Value));
        var sobol = new GaussianSobolPathGenerator(
            new OrnsteinUhlenbeckProcess(0.1, 0.2), 10.0, Steps, new InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>(new SobolRsg(Steps, 42)), false);
        GaussianSobolPathGenerator sobolClone = sobol.Clone();
        Assert.Equal(Values(sobol.Next().Value), Values(sobolClone.Next().Value));
    }

    private static Gsg Generator() =>
        new(new RandomSequenceGenerator<MersenneTwisterUniformRng>(Steps, new MersenneTwisterUniformRng(42)));

    private static double[] Values(MonteCarloPath path)
    {
        var values = new List<double>();
        foreach (double x in path)
        {
            values.Add(x);
        }

        return [.. values];
    }
}
