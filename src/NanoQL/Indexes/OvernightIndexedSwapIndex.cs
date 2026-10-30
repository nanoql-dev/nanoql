// SPDX-FileCopyrightText: Copyright (C) 2006, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/swapindex.hpp ql/indexes/swapindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The base class of overnight-indexed swap indexes: a swap index on an overnight index, with an annual fixed leg,
/// modified following, on the overnight index's calendar and day counter.
/// </summary>
/// <remarks>
/// <para>
/// Its <see cref="UnderlyingSwap"/> hides the base's and returns the overnight-indexed swap, as QuantLib's does.
/// QuantLib overrides neither the forecast nor the maturity date, and both call the base's underlying swap, which is
/// not virtual: so the inherited <see cref="SwapIndex.ForecastFixing"/> and <see cref="SwapIndex.MaturityDate"/> use
/// a vanilla swap whose floating leg has a daily coupon on the overnight index, read as an Ibor index, and the
/// forecast reads the at-par switch. nanoql keeps that, so the fixings are QuantLib's.
/// </para>
/// <para>
/// The telescopic-value-dates flag is kept, and passed to the overnight-indexed swap, which builds every daily value
/// date. A clone is a plain <see cref="SwapIndex"/>, as in QuantLib, where the clones are inherited.
/// </para>
/// </remarks>
public sealed class OvernightIndexedSwapIndex : SwapIndex
{
    private readonly bool telescopicValueDates;
    private readonly RateAveragingType averagingMethod;

    /// <summary>Initializes a new instance of the <see cref="OvernightIndexedSwapIndex"/> class.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="settlementDays">The fixing days: the business days from a fixing to the swap's start.</param>
    /// <param name="currency">The currency, or <see langword="null"/>, QuantLib's empty currency.</param>
    /// <param name="overnightIndex">The overnight index.</param>
    /// <param name="telescopicValueDates">
    /// Whether the overnight-indexed swap's coupons take telescopic value dates.
    /// </param>
    /// <param name="averagingMethod">How the overnight-indexed swap's coupons average the overnight fixings.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="familyName"/> or <paramref name="overnightIndex"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or <paramref name="averagingMethod"/> isn't a member of its type.
    /// </exception>
    public OvernightIndexedSwapIndex(
        string familyName,
        Period tenor,
        int settlementDays,
        Currency? currency,
        OvernightIndex overnightIndex,
        bool telescopicValueDates = false,
        RateAveragingType averagingMethod = RateAveragingType.Compound)
        : base(
            familyName,
            tenor,
            settlementDays,
            currency,
            NotNull(overnightIndex).FixingCalendar,
            new Period(1, TimeUnit.Years),
            BusinessDayConvention.ModifiedFollowing,
            overnightIndex.DayCounter,
            overnightIndex)
    {
        EnumArguments.Check(averagingMethod, nameof(averagingMethod));
        OvernightIndex = overnightIndex;
        this.telescopicValueDates = telescopicValueDates;
        this.averagingMethod = averagingMethod;
    }

    /// <summary>Gets the overnight index.</summary>
    public OvernightIndex OvernightIndex { get; }

    /// <summary>
    /// The overnight-indexed swap fixing on a date: from the fixing date's value date, at a zero fixed rate, with the
    /// index's day counter on the fixed leg. It hides the base's vanilla swap, which the forecast and the maturity date
    /// use.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The swap, which holds no engine.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="fixingDate"/> is <c>default(Date)</c> or isn't a valid fixing date.
    /// </exception>
    /// <exception cref="NanoQLException">The swap can't be built.</exception>
    public new OvernightIndexedSwap UnderlyingSwap(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        return new MakeOIS(SwapTenor, OvernightIndex, Provider)
            .WithFixedRate(0.0)
            .WithEffectiveDate(ValueDate(fixingDate))
            .WithFixedLegDayCount(DayCounter)
            .WithTelescopicValueDates(telescopicValueDates)
            .WithAveragingMethod(averagingMethod)
            .ToOvernightIndexedSwap(context);
    }

    // QuantLib reads the index's calendar and day counter before anything else, which is undefined for a null index.
    private static OvernightIndex NotNull(OvernightIndex overnightIndex)
    {
        ArgumentNullException.ThrowIfNull(overnightIndex);
        return overnightIndex;
    }
}
