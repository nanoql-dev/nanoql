// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's <c>InverseCumulativeRsg::nextSequence</c> maps the uniform sample at every index below the dimension it
/// read at construction (<c>inversecumulativersg.hpp:89-90</c>), so a uniform sequence generator that draws fewer
/// values reads past the end of its sample, which C++ leaves undefined. None of QuantLib's generators does, but
/// <see cref="IRsg"/> is an extension point: nanoql's composite refuses such a draw with
/// <see cref="NanoQLException"/>. A longer draw is read as QuantLib reads it, its first values only.
/// </summary>
public class ShortUniformSequenceTests
{
    [Fact]
    public void ADrawShorterThanTheDimensionIsRefused()
    {
        var rsg = new InverseCumulativeRsg<Shrinking, InverseCumulativeNormal>(new Shrinking());
        Assert.Equal(3, rsg.Dimension);

        // The first draw holds the dimension's three values; the second holds two.
        Assert.Equal(3, rsg.NextSequence().Value.Length);
        NanoQLException e = Assert.Throws<NanoQLException>(() => rsg.NextSequence());
        Assert.Equal("the uniform sequence generator drew 2 values, fewer than the dimension (3)", e.Message);

        // A longer draw is mapped on its first three values.
        Sample<ReadOnlyMemory<double>> longer = rsg.NextSequence();
        Assert.Equal([0.0, 0.0, 0.0], longer.Value.ToArray());
    }

    // A uniform sequence generator of dimension 3 whose draws hold 3, 2 and then 4 values of 0.5.
    private sealed class Shrinking : IRsg
    {
        private readonly int[] lengths = [3, 2, 4];
        private int draws;
        private double[] last = [];

        public int Dimension => 3;

        public Sample<ReadOnlyMemory<double>> NextSequence()
        {
            last = Enumerable.Repeat(0.5, lengths[Math.Min(draws, lengths.Length - 1)]).ToArray();
            draws++;
            return new(last, 1.0);
        }

        public Sample<ReadOnlyMemory<double>> LastSequence() => new(last, 1.0);

        public IRsg Clone() => new Shrinking { draws = draws, last = (double[])last.Clone() };
    }
}
