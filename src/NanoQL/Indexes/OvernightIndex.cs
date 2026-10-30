// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/iborindex.hpp ql/indexes/iborindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The base class of overnight indexes (SOFR, €STR, SONIA, ...): a one-day Ibor index.</summary>
/// <remarks>Unsealed, as in QuantLib, where the presets derive from it.</remarks>
public class OvernightIndex : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="OvernightIndex"/> class.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="settlementDays">The fixing days.</param>
    /// <param name="currency">The currency, or <see langword="null"/>, QuantLib's empty currency.</param>
    /// <param name="fixingCalendar">The fixing calendar.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="h">The curve that forecasts fixings, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    public OvernightIndex(
        string familyName,
        int settlementDays,
        Currency? currency,
        Calendar fixingCalendar,
        DayCounter dayCounter,
        YieldTermStructure? h = null)
        : this(familyName, settlementDays, currency, fixingCalendar, dayCounter, h, ConventionProvider.Current)
    {
    }

    // An index keeping a given provider: a preset's, read once, or a clone's source's.
    private protected OvernightIndex(
        string familyName,
        int settlementDays,
        Currency? currency,
        Calendar fixingCalendar,
        DayCounter dayCounter,
        YieldTermStructure? h,
        ConventionProvider provider)
        : base(
            familyName,
            new Period(1, TimeUnit.Days),
            settlementDays,
            currency,
            fixingCalendar,
            BusinessDayConvention.Following,
            false,
            dayCounter,
            h,
            provider)
    {
    }

    /// <summary>The factor a fixing grows a unit by over a span: <c>1 + fixing × span</c>, a simple rate's.</summary>
    /// <param name="fixing">The fixing, as the index quotes it.</param>
    /// <param name="span">The span, as a year fraction on the index's day counter.</param>
    /// <returns>The growth factor.</returns>
    /// <remarks>
    /// An extension point: an index whose fixings are quoted on another convention states its growth, as
    /// <see cref="Cdi"/>'s annually compounded rate does. QuantLib's overnight coupon pricers grow every fixing as a
    /// simple rate; nanoql's compounding pricers read this factor, which by default is their expression, and the
    /// averaging pricers read <see cref="SimpleAccrual"/>, which an index overriding this factor overrides too.
    /// </remarks>
    public virtual double GrowthFactor(double fixing, double span) => 1.0 + (fixing * span);

    /// <summary>The interest a fixing accrues per unit over a span: <c>fixing × span</c>, a simple rate's.</summary>
    /// <param name="fixing">The fixing, as the index quotes it.</param>
    /// <param name="span">The span, as a year fraction on the index's day counter.</param>
    /// <returns>The accrual.</returns>
    /// <remarks>
    /// The averaging pricers' term, the extension point beside <see cref="GrowthFactor"/>: an index whose fixings
    /// grow on another convention overrides both, its accrual its growth factor less one, as <see cref="Cdi"/> does.
    /// QuantLib's averaging pricers sum <c>fixing × span</c>; nanoql's read this accrual, which by default is their
    /// expression, so an index keeps QuantLib's bits. The Black pricers' forward parts read it too, to compare a daily
    /// cap's or floor's strike, as its accrual per unit of span, with the simple rate they estimate from the curve,
    /// reading the volatility at the strike as quoted; an index accruing simply keeps its strike as quoted.
    /// </remarks>
    public virtual double SimpleAccrual(double fixing, double span) => fixing * span;

    /// <summary>A copy of the index on another forwarding curve: a plain <see cref="OvernightIndex"/>, as in QuantLib.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public override OvernightIndex Clone(YieldTermStructure? h) =>
        new(FamilyName, FixingDays, Currency, FixingCalendar, DayCounter, h, Provider);
}
