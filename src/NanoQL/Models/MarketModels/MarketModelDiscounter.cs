// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/discounter.hpp ql/models/marketmodels/discounter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// The number of numeraire bonds a unit paid at a time is worth on a curve state: the discount ratio of the rate
/// times bracketing the payment, log-linearly interpolated between them.
/// </summary>
/// <remarks>
/// <para>Immutable: a pure function of a curve state.</para>
/// <para>
/// <b>The bracket</b> (a documented divergence): the period's start is the last rate time at or before the payment,
/// clamped to the periods, so a payment inside a period interpolates its own period's bonds. QuantLib's
/// <c>lower_bound</c> takes the first rate time at or after the payment (<c>discounter.cpp:31</c>), so a payment
/// strictly inside a period but the last is extrapolated from the next period, against its own weight's [0, 1] form
/// (<c>Divergences/MarketModelDiscounterBracketTests.cs</c>). A payment on a rate time, before the first or after the
/// last gives QuantLib's bits.
/// </para>
/// </remarks>
public sealed class MarketModelDiscounter
{
    private readonly int before;
    private readonly double beforeWeight;

    /// <summary>Initializes a new instance of the <see cref="MarketModelDiscounter"/> class.</summary>
    /// <param name="paymentTime">The payment time.</param>
    /// <param name="rateTimes">The rate times, at least two, increasing.</param>
    /// <exception cref="ArgumentException">There are fewer than two rate times, or they aren't increasing.</exception>
    public MarketModelDiscounter(double paymentTime, ReadOnlySpan<double> rateTimes)
    {
        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        if (rateTimes.Length < 2)
        {
            throw new ArgumentException("at least two rate times are required", nameof(rateTimes));
        }

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

        beforeWeight = 1.0 - ((paymentTime - rateTimes[before]) / (rateTimes[before + 1] - rateTimes[before]));
    }

    /// <summary>The number of numeraire bonds a unit paid at the payment time is worth.</summary>
    /// <param name="curveState">The curve state.</param>
    /// <param name="numeraire">The numeraire's index.</param>
    /// <returns>The number of numeraire bonds.</returns>
    public double NumeraireBonds(CurveState curveState, int numeraire)
    {
        ArgumentNullException.ThrowIfNull(curveState);
        double preDF = curveState.DiscountRatio(before, numeraire);
        if (beforeWeight == 1.0)
        {
            return preDF;
        }

        double postDF = curveState.DiscountRatio(before + 1, numeraire);
        if (beforeWeight == 0.0)
        {
            return postDF;
        }

        return Math.Pow(preDF, beforeWeight) * Math.Pow(postDF, 1.0 - beforeWeight);
    }
}
