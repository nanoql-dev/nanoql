// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The discrete integrators with too few evaluations. With fewer than two, QuantLib's count of intervals, an unsigned
/// <c>Size</c>, wraps below zero and its loop runs about 2⁶⁴ times (<c>discreteintegrals.cpp:74-79</c>, <c>:92</c>);
/// nanoql refuses them, at construction and where the count is set below two since. With two, one interval, QuantLib's
/// Simpson integrator closes its odd interval count by an end correction that counts f(a) a second time: on exp over
/// [0, 1] it gives 2.5258075808961893, where its own <c>DiscreteSimpsonIntegral</c> on the same two points gives the
/// trapezoid's 1.8591409142295225 (the C++ harness); nanoql's integrator gives the trapezoid there. QuantLib's check:
/// its array form.
/// </summary>
public class DiscreteIntegratorFewPointsTests
{
    [Fact]
    public void BelowTwoEvaluationsTheIntegratorsRefuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscreteTrapezoidIntegrator(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscreteTrapezoidIntegrator(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscreteSimpsonIntegrator(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscreteSimpsonIntegrator(0));

        // a count set below two since is refused where the rule runs
        Integrator trapezoid = new DiscreteTrapezoidIntegrator(10).SetMaxEvaluations(1);
        Integrator simpson = new DiscreteSimpsonIntegrator(10).SetMaxEvaluations(1);
        Assert.Throws<NanoQLException>(() => trapezoid.Integrate(Math.Exp, 0.0, 1.0));
        Assert.Throws<NanoQLException>(() => simpson.Integrate(Math.Exp, 0.0, 1.0));
    }

    [Fact]
    public void AtTwoEvaluationsSimpsonIsTheTrapezoid()
    {
        IntegrationResult r = new DiscreteSimpsonIntegrator(2).IntegrateWithStatistics(Math.Exp, 0.0, 1.0);
        double arrayForm = DiscreteSimpsonIntegral.Integrate([0.0, 1.0], [1.0, Math.Exp(1.0)]);
        Assert.Equal(arrayForm, r.Value);
        Assert.Equal(1.8591409142295225, r.Value);
        Assert.Equal(2, r.NumberOfEvaluations);
        Assert.NotEqual(2.5258075808961893, r.Value);

        // and it is the trapezoid integrator's value, reversed too
        Assert.Equal(new DiscreteTrapezoidIntegrator(2).Integrate(Math.Sin, 0.25, 3.0), new DiscreteSimpsonIntegrator(2).Integrate(Math.Sin, 0.25, 3.0));
        Assert.Equal(-new DiscreteSimpsonIntegrator(2).Integrate(Math.Sin, 0.25, 3.0), new DiscreteSimpsonIntegrator(2).Integrate(Math.Sin, 3.0, 0.25));
    }
}
