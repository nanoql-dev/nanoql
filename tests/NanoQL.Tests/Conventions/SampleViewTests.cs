// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A sequence draw's sample is a view of the generator's buffer, as QuantLib's <c>const sample_type&amp;</c> is (playbook
/// §2.7): the next draw overwrites it, <c>LastSequence</c> is the same buffer, and a caller that keeps a sequence
/// copies it. So no draw allocates.
/// </summary>
public class SampleViewTests
{
    [Fact]
    public void ASequenceSampleIsTheGeneratorsBuffer()
    {
        IRsg[] generators =
        [
            RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(4, 7),
            new SobolRsg(4, 7),
            new HaltonRsg(4, 7),
            PseudoRandom.MakeSequenceGenerator(4, 7),
            LowDiscrepancy.MakeSequenceGenerator(4, 7),
        ];
        foreach (IRsg generator in generators)
        {
            Sample<ReadOnlyMemory<double>> first = generator.NextSequence();
            double[] kept = first.Value.ToArray();
            Sample<ReadOnlyMemory<double>> second = generator.NextSequence();

            Assert.True(first.Value.Span.Overlaps(second.Value.Span));
            Assert.True(generator.LastSequence().Value.Span.Overlaps(second.Value.Span));
            Assert.Equal(second.Value.ToArray(), first.Value.ToArray());
            Assert.NotEqual(kept, first.Value.ToArray());

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                generator.NextSequence();
            }

            Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        }

        // The Sobol integers too: the next draw overwrites them.
        var sobol = new SobolRsg(4, 7);
        ReadOnlyMemory<long> integers = sobol.NextInt32Sequence();
        long[] keptIntegers = integers.ToArray();
        ReadOnlyMemory<long> next = sobol.NextInt32Sequence();
        Assert.True(integers.Span.Overlaps(next.Span));
        Assert.NotEqual(keptIntegers, integers.ToArray());
    }
}
