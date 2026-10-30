// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2015 Maddalena Zanzi
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/ratehelpers.hpp ql/termstructures/yield/ratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A rate helper for bootstrapping over deposit rates: the deposit's rate is the index fixing the curve forecasts for
/// its fixing date.
/// </summary>
/// <remarks>
/// The helper prices on a clone of its index, without fixings taken into account: the fixing is always forecast. Bound
/// to a curve, its copy forecasts on a clone of the index on that curve.
/// </remarks>
public sealed class DepositRateHelper : RelativeDateRateHelper
{
    // Written by the constructor, and on a fresh copy only: the index cloned on the bound curve, the fixing date when
    // the helper is re-dated.
    private IborIndex iborIndex;
    private Date fixingDate;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepositRateHelper"/> class for a deposit of the given conventions,
    /// starting after its fixing days from the evaluation date.
    /// </summary>
    /// <param name="rate">The deposit's rate.</param>
    /// <param name="tenor">The deposit's tenor.</param>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="endOfMonth">Whether the maturity follows the end-of-month rule.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context whose evaluation date the dates are counted from.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="calendar"/>, <paramref name="dayCounter"/> or <paramref name="context"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fixingDays"/> is negative, or <paramref name="convention"/> isn't a member of its type.
    /// </exception>
    /// <exception cref="NanoQLException">The tenor has an intraday unit.</exception>
    public DepositRateHelper(
        double rate,
        Period tenor,
        int fixingDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        DayCounter dayCounter,
        EvaluationContext context)
        : base(rate, context)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(dayCounter);

        // never take fixing into account
        iborIndex = new IborIndex("no-fix", tenor, fixingDays, null, calendar, convention, endOfMonth, dayCounter);
        InitializeDates(context);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DepositRateHelper"/> class for a deposit on an index's conventions,
    /// starting after its fixing days from the evaluation date.
    /// </summary>
    /// <param name="rate">The deposit's rate.</param>
    /// <param name="iborIndex">The index whose conventions the deposit follows.</param>
    /// <param name="context">The evaluation context whose evaluation date the dates are counted from.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="iborIndex"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public DepositRateHelper(double rate, IborIndex iborIndex, EvaluationContext context)
        : base(rate, context)
    {
        ArgumentNullException.ThrowIfNull(iborIndex);
        this.iborIndex = iborIndex.Clone(null);
        InitializeDates(context);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DepositRateHelper"/> class for a deposit on an index's conventions,
    /// fixing on a given date; its dates don't follow the evaluation date.
    /// </summary>
    /// <param name="rate">The deposit's rate.</param>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="iborIndex">The index whose conventions the deposit follows.</param>
    /// <exception cref="ArgumentNullException"><paramref name="iborIndex"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is <c>default(Date)</c>.</exception>
    public DepositRateHelper(double rate, Date fixingDate, IborIndex iborIndex)
        : base(rate, null, false)
    {
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        ArgumentNullException.ThrowIfNull(iborIndex);
        this.fixingDate = fixingDate;
        this.iborIndex = iborIndex.Clone(null);
        earliestDate = this.iborIndex.ValueDate(fixingDate);
        SetMaturityDates();
    }

    /// <inheritdoc/>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();

        // The fixing date can precede the evaluation date when the latter
        // is not a business day.  A rate helper must forecast the quote
        // from the bootstrapped curve even if a historical fixing exists
        // (QuantLib's fix after v1.44, #2899).
        return iborIndex.ForecastFixing(fixingDate, context);
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        var copy = (DepositRateHelper)base.WithTermStructure(t);
        copy.iborIndex = iborIndex.Clone(t);
        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        // fixing days are counted from the actual evaluation date,
        // even when it is not a business day (see issue #753)
        ArgumentNullException.ThrowIfNull(context);
        Date earliest = iborIndex.FixingCalendar.Advance(context.EvaluationDate, iborIndex.FixingDays, TimeUnit.Days);
        earliestDate = earliest;
        fixingDate = iborIndex.FixingDate(earliest);
        SetMaturityDates();
    }

    private void SetMaturityDates()
    {
        maturityDate = iborIndex.MaturityDate(earliestDate!.Value);
        pillarDate = latestDate = latestRelevantDate = maturityDate;
    }
}
