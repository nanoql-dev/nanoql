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

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A rate helper for bootstrapping over FX swap rates: the forward points the curve and the collateral curve imply
/// between the swap's earliest and latest dates, the forward being the spot plus the points.
/// </summary>
/// <remarks>
/// <para>
/// <c>isFxBaseCurrencyCollateralCurrency</c> says whether the base currency of the pair is the collateral currency.
/// The calendar is usually the joint calendar of the pair's two currencies. A trading calendar, usually the business
/// center's (<see cref="UnitedStates"/>), when given, adjusts the earliest date and moves the latest one together with
/// the calendar; a joint calendar holding it given as the calendar wouldn't do, since the spot convention uses the
/// calendar alone for the earliest date. QuantLib warns that overnight swaps (no fixing days, a tenor of 1 day) and
/// tomorrow-next ones (1 fixing day) on crosses without the business center's currency are not trivial: a holiday
/// there today or tomorrow moves or removes the trade, and a trading calendar is then safer left out.
/// </para>
/// <para>
/// A helper built from dates has no fixing days, convention or end-of-month flag, which QuantLib leaves uninitialized
/// and returns from its inspectors: here those three are methods, which refuse for such a helper. Its tenor is zero
/// days and its calendars are QuantLib's empty one, <see langword="null"/>. The collateral curve is read directly when
/// the helper is priced.
/// </para>
/// </remarks>
public sealed class FxSwapRateHelper : RelativeDateRateHelper
{
    private readonly int fixingDays;
    private readonly NanoQL.Time.BusinessDayConvention convention;
    private readonly bool endOfMonth;
    private readonly YieldTermStructure? collateralCurve;

    /// <summary>
    /// Initializes a new instance of the <see cref="FxSwapRateHelper"/> class for an FX swap of a tenor, starting after
    /// its fixing days from the evaluation date, adjusted to a business day.
    /// </summary>
    /// <param name="fwdPoint">The forward points.</param>
    /// <param name="spotFx">The spot rate.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="calendar">The calendar, usually the joint calendar of the pair's currencies.</param>
    /// <param name="convention">The business-day convention of the latest date.</param>
    /// <param name="endOfMonth">Whether the latest date follows the end-of-month rule.</param>
    /// <param name="isFxBaseCurrencyCollateralCurrency">
    /// Whether the base currency of the pair is the collateral currency.
    /// </param>
    /// <param name="collateralCurve">
    /// The collateral currency's curve, or <see langword="null"/>, QuantLib's empty handle: a helper on none can't be
    /// bound to a curve.
    /// </param>
    /// <param name="context">The evaluation context whose evaluation date the dates are counted from.</param>
    /// <param name="tradingCalendar">The trading calendar, or <see langword="null"/>, QuantLib's empty one, for none.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="calendar"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fixingDays"/> is negative, or <paramref name="convention"/> isn't a member of its type.
    /// </exception>
    public FxSwapRateHelper(
        double fwdPoint,
        double spotFx,
        Period tenor,
        int fixingDays,
        Calendar calendar,
        NanoQL.Time.BusinessDayConvention convention,
        bool endOfMonth,
        bool isFxBaseCurrencyCollateralCurrency,
        YieldTermStructure? collateralCurve,
        EvaluationContext context,
        Calendar? tradingCalendar = null)
        : base(fwdPoint, context)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);
        ArgumentNullException.ThrowIfNull(calendar);
        EnumArguments.Check(convention, nameof(convention));
        Spot = spotFx;
        Tenor = tenor;
        this.fixingDays = fixingDays;
        Calendar = calendar;
        this.convention = convention;
        this.endOfMonth = endOfMonth;
        IsFxBaseCurrencyCollateralCurrency = isFxBaseCurrencyCollateralCurrency;
        this.collateralCurve = collateralCurve;
        TradingCalendar = tradingCalendar;
        AdjustmentCalendar = tradingCalendar is null
            ? calendar
            : new JointCalendar(tradingCalendar, calendar, JointCalendarRule.JoinHolidays);
        InitializeDates(context);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxSwapRateHelper"/> class for an FX swap between explicit start and
    /// end dates; its dates don't follow the evaluation date, and it has no fixing days, convention or end-of-month
    /// flag.
    /// </summary>
    /// <param name="fwdPoint">The forward points.</param>
    /// <param name="spotFx">The spot rate.</param>
    /// <param name="startDate">The swap's start date, its earliest date.</param>
    /// <param name="endDate">The swap's end date, its latest date.</param>
    /// <param name="isFxBaseCurrencyCollateralCurrency">
    /// Whether the base currency of the pair is the collateral currency.
    /// </param>
    /// <param name="collateralCurve">
    /// The collateral currency's curve, or <see langword="null"/>, QuantLib's empty handle: a helper on none can't be
    /// bound to a curve.
    /// </param>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    public FxSwapRateHelper(
        double fwdPoint,
        double spotFx,
        Date startDate,
        Date endDate,
        bool isFxBaseCurrencyCollateralCurrency,
        YieldTermStructure? collateralCurve)
        : base(fwdPoint, null, false)
    {
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(endDate, nameof(endDate));
        Spot = spotFx;
        IsFxBaseCurrencyCollateralCurrency = isFxBaseCurrencyCollateralCurrency;
        this.collateralCurve = collateralCurve;
        earliestDate = startDate;
        latestDate = endDate;
    }

    /// <summary>Gets the spot rate: QuantLib's <c>spot()</c>.</summary>
    public double Spot { get; }

    /// <summary>Gets the swap's tenor: zero days for a helper built from dates.</summary>
    public Period Tenor { get; }

    /// <summary>
    /// Gets the calendar, or <see langword="null"/>, QuantLib's empty calendar, for a helper built from dates.
    /// </summary>
    public Calendar? Calendar { get; }

    /// <summary>Gets a value indicating whether the base currency of the pair is the collateral currency.</summary>
    public bool IsFxBaseCurrencyCollateralCurrency { get; }

    /// <summary>
    /// Gets the trading calendar, or <see langword="null"/>, QuantLib's empty calendar, when none is given.
    /// </summary>
    public Calendar? TradingCalendar { get; }

    /// <summary>
    /// Gets the calendar the earliest date is adjusted on and the latest one is moved on: the trading calendar joined
    /// with the calendar, or the calendar alone; <see langword="null"/>, QuantLib's empty calendar, for a helper built
    /// from dates.
    /// </summary>
    public Calendar? AdjustmentCalendar { get; }

    /// <summary>The fixing days: QuantLib's <c>fixingDays()</c>.</summary>
    /// <returns>The fixing days.</returns>
    /// <exception cref="NanoQLException">
    /// The helper was built from dates, where QuantLib's are uninitialized.
    /// </exception>
    public int FixingDays() =>
        UpdateDates ? fixingDays : throw new NanoQLException("no fixing days for an FX swap helper built from dates");

    /// <summary>The business-day convention of the latest date: QuantLib's <c>businessDayConvention()</c>.</summary>
    /// <returns>The convention.</returns>
    /// <exception cref="NanoQLException">
    /// The helper was built from dates, where QuantLib's is uninitialized.
    /// </exception>
    public NanoQL.Time.BusinessDayConvention BusinessDayConvention() =>
        UpdateDates
            ? convention
            : throw new NanoQLException("no business-day convention for an FX swap helper built from dates");

    /// <summary>Whether the latest date follows the end-of-month rule: QuantLib's <c>endOfMonth()</c>.</summary>
    /// <returns>The flag.</returns>
    /// <exception cref="NanoQLException">
    /// The helper was built from dates, where QuantLib's is uninitialized.
    /// </exception>
    public bool EndOfMonth() =>
        UpdateDates
            ? endOfMonth
            : throw new NanoQLException("no end-of-month flag for an FX swap helper built from dates");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">A date is outside a curve's range.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        if (collateralCurve is null)
        {
            throw new NanoQLException("collateral term structure not set");
        }

        Date earliest = earliestDate!.Value;
        Date latest = latestDate!.Value;
        double d1 = collateralCurve.Discount(earliest);
        double d2 = collateralCurve.Discount(latest);
        double collRatio = d1 / d2;
        d1 = TermStructure!.Discount(earliest);
        d2 = TermStructure.Discount(latest);
        double ratio = d1 / d2;
        double spot = Spot;
        if (IsFxBaseCurrencyCollateralCurrency)
        {
            return ((ratio / collRatio) - 1) * spot;
        }
        else
        {
            return ((collRatio / ratio) - 1) * spot;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The helper has no collateral curve.</exception>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        // QuantLib links its unused relinkable handle to the collateral curve's here, which refuses an empty handle
        if (collateralCurve is null)
        {
            throw new NanoQLException("empty Handle cannot be dereferenced");
        }

        return base.WithTermStructure(t);
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // if the evaluation date is not a business day
        // then move to the next business day
        Calendar calendar = Calendar!;
        Date refDate = calendar.Adjust(context.EvaluationDate);
        Date earliest = calendar.Advance(refDate, fixingDays, TimeUnit.Days);

        if (TradingCalendar is not null)
        {
            // check if fx trade can be settled in US, if not, adjust it
            earliest = AdjustmentCalendar!.Adjust(earliest);
            latestDate = AdjustmentCalendar.Advance(earliest, Tenor, convention, endOfMonth);
        }
        else
        {
            latestDate = calendar.Advance(earliest, Tenor, convention, endOfMonth);
        }

        earliestDate = earliest;
    }
}
