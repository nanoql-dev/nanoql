// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's <c>PathGenerator</c> takes any <c>StochasticProcess</c> and casts it to <c>StochasticProcess1D</c>
/// (<c>ext::dynamic_pointer_cast</c> in <c>pathgenerator.hpp</c>): a process that isn't 1-D casts to null, which
/// QuantLib keeps, and its first draw dereferences it (<c>process_-&gt;x0()</c>). nanoql refuses such a process when the
/// generator is built, in both constructors, with an <see cref="ArgumentException"/> naming the process.
/// </summary>
public class PathGeneratorProcessTests
{
    [Fact]
    public void AProcessThatIsNotOneDimensionalIsRefused()
    {
        var process = new TwoDimensionalProcess();
        var generator = new ZeroSequenceGenerator(4);

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new PathGenerator<ZeroSequenceGenerator>(process, 1.0, 4, generator, false));
        Assert.Equal("process", e.ParamName);

        e = Assert.Throws<ArgumentException>(
            () => new PathGenerator<ZeroSequenceGenerator>(process, new TimeGrid(1.0, 4), generator, true));
        Assert.Equal("process", e.ParamName);
    }

    // A process of two dimensions, which nothing here evaluates.
    private sealed class TwoDimensionalProcess : StochasticProcess
    {
        public override int Size => 2;

        public override Vector InitialValues() => new(2);

        public override Vector Drift(double t, Vector x) => new(2);

        public override Matrix Diffusion(double t, Vector x) => new(2, 2);

        public override Vector Expectation(double t0, Vector x0, double dt) => x0.Clone();

        public override Matrix StdDeviation(double t0, Vector x0, double dt) => new(2, 2);

        public override Matrix Covariance(double t0, Vector x0, double dt) => new(2, 2);
    }

    // A sequence generator drawing zeros, which the refusal comes before.
    private sealed class ZeroSequenceGenerator : IRsg
    {
        private readonly double[] sequence;

        public ZeroSequenceGenerator(int dimension) => sequence = new double[dimension];

        public int Dimension => sequence.Length;

        public Sample<ReadOnlyMemory<double>> NextSequence() => new(sequence, 1.0);

        public Sample<ReadOnlyMemory<double>> LastSequence() => new(sequence, 1.0);

        public IRsg Clone() => new ZeroSequenceGenerator(sequence.Length);
    }
}
