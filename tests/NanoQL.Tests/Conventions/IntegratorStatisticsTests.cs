// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The integrators are immutable (the S-eq plan's SEQR4). QuantLib's <c>Integrator</c> keeps the last call's number of
/// evaluations in a mutable member, read by <c>numberOfEvaluations()</c> after the call, and its setters change the
/// integrator (<c>integral.hpp:30-70</c>); nanoql returns each call's count with its value through
/// <see cref="Integrator.IntegrateWithStatistics"/>, and <see cref="Integrator.SetAbsoluteAccuracy"/> and
/// <see cref="Integrator.SetMaxEvaluations"/> return a changed copy. QuantLib's values are the vectors' (the wheel's
/// counts after each call, and the C++ harness's <c>integral</c> file for the settings).
/// </summary>
public class IntegratorStatisticsTests
{
    [Fact]
    public void EachCallsCountIsItsOwn()
    {
        var simpson = new SimpsonIntegral(1e-8, 100);

        IntegrationResult first = simpson.IntegrateWithStatistics(Math.Exp, 0.0, 1.0);
        IntegrationResult second = simpson.IntegrateWithStatistics(Math.Sin, 0.0, Math.PI);
        IntegrationResult again = simpson.IntegrateWithStatistics(Math.Exp, 0.0, 1.0);

        // QuantLib resets its count at each call (integral.cpp:78), so its numberOfEvaluations() reads each call's own
        Assert.True(first.NumberOfEvaluations > 0);
        Assert.Equal(first, again);
        Assert.NotEqual(first.NumberOfEvaluations, second.NumberOfEvaluations);
        Assert.Equal(0, simpson.IntegrateWithStatistics(Math.Exp, 1.0, 1.0).NumberOfEvaluations);
        Assert.Equal(first.Value, simpson.Integrate(Math.Exp, 0.0, 1.0));
    }

    [Fact]
    public void TheSettersReturnAChangedCopy()
    {
        var lobatto = new GaussLobattoIntegral(1000, 1e-6);

        Integrator tighter = lobatto.SetAbsoluteAccuracy(1e-12);
        Integrator shorter = lobatto.SetMaxEvaluations(10);

        Assert.Equal(1e-6, lobatto.AbsoluteAccuracy);
        Assert.Equal(1000, lobatto.MaxEvaluations);
        Assert.IsType<GaussLobattoIntegral>(tighter);
        Assert.Equal(1e-12, tighter.AbsoluteAccuracy);
        Assert.Equal(1000, tighter.MaxEvaluations);
        Assert.Equal(1e-6, shorter.AbsoluteAccuracy);
        Assert.Equal(10, shorter.MaxEvaluations);

        // the copy integrates as QuantLib's integrator does after its setter, as one built with those settings
        Assert.Equal(
            new GaussLobattoIntegral(1000, 1e-12).IntegrateWithStatistics(Math.Sqrt, 0.0, 1.0),
            tighter.IntegrateWithStatistics(Math.Sqrt, 0.0, 1.0));
        Assert.Equal(
            Assert.Throws<NanoQLException>(() => new GaussLobattoIntegral(10, 1e-6).Integrate(Math.Sqrt, 0.0, 1.0)).Message,
            Assert.Throws<NanoQLException>(() => shorter.Integrate(Math.Sqrt, 0.0, 1.0)).Message);

        // QuantLib's setter takes any accuracy unchecked, where its constructor refuses one not above epsilon
        Assert.Equal(0.0, lobatto.SetAbsoluteAccuracy(0.0).AbsoluteAccuracy);
    }
}
