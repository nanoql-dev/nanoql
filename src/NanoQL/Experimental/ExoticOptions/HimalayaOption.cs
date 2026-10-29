// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/himalayaoption.hpp ql/experimental/exoticoptions/himalayaoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A Himalaya option on a basket: at each fixing date the best performer since inception among the assets left joins
/// the average and leaves the basket; at the last fixing the option pays the average's excess over the strike.
/// </summary>
/// <remarks>
/// Immutable; its payoff is a plain-vanilla call at the strike and its exercise European at the last fixing date, as
/// QuantLib builds them. Its engines return a <see cref="MultiAssetOptionResults"/>, QuantLib's results class adding
/// nothing to the multi-asset option's. QuantLib doesn't manage seasoned options: a fixing before the evaluation date
/// is refused by the engine. The paths start at the evaluation date, the performances' inception, so a fixing on it is
/// that inception rather than a selection (every asset's performance is 1 there), as QuantLib's grid reads it and its
/// own case lists one.
/// </remarks>
[Experimental("NQL0001")]
public sealed class HimalayaOption : MultiAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="HimalayaOption"/> class.</summary>
    /// <param name="fixingDates">The fixing dates, the last the exercise date.</param>
    /// <param name="strike">The strike.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fixingDates"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">No fixing date is given ("no fixing dates given"), where QuantLib's constructor
    /// reads the last of none.</exception>
    public HimalayaOption(IEnumerable<Date> fixingDates, double strike)
        : this(DatesOf(fixingDates), strike)
    {
    }

    private HimalayaOption(ImmutableArray<Date> fixingDates, double strike)
        : base(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(fixingDates[^1]))
    {
        FixingDates = fixingDates;
    }

    /// <summary>Gets the fixing dates.</summary>
    public ImmutableArray<Date> FixingDates { get; }

    // QuantLib's arguments' "no fixing dates given", checked before the constructor reads the last date
    private static ImmutableArray<Date> DatesOf(IEnumerable<Date> fixingDates)
    {
        ArgumentNullException.ThrowIfNull(fixingDates);
        ImmutableArray<Date> dates = [.. fixingDates];
        if (dates.IsEmpty)
        {
            throw new NanoQLException("no fixing dates given");
        }

        return dates;
    }
}
