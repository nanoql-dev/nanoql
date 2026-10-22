// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/gaussianstatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Utilities.DataFormattersFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// The Gaussian-assumption risk measures (value-at-risk, expected shortfall, …), from the mean and standard deviation
/// of a statistics tool: QuantLib's <c>GenericGaussianStatistics&lt;Stat&gt;</c>, which derives from its type
/// parameter, as C# 14 extension members over any <see cref="ISummaryStatistics"/>.
/// </summary>
/// <remarks>
/// <c>stats.GaussianValueAtRisk(0.99)</c> reads as QuantLib's on <see cref="GeneralStatistics"/> (QuantLib's
/// <c>GaussianStatistics</c> and, with the empirical measures, its <c>Statistics</c>), on
/// <see cref="IncrementalStatistics"/> and on <see cref="StatsHolder"/>. Where QuantLib builds a distribution from
/// <c>mean()</c> and <c>standardDeviation()</c> as constructor arguments, the standard deviation is read first, as MSVC,
/// which built QuantLib's wheel, evaluates the arguments; it decides which refusal a set too small for both gives.
/// </remarks>
public static class GaussianStatisticsExtensions
{
    extension<TStat>(TStat stats)
        where TStat : ISummaryStatistics
    {
        /// <summary>
        /// The Gaussian-assumption downside variance, the variance of observations below 0: QuantLib's
        /// <c>gaussianRegret(0.0)</c>.
        /// </summary>
        /// <returns>The downside variance.</returns>
        public double GaussianDownsideVariance() => stats.GaussianRegret(0.0);

        /// <summary>The Gaussian-assumption downside deviation, the square root of the downside variance.</summary>
        /// <returns>The downside deviation.</returns>
        public double GaussianDownsideDeviation() => Math.Sqrt(stats.GaussianDownsideVariance());

        /// <summary>
        /// The Gaussian-assumption variance of observations below a target, Σ wᵢ min(0, xᵢ − target)² / Σ wᵢ (Dembo,
        /// Freeman, "The Rules Of Risk", Wiley, 2001).
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>The regret.</returns>
        public double GaussianRegret(double target)
        {
            ArgumentNullException.ThrowIfNull(stats);
            double m = stats.Mean();
            double std = stats.StandardDeviation();
            double variance = std * std;
            var gIntegral = new CumulativeNormalDistribution(m, std);
            var g = new NormalDistribution(m, std);
            double firstTerm = variance + (m * m) - (2.0 * target * m) + (target * target);
            double alfa = gIntegral.Evaluate(target);
            double secondTerm = m - target;
            double beta = variance * g.Evaluate(target);
            double result = (alfa * firstTerm) - (beta * secondTerm);
            return result / alfa;
        }

        /// <summary>
        /// The Gaussian-assumption y-th percentile, the value x such that y = 1/√(2π) ∫_{−∞}^{x} exp(−u²/2) du, on the
        /// distribution with the statistics' mean and standard deviation.
        /// </summary>
        /// <param name="percentile">The percentile, in (0, 1), the extremes excluded.</param>
        /// <returns>The percentile.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentile"/> is outside (0, 1).</exception>
        public double GaussianPercentile(double percentile)
        {
            ArgumentNullException.ThrowIfNull(stats);
            if (!(percentile > 0.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(percentile), $"percentile ({General(percentile)}) must be > 0.0");
            }

            if (!(percentile < 1.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(percentile), $"percentile ({General(percentile)}) must be < 1.0");
            }

            double standardDeviation = stats.StandardDeviation();
            var gInverse = new InverseCumulativeNormal(stats.Mean(), standardDeviation);
            return gInverse.Evaluate(percentile);
        }

        /// <summary>The Gaussian-assumption y-th top percentile: the (1 − y)-th percentile.</summary>
        /// <param name="percentile">The percentile, in (0, 1), the extremes excluded.</param>
        /// <returns>The top percentile.</returns>
        /// <exception cref="ArgumentOutOfRangeException">1 − <paramref name="percentile"/> is outside (0, 1).</exception>
        public double GaussianTopPercentile(double percentile) => stats.GaussianPercentile(1.0 - percentile);

        /// <summary>
        /// The Gaussian-assumption potential upside at a percentile: the percentile, floored at 0, since it must be a
        /// gain.
        /// </summary>
        /// <param name="percentile">The percentile, in [0.9, 1).</param>
        /// <returns>The potential upside.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentile"/> is outside [0.9, 1).</exception>
        public double GaussianPotentialUpside(double percentile)
        {
            ArgumentNullException.ThrowIfNull(stats);
            if (!(percentile < 1.0 && percentile >= 0.9))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(percentile), $"percentile ({General(percentile)}) out of range [0.9, 1)");
            }

            double result = stats.GaussianPercentile(percentile);
            // potential upside must be a gain, i.e., floored at 0.0
            return result < 0.0 ? 0.0 : result;
        }

        /// <summary>
        /// The Gaussian-assumption value-at-risk at a percentile: the (1 − percentile)-th percentile, capped at 0 and
        /// negated, since it must be a loss and a positive quantity.
        /// </summary>
        /// <param name="percentile">The percentile, in [0.9, 1).</param>
        /// <returns>The value-at-risk.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentile"/> is outside [0.9, 1).</exception>
        public double GaussianValueAtRisk(double percentile)
        {
            ArgumentNullException.ThrowIfNull(stats);
            if (!(percentile < 1.0 && percentile >= 0.9))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(percentile), $"percentile ({General(percentile)}) out of range [0.9, 1)");
            }

            double result = stats.GaussianPercentile(1.0 - percentile);
            // VAR must be a loss
            // this means that it has to be MIN(dist(1.0-percentile), 0.0)
            // VAR must also be a positive quantity, so -MIN(*)
            return -(0.0 < result ? 0.0 : result);
        }

        /// <summary>
        /// The Gaussian-assumption expected shortfall at a percentile: the expected loss where the loss exceeds the
        /// value-at-risk, E[x | x &lt; VaR(p)], the average of observations below the percentile, also known as
        /// conditional value-at-risk (Artzner, Delbaen, Eber and Heath, "Coherent measures of risk", Mathematical
        /// Finance 9, 1999); capped at 0 and negated.
        /// </summary>
        /// <param name="percentile">The percentile, in [0.9, 1).</param>
        /// <returns>The expected shortfall.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentile"/> is outside [0.9, 1).</exception>
        public double GaussianExpectedShortfall(double percentile)
        {
            ArgumentNullException.ThrowIfNull(stats);
            if (!(percentile < 1.0 && percentile >= 0.9))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(percentile), $"percentile ({General(percentile)}) out of range [0.9, 1)");
            }

            double m = stats.Mean();
            double std = stats.StandardDeviation();
            var gInverse = new InverseCumulativeNormal(m, std);
            double var = gInverse.Evaluate(1.0 - percentile);
            var g = new NormalDistribution(m, std);
            double result = m - (std * std * g.Evaluate(var) / (1.0 - percentile));
            // expectedShortfall must be a loss
            // this means that it has to be MIN(result, 0.0)
            // expectedShortfall must also be a positive quantity, so -MIN(*)
            return -(0.0 < result ? 0.0 : result);
        }

        /// <summary>The Gaussian-assumption shortfall: the probability of an observation below a target.</summary>
        /// <param name="target">The target.</param>
        /// <returns>The shortfall.</returns>
        public double GaussianShortfall(double target)
        {
            ArgumentNullException.ThrowIfNull(stats);
            double standardDeviation = stats.StandardDeviation();
            var gIntegral = new CumulativeNormalDistribution(stats.Mean(), standardDeviation);
            return gIntegral.Evaluate(target);
        }

        /// <summary>The Gaussian-assumption average shortfall, the averaged shortfallness below a target.</summary>
        /// <param name="target">The target.</param>
        /// <returns>The average shortfall.</returns>
        public double GaussianAverageShortfall(double target)
        {
            ArgumentNullException.ThrowIfNull(stats);
            double m = stats.Mean();
            double std = stats.StandardDeviation();
            var gIntegral = new CumulativeNormalDistribution(m, std);
            var g = new NormalDistribution(m, std);
            return target - m + (std * std * g.Evaluate(target) / gIntegral.Evaluate(target));
        }
    }
}
