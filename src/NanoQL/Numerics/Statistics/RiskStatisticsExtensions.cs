// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/riskstatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Utilities.DataFormattersFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// The empirical risk measures (semi-variance, regret, value-at-risk, expected shortfall, …), from the samples of a
/// statistics tool that keeps them: QuantLib's <c>GenericRiskStatistics&lt;S&gt;</c>, which derives from its type
/// parameter, as C# 14 extension members over any <see cref="IEmpiricalStatistics"/>.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's <c>RiskStatistics</c>, <c>GenericRiskStatistics&lt;GaussianStatistics&gt;</c>, and its
/// <c>Statistics</c> typedef are <see cref="GeneralStatistics"/> with these members and those of
/// <see cref="GaussianStatisticsExtensions"/>.
/// </para>
/// <para>
/// <see cref="ExpectedShortfall"/> where no sample lies below the value-at-risk's level reads the level itself, 0 with
/// no loss and the value-at-risk where the tail is an atom at that level, where QuantLib refuses "no data below the
/// target": a documented divergence.
/// </para>
/// </remarks>
public static class RiskStatisticsExtensions
{
    extension<TStat>(TStat stats)
        where TStat : IEmpiricalStatistics
    {
        /// <summary>
        /// The variance of observations below the mean, N/(N − 1) E[(x − ⟨x⟩)² | x &lt; ⟨x⟩] (Markowitz, 1959).
        /// </summary>
        /// <returns>The semi-variance.</returns>
        /// <exception cref="NanoQLException">There are fewer than two samples below the mean.</exception>
        public double SemiVariance() => stats.Regret(stats.Mean());

        /// <summary>The semi-deviation, the square root of the semi-variance.</summary>
        /// <returns>The semi-deviation.</returns>
        /// <exception cref="NanoQLException">There are fewer than two samples below the mean.</exception>
        public double SemiDeviation() => Math.Sqrt(stats.SemiVariance());

        /// <summary>The variance of observations below 0, N/(N − 1) E[x² | x &lt; 0].</summary>
        /// <returns>The downside variance.</returns>
        /// <exception cref="NanoQLException">There are fewer than two negative samples.</exception>
        public double DownsideVariance() => stats.Regret(0.0);

        /// <summary>The downside deviation, the square root of the downside variance.</summary>
        /// <returns>The downside deviation.</returns>
        /// <exception cref="NanoQLException">There are fewer than two negative samples.</exception>
        public double DownsideDeviation() => Math.Sqrt(stats.DownsideVariance());

        /// <summary>
        /// The variance of observations below a target, N/(N − 1) E[(x − t)² | x &lt; t] (Dembo and Freeman, "The Rules
        /// Of Risk", Wiley, 2001).
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>The regret.</returns>
        /// <exception cref="NanoQLException">There are fewer than two samples below the target.</exception>
        public double Regret(double target)
        {
            ArgumentNullException.ThrowIfNull(stats);
            // average over the range below the target
            (double? x, int n) = stats.ExpectationValue(
                xi =>
                {
                    double d = xi - target;
                    return d * d;
                },
                xi => xi < target);
            if (!(n > 1))
            {
                throw new NanoQLException("samples under target <= 1, unsufficient");
            }

            return n / (n - 1.0) * x!.Value;
        }

        /// <summary>The potential upside (the reciprocal of value-at-risk) at a percentile, floored at 0.</summary>
        /// <param name="percentile">The percentile, in [0.9, 1).</param>
        /// <returns>The potential upside.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentile"/> is outside [0.9, 1).</exception>
        public double PotentialUpside(double percentile)
        {
            ArgumentNullException.ThrowIfNull(stats);
            if (!(percentile >= 0.9 && percentile < 1.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(percentile), $"percentile ({General(percentile)}) out of range [0.9, 1.0)");
            }

            // potential upside must be a gain, i.e., floored at 0.0
            double result = stats.Percentile(percentile);
            return result < 0.0 ? 0.0 : result;
        }

        /// <summary>The value-at-risk at a percentile: the (1 − percentile)-th percentile, capped at 0 and negated.</summary>
        /// <param name="percentile">The percentile, in [0.9, 1).</param>
        /// <returns>The value-at-risk.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentile"/> is outside [0.9, 1).</exception>
        public double ValueAtRisk(double percentile)
        {
            ArgumentNullException.ThrowIfNull(stats);
            if (!(percentile >= 0.9 && percentile < 1.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(percentile), $"percentile ({General(percentile)}) out of range [0.9, 1.0)");
            }

            // must be a loss, i.e., capped at 0.0 and negated
            double result = stats.Percentile(1.0 - percentile);
            return -(0.0 < result ? 0.0 : result);
        }

        /// <summary>
        /// The expected shortfall at a percentile: the expected loss where the loss exceeds the value-at-risk,
        /// E[x | x &lt; VaR(p)], the average of observations below the percentile, also known as conditional
        /// value-at-risk (Artzner, Delbaen, Eber and Heath, "Coherent measures of risk", Mathematical Finance 9, 1999);
        /// capped at 0 and negated.
        /// </summary>
        /// <param name="percentile">The percentile, in [0.9, 1).</param>
        /// <returns>
        /// The expected shortfall; where no sample lies below the value-at-risk's level, that level capped and negated:
        /// 0 with no loss, the value-at-risk where the tail is an atom at that level (a divergence: QuantLib refuses).
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentile"/> is outside [0.9, 1).</exception>
        /// <exception cref="NanoQLException">There's no sample.</exception>
        public double ExpectedShortfall(double percentile)
        {
            ArgumentNullException.ThrowIfNull(stats);
            if (!(percentile >= 0.9 && percentile < 1.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(percentile), $"percentile ({General(percentile)}) out of range [0.9, 1.0)");
            }

            if (!(stats.Samples != 0))
            {
                throw new NanoQLException("empty sample set");
            }

            double target = -stats.ValueAtRisk(percentile);
            (double? x, int n) = stats.ExpectationValue(static xi => xi, xi => xi < target);

            // QuantLib refuses "no data below the target" here. Nothing lies below the level where there's no loss
            // (the level is 0) and where the tail is an atom at the level: the tail's mean is the level, QuantLib's
            // own limit as the atom splits (its comment: "must be a loss, i.e., capped at 0.0 and negated").
            double mean = n == 0 ? target : x!.Value;

            // must be a loss, i.e., capped at 0.0 and negated
            return -(0.0 < mean ? 0.0 : mean);
        }

        /// <summary>
        /// The probability of missing a target, E[Θ | (−∞, ∞)], where Θ(x) is 1 for x &lt; t and 0 for x ≥ t.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>The shortfall.</returns>
        /// <exception cref="NanoQLException">There's no sample.</exception>
        public double Shortfall(double target)
        {
            ArgumentNullException.ThrowIfNull(stats);
            if (!(stats.Samples != 0))
            {
                throw new NanoQLException("empty sample set");
            }

            return stats.ExpectationValue(x => x < target ? 1.0 : 0.0).Value!.Value;
        }

        /// <summary>The averaged shortfallness, E[t − x | x &lt; t].</summary>
        /// <param name="target">The target.</param>
        /// <returns>The average shortfall.</returns>
        /// <exception cref="NanoQLException">No sample lies below the target.</exception>
        public double AverageShortfall(double target)
        {
            ArgumentNullException.ThrowIfNull(stats);
            (double? x, int n) = stats.ExpectationValue(xi => target - xi, xi => xi < target);
            if (!(n != 0))
            {
                throw new NanoQLException("no data below the target");
            }

            return x!.Value;
        }
    }
}
