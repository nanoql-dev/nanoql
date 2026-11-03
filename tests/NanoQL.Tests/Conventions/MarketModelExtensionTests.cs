// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market-model extension points (the playbook's §2.4): a correlation implementing its matrices alone answers each
/// period from them through the base's <see cref="PiecewiseConstantCorrelation.Correlation"/>, with QuantLib's index
/// check; and a model overriding <see cref="MarketModel.Covariance"/> gives its volatilities from the override, as
/// QuantLib's <c>timeDependentVolatility</c> reads the virtual <c>covariance(j)</c>.
/// </summary>
public class MarketModelExtensionTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0];

    [Fact]
    public void ACorrelationOfItsMatricesAloneAnswersEachPeriod()
    {
        var inner = new ExponentialForwardCorrelation(RateTimes, 0.5, 0.2, 0.5);
        var corr = new MatricesOnly(inner);
        for (int i = 0; i < inner.Times.Length; ++i)
        {
            Matrix expected = inner.Correlation(i), actual = corr.Correlation(i);
            Assert.Equal(expected.Rows, actual.Rows);
            for (int r = 0; r < expected.Rows; ++r)
            {
                Assert.Equal(expected.Row(r).ToArray(), actual.Row(r).ToArray());
            }
        }

        int n = inner.Times.Length;
        var past = Assert.Throws<ArgumentOutOfRangeException>(() => corr.Correlation(n));
        Assert.StartsWith($"index ({n}) must be less than correlations vector size ({n})", past.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => corr.Correlation(-1));
    }

    [Fact]
    public void AModelsCovarianceOverrideGivesItsVolatilities()
    {
        var inner = new FlatVol(
            [0.2, 0.21, 0.22],
            new ExponentialForwardCorrelation(RateTimes, 0.5, 0.2),
            new EvolutionDescription(RateTimes),
            3,
            [0.03, 0.031, 0.032],
            [0.0, 0.0, 0.0]);
        var scaled = new FourTimesTheCovariance(inner);
        for (int i = 0; i < inner.NumberOfRates; ++i)
        {
            ImmutableArray<double> expected = inner.TimeDependentVolatility(i), actual = scaled.TimeDependentVolatility(i);
            for (int j = 0; j < expected.Length; ++j)
            {
                // four times the variance is twice the volatility, exactly
                Assert.Equal(2.0 * expected[j], actual[j]);
            }

            Assert.Contains(expected, v => v > 0.0);
        }
    }

    private sealed class MatricesOnly(PiecewiseConstantCorrelation inner) : PiecewiseConstantCorrelation
    {
        public override ImmutableArray<double> Times => inner.Times;

        public override ImmutableArray<double> RateTimes => inner.RateTimes;

        public override int NumberOfRates => inner.NumberOfRates;

        public override ImmutableArray<Matrix> Correlations() => inner.Correlations();
    }

    private sealed class FourTimesTheCovariance(MarketModel inner) : MarketModel
    {
        public override ImmutableArray<double> InitialRates => inner.InitialRates;

        public override ImmutableArray<double> Displacements => inner.Displacements;

        public override EvolutionDescription Evolution => inner.Evolution;

        public override int NumberOfRates => inner.NumberOfRates;

        public override int NumberOfFactors => inner.NumberOfFactors;

        public override int NumberOfSteps => inner.NumberOfSteps;

        public override Matrix PseudoRoot(int i) => inner.PseudoRoot(i);

        public override Matrix Covariance(int i)
        {
            Matrix c = inner.Covariance(i);
            for (int r = 0; r < c.Rows; ++r)
            {
                for (int k = 0; k < c.Columns; ++k)
                {
                    c[r, k] *= 4.0;
                }
            }

            return c;
        }
    }
}
