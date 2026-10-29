// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/pagodaoption.hpp ql/experimental/exoticoptions/pagodaoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A Pagoda option on a portfolio: at expiry it pays a fraction of the portfolio's performance over the fixing periods,
/// capped at a roof and floored at 0.
/// </summary>
/// <remarks>
/// Immutable; its payoff is a <see cref="NullPayoff"/> and its exercise European at the last fixing date, as QuantLib
/// builds them. Each period's performance is the sum of the assets' returns over the period, each weighted by the
/// asset's value at inception, averaged over the assets, as QuantLib's path pricer reads it. QuantLib doesn't manage
/// seasoned options: a fixing before the evaluation date is refused by the engine.
/// </remarks>
[Experimental("NQL0001")]
public sealed class PagodaOption : MultiAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="PagodaOption"/> class.</summary>
    /// <param name="fixingDates">The fixing dates, the last the exercise date.</param>
    /// <param name="roof">The cap on the performance.</param>
    /// <param name="fraction">The fraction of the capped performance paid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fixingDates"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">No fixing date is given ("no fixingDates given"), where QuantLib's constructor
    /// reads the last of none.</exception>
    public PagodaOption(IEnumerable<Date> fixingDates, double roof, double fraction)
        : this(DatesOf(fixingDates), roof, fraction)
    {
    }

    private PagodaOption(ImmutableArray<Date> fixingDates, double roof, double fraction)
        : base(new NullPayoff(), new EuropeanExercise(fixingDates[^1]))
    {
        FixingDates = fixingDates;
        Roof = roof;
        Fraction = fraction;
    }

    /// <summary>Gets the fixing dates.</summary>
    public ImmutableArray<Date> FixingDates { get; }

    /// <summary>Gets the cap on the performance.</summary>
    public double Roof { get; }

    /// <summary>Gets the fraction of the capped performance paid.</summary>
    public double Fraction { get; }

    // QuantLib's arguments' "no fixingDates given", checked before the constructor reads the last date
    private static ImmutableArray<Date> DatesOf(IEnumerable<Date> fixingDates)
    {
        ArgumentNullException.ThrowIfNull(fixingDates);
        ImmutableArray<Date> dates = [.. fixingDates];
        if (dates.IsEmpty)
        {
            throw new NanoQLException("no fixingDates given");
        }

        return dates;
    }
}
