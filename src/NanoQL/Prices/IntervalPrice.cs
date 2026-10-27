// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/prices.hpp ql/prices.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL;

/// <summary>The open, close, high and low prices of an interval.</summary>
/// <remarks>
/// Immutable (the S-bar plan's BR3 (iv)): QuantLib's <c>setValue</c> and <c>setValues</c> change the object, where
/// <see cref="SetValue"/> and <see cref="SetValues"/> return a copy. Two prices are equal when their four values are,
/// by <see cref="double.Equals(double)"/>; QuantLib has no equality on the type. QuantLib's parameterless constructor
/// isn't ported: its nulls serve QuantLib's default insertion into a mutable time series, and no QuantLib test builds
/// one.
/// </remarks>
public sealed class IntervalPrice : IEquatable<IntervalPrice>
{
    /// <summary>Initializes a new instance of the <see cref="IntervalPrice"/> class.</summary>
    /// <param name="open">The opening price.</param>
    /// <param name="close">The closing price.</param>
    /// <param name="high">The highest price.</param>
    /// <param name="low">The lowest price.</param>
    public IntervalPrice(double open, double close, double high, double low)
    {
        Open = open;
        Close = close;
        High = high;
        Low = low;
    }

    /// <summary>Gets the opening price.</summary>
    public double Open { get; }

    /// <summary>Gets the closing price.</summary>
    public double Close { get; }

    /// <summary>Gets the highest price.</summary>
    public double High { get; }

    /// <summary>Gets the lowest price.</summary>
    public double Low { get; }

    /// <summary>One of the four prices.</summary>
    /// <param name="type">Which price.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't an <see cref="IntervalPriceType"/>
    /// member.</exception>
    public double Value(IntervalPriceType type)
    {
        EnumArguments.Check(type, nameof(type));
        return type switch
        {
            IntervalPriceType.Open => Open,
            IntervalPriceType.Close => Close,
            IntervalPriceType.High => High,
            _ => Low,
        };
    }

    /// <summary>The prices with one of them replaced.</summary>
    /// <param name="value">The new price.</param>
    /// <param name="type">Which price.</param>
    /// <returns>A new interval price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't an <see cref="IntervalPriceType"/>
    /// member.</exception>
    public IntervalPrice SetValue(double value, IntervalPriceType type)
    {
        EnumArguments.Check(type, nameof(type));
        return type switch
        {
            IntervalPriceType.Open => new IntervalPrice(value, Close, High, Low),
            IntervalPriceType.Close => new IntervalPrice(Open, value, High, Low),
            IntervalPriceType.High => new IntervalPrice(Open, Close, value, Low),
            _ => new IntervalPrice(Open, Close, High, value),
        };
    }

    /// <summary>The prices with all four replaced.</summary>
    /// <param name="open">The opening price.</param>
    /// <param name="close">The closing price.</param>
    /// <param name="high">The highest price.</param>
    /// <param name="low">The lowest price.</param>
    /// <returns>A new interval price.</returns>
    public IntervalPrice SetValues(double open, double close, double high, double low) => new(open, close, high, low);

    /// <summary>A series of interval prices from their dates and four price sequences.</summary>
    /// <param name="d">The dates.</param>
    /// <param name="open">The opening prices.</param>
    /// <param name="close">The closing prices.</param>
    /// <param name="high">The highest prices.</param>
    /// <param name="low">The lowest prices.</param>
    /// <returns>The series.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The sequences' sizes differ.</exception>
    public static TimeSeries<IntervalPrice> MakeSeries(
        IReadOnlyList<Date> d, IReadOnlyList<double> open, IReadOnlyList<double> close, IReadOnlyList<double> high, IReadOnlyList<double> low)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(close);
        ArgumentNullException.ThrowIfNull(high);
        ArgumentNullException.ThrowIfNull(low);
        int dsize = d.Count;
        if (!(open.Count == dsize && close.Count == dsize && high.Count == dsize && low.Count == dsize))
        {
            throw new NanoQLException($"size mismatch ({dsize}, {open.Count}, {close.Count}, {high.Count}, {low.Count})");
        }

        var prices = new IntervalPrice[dsize];
        for (int i = 0; i < dsize; ++i)
        {
            prices[i] = new IntervalPrice(open[i], close[i], high[i], low[i]);
        }

        return new TimeSeries<IntervalPrice>(d, prices);
    }

    /// <summary>One of the four prices at each date of a series, in date order.</summary>
    /// <param name="ts">The series.</param>
    /// <param name="type">Which price.</param>
    /// <returns>The prices.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't an <see cref="IntervalPriceType"/>
    /// member.</exception>
    public static ImmutableArray<double> ExtractValues(TimeSeries<IntervalPrice> ts, IntervalPriceType type)
    {
        ArgumentNullException.ThrowIfNull(ts);
        EnumArguments.Check(type, nameof(type));
        ImmutableArray<double>.Builder returnval = ImmutableArray.CreateBuilder<double>(ts.Size);
        foreach (KeyValuePair<Date, IntervalPrice> i in ts)
        {
            returnval.Add(i.Value.Value(type));
        }

        return returnval.MoveToImmutable();
    }

    /// <summary>One of the four prices at each date of a series, as a series.</summary>
    /// <param name="ts">The series.</param>
    /// <param name="type">Which price.</param>
    /// <returns>The series of prices.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't an <see cref="IntervalPriceType"/>
    /// member.</exception>
    public static TimeSeries<double> ExtractComponent(TimeSeries<IntervalPrice> ts, IntervalPriceType type)
    {
        ArgumentNullException.ThrowIfNull(ts);
        return new TimeSeries<double>(ts.Dates(), ExtractValues(ts, type));
    }

    /// <inheritdoc/>
    public bool Equals(IntervalPrice? other) =>
        other is not null && Open.Equals(other.Open) && Close.Equals(other.Close) && High.Equals(other.High) && Low.Equals(other.Low);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IntervalPrice other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Open, Close, High, Low);
}
