// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="StochasticProcess"/>'s and <see cref="StochasticProcess1D"/>'s defaults on a process built without a
/// discretization. QuantLib's expectation, standard deviation, covariance and variance call the discretization through
/// a pointer (<c>stochasticprocess.cpp:36-52</c>, <c>:78-88</c>), which is null when the process was built without
/// one, a result C++ leaves undefined. nanoql refuses with <see cref="NanoQLException"/>, "no discretization given".
/// </summary>
public class StochasticProcessDiscretizationRefusalTests
{
    [Fact]
    public void TheDefaultsRefuseWithoutADiscretization()
    {
        var p = new NoDiscretization();

        foreach (Action call in new Action[]
        {
            () => p.Expectation(0.0, 1.0, 0.5),
            () => p.StdDeviation(0.0, 1.0, 0.5),
            () => p.Variance(0.0, 1.0, 0.5),
            () => p.Evolve(0.0, 1.0, 0.5, 0.1),
            () => ((StochasticProcess)new NoDiscretizationND()).Expectation(0.0, new Vector(1, 1.0), 0.5),
            () => ((StochasticProcess)new NoDiscretizationND()).StdDeviation(0.0, new Vector(1, 1.0), 0.5),
            () => ((StochasticProcess)new NoDiscretizationND()).Covariance(0.0, new Vector(1, 1.0), 0.5),
        })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(call);
            Assert.StartsWith("no discretization given", e.Message, StringComparison.Ordinal);
        }
    }

    // A 1-D process that overrides only what the base leaves abstract, built without a discretization.
    private sealed class NoDiscretization : StochasticProcess1D
    {
        public override double X0 => 1.0;

        public override double Drift(double t, double x) => 0.0;

        public override double Diffusion(double t, double x) => 0.2;
    }

    // A multi-dimensional process that overrides only what the base leaves abstract, built without a discretization.
    private sealed class NoDiscretizationND : StochasticProcess
    {
        public override int Size => 1;

        public override Vector InitialValues() => new(1, 1.0);

        public override Vector Drift(double t, Vector x) => new(1, 0.0);

        public override Matrix Diffusion(double t, Vector x) => new(1, 1, 0.2);
    }
}
