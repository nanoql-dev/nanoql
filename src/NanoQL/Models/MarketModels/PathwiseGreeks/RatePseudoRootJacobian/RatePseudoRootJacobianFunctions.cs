// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.hpp ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The derivative of each end rate of a log-normal Euler step with respect to each pseudo-root element, which QuantLib's
/// two analytic Jacobians repeat.
/// </summary>
internal static class RatePseudoRootJacobianFunctions
{
    /// <summary>
    /// Writes, for each rate j, the matrix of its derivatives with respect to the pseudo-root's elements (k, f): zero
    /// for a rate already reset, for a root of a dead rate and for a root of a later rate (MMPR4 (iv)'s form).
    /// </summary>
    public static void Derivatives(
        Matrix pseudoRoot,
        int aliveIndex,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> displacements,
        ReadOnlySpan<double> oldRates,
        ReadOnlySpan<double> discountRatios,
        ReadOnlySpan<double> newRates,
        ReadOnlySpan<double> gaussians,
        Matrix e,
        Span<double> ratios,
        IReadOnlyList<Matrix> derivatives)
    {
        int numberRates = taus.Length;
        int factors = pseudoRoot.Columns;

        for (int j = aliveIndex; j < numberRates; ++j)
        {
            ratios[j] = (oldRates[j] + displacements[j]) * discountRatios[j + 1];
        }

        // the drift's sum, each term with its own accrual period (MMPR4 (iv))
        for (int f = 0; f < factors; ++f)
        {
            e[aliveIndex, f] = 0;

            for (int j = aliveIndex + 1; j < numberRates; ++j)
            {
                e[j, f] = e[j - 1, f] + (taus[j - 1] * ratios[j - 1] * pseudoRoot[j - 1, f]);
            }
        }

        // nullify the derivatives of rates that have already reset
        for (int j = 0; j < aliveIndex; ++j)
        {
            derivatives[j].AsSpan().Clear();
        }

        for (int f = 0; f < factors; ++f)
        {
            for (int j = aliveIndex; j < numberRates; ++j)
            {
                Matrix d = derivatives[j];

                // the displaced end rate (MMPR4 (iv))
                double displacedRate = newRates[j] + displacements[j];
                for (int k = aliveIndex; k < j; ++k)
                {
                    d[k, f] = displacedRate * ratios[k] * taus[k] * pseudoRoot[j, f];
                }

                // GG don't seem to have the 2, this term is miniscule in any case
                double tmp = 2 * ratios[j] * taus[j] * pseudoRoot[j, f];
                tmp -= pseudoRoot[j, f];
                tmp += e[j, f];
                tmp += gaussians[f];
                tmp *= newRates[j] + displacements[j];

                d[j, f] = tmp;

                for (int k = 0; k < aliveIndex; ++k)
                {
                    d[k, f] = 0.0;
                }

                for (int k = j + 1; k < numberRates; ++k)
                {
                    d[k, f] = 0.0;
                }
            }
        }
    }
}
