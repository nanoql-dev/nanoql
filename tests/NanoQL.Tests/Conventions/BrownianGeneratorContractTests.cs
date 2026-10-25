// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Brownian generators' contract (the S-hes plan's HR3 (iv)): <see cref="BrownianGeneratorFactory.Create"/> returns
/// a new generator each call, each drawing its own sequence from the factory's seed;
/// <see cref="BrownianGenerator.NextStep"/> and <see cref="BrownianGenerator.NextPath"/> return the draw's weight, 1
/// for the Mersenne-Twister and Sobol generators, as QuantLib's; and seeds are explicit, in [1, 4294967295], where
/// QuantLib's 0 draws one from the clock.
/// </summary>
public class BrownianGeneratorContractTests
{
    private static readonly BrownianGeneratorFactory[] Factories =
    [
        new MTBrownianGeneratorFactory(1234),
        new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 1234, SobolRsgDirectionIntegers.JoeKuoD7),
    ];

    [Fact]
    public void EachCreateReturnsANewGeneratorDrawingFromTheSeed()
    {
        foreach (BrownianGeneratorFactory factory in Factories)
        {
            BrownianGenerator first = factory.Create(2, 3);
            BrownianGenerator second = factory.Create(2, 3);
            Assert.NotSame(first, second);

            // drawing a path from the first leaves the second at its start: its first path is the first's
            double[][] path = Draw(first);
            Assert.Equal(path, Draw(second));
            Assert.NotEqual(path, Draw(first));
        }
    }

    [Fact]
    public void EveryDrawWeighsOne()
    {
        foreach (BrownianGeneratorFactory factory in Factories)
        {
            BrownianGenerator g = factory.Create(3, 4);
            double[] output = new double[3];
            for (int p = 0; p < 5; ++p)
            {
                Assert.Equal(1.0, g.NextPath());
                for (int s = 0; s < 4; ++s)
                {
                    Assert.Equal(1.0, g.NextStep(output));
                }
            }
        }
    }

    [Fact]
    public void ASeedOutsideItsRangeIsRefused()
    {
        foreach (long seed in new[] { 0L, -1L, 4294967296L })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MTBrownianGeneratorFactory(seed));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MTBrownianGenerator(1, 1, seed));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Steps, seed));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SobolBrownianGenerator(1, 1, SobolBrownianGeneratorBaseOrdering.Steps, seed));
        }
    }

    private static double[][] Draw(BrownianGenerator g)
    {
        g.NextPath();
        var steps = new double[g.NumberOfSteps][];
        for (int s = 0; s < steps.Length; ++s)
        {
            steps[s] = new double[g.NumberOfFactors];
            g.NextStep(steps[s]);
        }

        return steps;
    }
}
