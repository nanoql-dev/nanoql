// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/garmanklass.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The Garman-Klass volatility models, based on high-low formulas using the method of Garman and Klass in their paper
/// "On the Estimation of the Security Price from Historical Data": at each date, √(|f(p)|/y) for a point estimator f
/// of the interval's prices p and a year fraction y. Volatilities are assumed to be expressed on an annual basis.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: each estimator implements its point function. QuantLib's
/// <c>GarmanKlassOpenClose&lt;T&gt;</c>, a template deriving its parameter, isn't a type here: the estimators that
/// read the previous close and today's open (<see cref="GarmanKlassSigma1"/>, <see cref="GarmanKlassSigma3"/> and
/// <see cref="GarmanKlassSigma6"/>) run its calculation through <see cref="OpenClose"/> on their base estimator's point
/// function, QuantLib's arithmetic.
/// </para>
/// <para>
/// A documented divergence: the open-close calculation refuses an empty series, where QuantLib increments its begin
/// past its end.
/// </para>
/// </remarks>
public abstract class GarmanKlassAbstract : LocalVolatilityEstimator<IntervalPrice>
{
    /// <summary>Initializes a new instance of the <see cref="GarmanKlassAbstract"/> class.</summary>
    /// <param name="y">The year fraction of an interval.</param>
    protected GarmanKlassAbstract(double y)
    {
        YearFraction = y;
    }

    /// <summary>Gets the year fraction of an interval.</summary>
    protected double YearFraction { get; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="quoteSeries"/> is <see langword="null"/>.</exception>
    public override TimeSeries<double> Calculate(TimeSeries<IntervalPrice> quoteSeries)
    {
        ArgumentNullException.ThrowIfNull(quoteSeries);
        ImmutableArray<IntervalPrice> prices = quoteSeries.Values();
        var retval = new double[prices.Length];
        for (int cur = 0; cur < prices.Length; ++cur)
        {
            retval[cur] = Math.Sqrt(Math.Abs(CalculatePoint(prices[cur])) / YearFraction);
        }

        return new TimeSeries<double>(quoteSeries.Dates(), retval);
    }

    /// <summary>The estimator's point variance of an interval's prices.</summary>
    /// <param name="p">The interval's prices.</param>
    /// <returns>The point variance, over an interval.</returns>
    protected abstract double CalculatePoint(IntervalPrice p);

    /// <summary>
    /// QuantLib's <c>GarmanKlassOpenClose&lt;T&gt;::calculate</c>: at each date after the first,
    /// √((a (o₁ − c₀)²/f + (1 − a) g(p)/(1 − f))/y) for the previous close c₀ and today's open o₁ in logs, the base
    /// estimator's point function g, the market's open fraction f and the weight a.
    /// </summary>
    /// <param name="quoteSeries">The quotes.</param>
    /// <param name="yearFraction">The year fraction of an interval.</param>
    /// <param name="marketOpenFraction">The fraction of the interval the market is open, f.</param>
    /// <param name="a">The weight of the overnight jump, a.</param>
    /// <param name="point">The base estimator's point function.</param>
    /// <returns>A volatility for each date after the first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="quoteSeries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The series is empty.</exception>
    private protected static TimeSeries<double> OpenClose(
        TimeSeries<IntervalPrice> quoteSeries, double yearFraction, double marketOpenFraction, double a, Func<IntervalPrice, double> point)
    {
        ArgumentNullException.ThrowIfNull(quoteSeries);
        if (quoteSeries.Empty)
        {
            throw new ArgumentException("the quote series is empty", nameof(quoteSeries));
        }

        ImmutableArray<Date> dates = quoteSeries.Dates();
        ImmutableArray<IntervalPrice> prices = quoteSeries.Values();
        var retval = new double[prices.Length - 1];
        for (int cur = 1; cur < prices.Length; ++cur)
        {
            int prev = cur - 1;
            double c0 = Math.Log(prices[prev].Close);
            double o1 = Math.Log(prices[cur].Open);
            double sigma2 = (a * (o1 - c0) * (o1 - c0) / marketOpenFraction) + ((1 - a) * point(prices[cur]) / (1 - marketOpenFraction));
            retval[prev] = Math.Sqrt(sigma2 / yearFraction);
        }

        return new TimeSeries<double>(dates[1..], retval);
    }
}
