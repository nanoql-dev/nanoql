// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisediscounter.hpp ql/models/marketmodels/pathwisediscounter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The number of units of the discretely compounding money-market account a unit paid at a time buys on a step's
/// rates, and its derivative with respect to each forward rate, for the pathwise Greeks: the discount factors of the
/// rate times bracketing the payment, log-linearly interpolated between them.
/// </summary>
/// <remarks>
/// <para>
/// Discounting reads the simulated rates only: to discount back to time 0, multiply by the discount factor of the
/// first rate time.
/// </para>
/// <para>Immutable (the S-mmp plan's MMPR1 (ii)): a pure function of a step's discount factors.</para>
/// <para>
/// <b>The bracket</b> (MMPR4 (ix), a documented divergence): the period's start is the last rate time at or before
/// the payment, clamped to the periods, as <see cref="MarketModelDiscounter"/>'s. QuantLib's <c>lower_bound</c> takes
/// the first rate time at or after the payment (<c>pathwisediscounter.cpp:32</c>), so a payment strictly inside a
/// period but the last is extrapolated from the next period (<c>Divergences/MarketModelDiscounterBracketTests.cs</c>).
/// A payment on a rate time, before the first or after the last gives QuantLib's bits.
/// </para>
/// </remarks>
public sealed class MarketModelPathwiseDiscounter
{
    private readonly int before;
    private readonly int numberRates;
    private readonly double postWeight;
    private readonly double[] taus;

    /// <summary>Initializes a new instance of the <see cref="MarketModelPathwiseDiscounter"/> class.</summary>
    /// <param name="paymentTime">The payment time.</param>
    /// <param name="rateTimes">The rate times, at least two, increasing.</param>
    /// <exception cref="ArgumentException">There are fewer than two rate times, or they aren't increasing.</exception>
    public MarketModelPathwiseDiscounter(double paymentTime, ReadOnlySpan<double> rateTimes)
    {
        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        if (rateTimes.Length < 2)
        {
            throw new ArgumentException("at least two rate times are required", nameof(rateTimes));
        }

        numberRates = rateTimes.Length - 1;

        // the last rate time at or before the payment: upper_bound's position less one
        int upper = 0;
        while (upper < rateTimes.Length && !(paymentTime < rateTimes[upper]))
        {
            ++upper;
        }

        before = Math.Max(upper - 1, 0);

        // handle the case where the payment is in the last period or after the last period
        if (before > rateTimes.Length - 2)
        {
            before = rateTimes.Length - 2;
        }

        double beforeWeight = 1.0 - ((paymentTime - rateTimes[before]) / (rateTimes[before + 1] - rateTimes[before]));
        postWeight = 1.0 - beforeWeight;
        taus = new double[numberRates];
        for (int i = 0; i < numberRates; ++i)
        {
            taus[i] = rateTimes[i + 1] - rateTimes[i];
        }
    }

    /// <summary>
    /// Writes the money-market units a unit paid at the payment time buys at a step, then its derivative with respect
    /// to each forward rate.
    /// </summary>
    /// <param name="liborRates">The rates at each step; not read, as in QuantLib.</param>
    /// <param name="discounts">The discount factors P(t_0, t_j), j = 0 to the number of rates, at each step.</param>
    /// <param name="currentStep">The step's row in <paramref name="discounts"/>.</param>
    /// <param name="factors">The units, then the derivatives: 1 + the number of rates entries, written.</param>
    /// <exception cref="ArgumentException"><paramref name="factors"/> holds fewer than 1 + the number of rates
    /// entries.</exception>
    public void GetFactors(Matrix liborRates, Matrix discounts, int currentStep, Span<double> factors)
    {
        ArgumentNullException.ThrowIfNull(discounts);
        if (factors.Length < numberRates + 1)
        {
            throw new ArgumentException(
                $"the factors hold {factors.Length} entries, not 1 + the {numberRates} rates", nameof(factors));
        }

        ReadOnlySpan<double> d = discounts.Row(currentStep);
        double preDF = d[before];
        double postDF = d[before + 1];

        for (int i = before + 1; i < numberRates; ++i)
        {
            factors[i + 1] = 0.0;
        }

        if (postWeight == 0.0)
        {
            factors[0] = preDF;

            for (int i = 0; i < before; ++i)
            {
                factors[i + 1] = -preDF * taus[i] * d[i + 1] / d[i];
            }

            factors[before + 1] = 0.0;

            return;
        }

        double df = preDF * Math.Pow(postDF / preDF, postWeight);

        factors[0] = df;

        for (int i = 0; i <= before; ++i)
        {
            factors[i + 1] = -df * taus[i] * d[i + 1] / d[i];
        }

        factors[before + 1] *= postWeight;
    }
}
