// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// An integration's error estimate: QuantLib's integrators keep it in a mutable member, which only the non-adaptive
/// Gauss-Kronrod rule, tanh-sinh and exp-sinh set, so <c>absoluteError()</c> reads an unset member on every other;
/// nanoql's <see cref="IntegrationResult.AbsoluteError"/> is <see langword="null"/> there, and
/// <see cref="Integrator.IntegrationSuccess"/>, which reads it, refuses a result without one rather than compare an
/// unset value. An empty interval's integral is exact: an error of 0, which every integrator reports.
/// </summary>
public class IntegrationErrorTests
{
    private static readonly Integrator[] WithoutEstimate =
    [
        new SimpsonIntegral(1e-8, 20),
        new TrapezoidIntegral<Default>(1e-8, 20),
        new TrapezoidIntegral<MidPoint>(1e-8, 20),
        new GaussLobattoIntegral(1000, 1e-8),
        new SegmentIntegral(100),
        new GaussLegendreIntegrator(16),
        new GaussChebyshevIntegrator(16),
        new GaussChebyshev2ndIntegrator(16),
        new GaussKronrodAdaptive(1e-8),
    ];

    [Fact]
    public void TheRulesWithoutAnEstimateReportNone()
    {
        foreach (Integrator integrator in WithoutEstimate)
        {
            IntegrationResult result = integrator.IntegrateWithStatistics(Math.Exp, 0.0, 1.0);

            // the Chebyshev rules integrate against their weights; every other rule integrates eˣ itself
            bool weighted = integrator is GaussChebyshevIntegrator or GaussChebyshev2ndIntegrator;
            Assert.True(
                weighted ? double.IsFinite(result.Value) : Math.Abs(result.Value - (Math.E - 1.0)) <= 1e-4,
                $"{integrator.GetType().Name}: {result.Value}");
            Assert.Null(result.AbsoluteError);
            NanoQLException e = Assert.Throws<NanoQLException>(() => integrator.IntegrationSuccess(result));
            Assert.Equal("the integrator computes no error estimate", e.Message);
        }
    }

    [Fact]
    public void TheNonAdaptiveRuleReportsQuantLibsEstimate()
    {
        // QuantLib's values (the harness vectors' kronrodintegral, its first case): 1/(1+x²) on [-5, 5] converges in
        // 87 points; √x on [0, 4] uses its 87 points and misses the absolute accuracy.
        var rule = new GaussKronrodNonAdaptive(1e-6, 100, 1e-6);
        IntegrationResult lorentz = rule.IntegrateWithStatistics(x => 1.0 / (1.0 + (x * x)), -5.0, 5.0);
        Assert.Equal((2.7468015338900504, 87, 3.8702076883163194e-07), (lorentz.Value, lorentz.NumberOfEvaluations, lorentz.AbsoluteError));
        Assert.True(rule.IntegrationSuccess(lorentz));
        IntegrationResult root = rule.IntegrateWithStatistics(Math.Sqrt, 0.0, 4.0);
        Assert.Equal((5.3333334271192765, 87, 5.878450300205771e-06), (root.Value, root.NumberOfEvaluations, root.AbsoluteError));
        Assert.False(rule.IntegrationSuccess(root));

        // over its evaluations: the error is within the accuracy, the 87 points over 50
        Assert.False(new GaussKronrodNonAdaptive(1e-6, 50, 1e-6).IntegrationSuccess(lorentz));
    }

    [Fact]
    public void TheTanhSinhRuleReportsBoostsEstimate()
    {
        // QuantLib's values (the vectors' tanhsinhintegral and the harness's tanh_sinh, their first cases): eˣ on [0, 1]
        // in 58 evaluations, Boost's error 1.5459454355661034e-11; the rule's accuracy is QL_MAX_REAL and its maximum
        // unbounded, as QuantLib's, so every integration with an estimate succeeds
        var rule = new TanhSinhIntegral();
        IntegrationResult result = rule.IntegrateWithStatistics(Math.Exp, 0.0, 1.0);
        Assert.Equal((1.718281828459045, 58, 1.5459454355661034e-11), (result.Value, result.NumberOfEvaluations, result.AbsoluteError));
        Assert.True(rule.IntegrationSuccess(result));
        IntegrationResult reversed = rule.IntegrateWithStatistics(Math.Exp, 1.0, 0.0);
        Assert.Equal((-1.718281828459045, 58, 1.5459454355661034e-11), (reversed.Value, reversed.NumberOfEvaluations, reversed.AbsoluteError));
    }

    [Fact]
    public void AnEmptyIntervalIsExact()
    {
        foreach (Integrator integrator in WithoutEstimate.Append(new GaussKronrodNonAdaptive(1e-6, 100, 1e-6)).Append(new TanhSinhIntegral()))
        {
            IntegrationResult result = integrator.IntegrateWithStatistics(Math.Exp, 2.0, 2.0);
            Assert.Equal((0.0, 0, 0.0), (result.Value, result.NumberOfEvaluations, result.AbsoluteError));
            Assert.True(integrator.IntegrationSuccess(result));
        }
    }
}
