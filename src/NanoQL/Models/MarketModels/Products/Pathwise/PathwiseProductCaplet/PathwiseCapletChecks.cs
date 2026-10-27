// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.hpp ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>The pathwise caplets' shared construction checks and evolution, QuantLib's constructors' repeated lines.</summary>
internal static class PathwiseCapletChecks
{
    /// <summary>The caplets' evolution, every rate time but the last, after QuantLib's checks.</summary>
    /// <param name="rateTimes">The rate times, increasing, one more than the accruals.</param>
    /// <param name="accruals">The accruals, one a rate.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate.</param>
    /// <returns>The evolution.</returns>
    /// <exception cref="ArgumentException">The times aren't increasing, or the inputs aren't one a rate.</exception>
    public static EvolutionDescription Evolution(
        ReadOnlySpan<double> rateTimes, ReadOnlySpan<double> accruals, ReadOnlySpan<double> paymentTimes)
    {
        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        UtilitiesFunctions.CheckIncreasingTimes(paymentTimes);
        int numberRates = accruals.Length;
        if (rateTimes.Length - 1 != numberRates)
        {
            throw new ArgumentException("rateTimes.size()<> numberOfRates+1", nameof(rateTimes));
        }

        if (paymentTimes.Length != numberRates)
        {
            throw new ArgumentException("paymentTimes.size()<> numberOfRates", nameof(paymentTimes));
        }

        return new EvolutionDescription(rateTimes, rateTimes[..^1]);
    }
}
