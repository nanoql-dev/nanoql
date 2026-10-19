// SPDX-FileCopyrightText: Copyright (C) 2004 Jeff Yu
// SPDX-FileCopyrightText: Copyright (C) 2004 M-Dimension Consulting Inc.
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008, 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2022 Oleg Kulkov
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bond.hpp ql/instruments/bond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>The base bond class: a leg of cash flows, its redemptions among them, with a settlement rule.</summary>
/// <remarks>
/// <para>
/// Most members assume that the cash flows are sorted by date, the redemptions after any cash flow on the same date;
/// with a single redemption, it is the last cash flow. Prices are per 100 of the notional at the settlement date, as
/// QuantLib's are: for a bond whose notional isn't 100, the cash price is the quote times the notional over 100, and
/// the yield and Z-spread functions take their prices per 100 too.
/// </para>
/// <para>
/// Unsealed, an extension point, as QuantLib's concrete base: the fixed-rate, floating-rate, zero-coupon and amortizing
/// bonds derive from it, and later bonds will. Its public constructors are QuantLib's two, which sort the cash flows
/// they are given (stably: equal-dated cash flows keep their order) and check the issue date against the first
/// payment. <b>The construction contract</b> is QuantLib's protected interface: a derived constructor calls the
/// protected <see cref="Bond(int, Calendar, Date?)"/>, which builds no leg, then writes the protected
/// <see cref="cashflows"/>, <see cref="maturityDate"/> and, for a bond with its own redemptions,
/// <see cref="redemptions"/>, and calls the helpers that fill the notional schedule and the redemptions:
/// <see cref="AddRedemptionsToCashflows"/>, <see cref="SetSingleRedemption(double, double, Date)"/>,
/// <see cref="SetSingleRedemption(double, CashFlow)"/> or <see cref="CalculateNotionalsFromCashflows"/>. That path
/// keeps QuantLib's omissions: no issue-date check, and no sort of a leg already sorted. The protected state and the
/// helpers are written while the bond is built, by a derived constructor, or on a fresh copy
/// (<see cref="WithCashflows"/>), and never after: a convention the base can't enforce, since a derived class's every
/// member can reach them, on which the bond's immutability rests.
/// </para>
/// <para>
/// The members that read the evaluation date, directly or through the settlement date or a coupon's fixing, take the
/// context, QuantLib's default date becoming <see langword="null"/>: the settlement date, the notional, tradability,
/// the accrued amount, the coupon rates, the cash-flow dates, and the prices and yields from a yield. The members that
/// read QuantLib's attached engine (<c>cleanPrice()</c>, <c>dirtyPrice()</c>, <c>settlementValue()</c> and the
/// theoretical <c>yield</c>) take that engine's results instead, with the context: they recompute the settlement date
/// and the accrued amount from it, as QuantLib's recompute them from its one evaluation date, so the results and the
/// context must be the same call's. Results read under another context mix two settlement dates, which nothing here
/// can detect.
/// </para>
/// </remarks>
public class Bond : Instrument
{
    /// <summary>QuantLib's <c>cashflows_</c>: every cash flow, the redemptions among them, sorted by date.</summary>
    protected ImmutableArray<CashFlow> cashflows = [];

    /// <summary>QuantLib's <c>redemptions_</c>: the redemption and amortizing payments among the cash flows.</summary>
    protected ImmutableArray<CashFlow> redemptions = [];

    /// <summary>
    /// QuantLib's <c>notionalSchedule_</c>: a null date, then the last date each notional of <see cref="notionals"/>
    /// is outstanding; empty for a bond with no cash flows.
    /// </summary>
    protected ImmutableArray<Date?> notionalSchedule = [];

    /// <summary>QuantLib's <c>notionals_</c>: each notional, then 0 after the last date of the schedule.</summary>
    protected ImmutableArray<double> notionals = [];

    /// <summary>QuantLib's <c>maturityDate_</c>: <see langword="null"/> for the cash flows' own maturity.</summary>
    protected Date? maturityDate;

    private readonly Calendar? calendar;
    private readonly Date? issueDate;

    /// <summary>
    /// Initializes a new instance of the <see cref="Bond"/> class from its coupons, amortizing or not: QuantLib's
    /// first constructor. The redemptions follow from the coupons' notionals, so the coupons must hold none.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">
    /// The settlement calendar, or <see langword="null"/>, QuantLib's empty calendar, on which only the members given
    /// explicit dates work.
    /// </param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="coupons">
    /// The coupons, sorted by date when built; empty by default, which builds a bond whose notional is refused.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// A coupon is <see langword="null"/>; the issue date is <c>default(Date)</c>, or not earlier than the first
    /// payment date.
    /// </exception>
    /// <exception cref="NanoQLException">No cash flow is a coupon.</exception>
    /// <remarks>The maturity date is the date of the last coupon given, before the sort, as QuantLib's is.</remarks>
    public Bond(
        int settlementDays,
        Calendar? calendar,
        Date? issueDate = null,
        ImmutableArray<CashFlow> coupons = default)
        : this(settlementDays, calendar, issueDate)
    {
        ImmutableArray<CashFlow> leg = CheckLeg(coupons, nameof(coupons));
        if (!leg.IsEmpty)
        {
            cashflows = [.. leg.OrderBy(c => c, EarlierThan)];

            if (issueDate is Date issue)
            {
                CheckIssueDate(issue);
            }

            maturityDate = leg[^1].Date;

            AddRedemptionsToCashflows();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Bond"/> class from its cash flows, the last one its redemption: the
    /// old constructor for non-amortizing bonds.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">
    /// The settlement calendar, or <see langword="null"/>, QuantLib's empty calendar, on which only the members given
    /// explicit dates work.
    /// </param>
    /// <param name="faceAmount">The notional until maturity.</param>
    /// <param name="maturityDate">The maturity date, or <see langword="null"/> for the cash flows' maturity.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="cashflows">
    /// The cash flows: the last one is the redemption, wherever its date, and no other may be later. All but the last
    /// are sorted by date when built. Empty by default, which builds a bond whose notional is refused.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// A cash flow is <see langword="null"/>; a date is <c>default(Date)</c>; or the issue date isn't earlier than the
    /// first payment date.
    /// </exception>
    public Bond(
        int settlementDays,
        Calendar? calendar,
        double faceAmount,
        Date? maturityDate,
        Date? issueDate = null,
        ImmutableArray<CashFlow> cashflows = default)
        : this(settlementDays, calendar, issueDate)
    {
        Date.ThrowIfDefault(maturityDate, nameof(maturityDate));
        ImmutableArray<CashFlow> leg = CheckLeg(cashflows, nameof(cashflows));
        this.maturityDate = maturityDate;
        if (!leg.IsEmpty)
        {
            this.cashflows = [.. leg.Take(leg.Length - 1).OrderBy(c => c, EarlierThan), leg[^1]];

            this.maturityDate ??= CashFlows.MaturityDate(leg);

            if (issueDate is Date issue)
            {
                CheckIssueDate(issue);
            }

            notionals = [faceAmount, 0.0];
            notionalSchedule = [null, this.maturityDate];

            redemptions = [leg[^1]];
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Bond"/> class with no cash flows, for a derived constructor to
    /// fill: QuantLib's derived bonds call its first constructor without a leg, then write the protected state and
    /// call the protected helpers (see the class's remarks).
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The settlement calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">The issue date is <c>default(Date)</c>.</exception>
    protected Bond(int settlementDays, Calendar? calendar, Date? issueDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        Date.ThrowIfDefault(issueDate, nameof(issueDate));
        SettlementDays = settlementDays;
        this.calendar = calendar;
        this.issueDate = issueDate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Bond"/> class as a plain copy of another bond: the base's state
    /// only, as QuantLib's bond helper copies a bond (<c>make_shared&lt;Bond&gt;(*bond)</c>), so that a derived bond's
    /// overrides don't reach the copy's prices.
    /// </summary>
    /// <param name="other">The bond copied; its cash flows are shared.</param>
    internal Bond(Bond other)
    {
        SettlementDays = other.SettlementDays;
        calendar = other.calendar;
        issueDate = other.issueDate;
        cashflows = other.cashflows;
        redemptions = other.redemptions;
        notionalSchedule = other.notionalSchedule;
        notionals = other.notionals;
        maturityDate = other.maturityDate;
    }

    /// <summary>Gets the settlement days.</summary>
    public int SettlementDays { get; }

    /// <summary>Gets the settlement calendar, or <see langword="null"/> for QuantLib's empty calendar.</summary>
    public Calendar? Calendar => calendar;

    /// <summary>Gets the notionals: each notional outstanding, then 0 after maturity.</summary>
    public ImmutableArray<double> Notionals => notionals;

    /// <summary>Gets every cash flow, the redemptions among them.</summary>
    public ImmutableArray<CashFlow> Cashflows => cashflows;

    /// <summary>Gets the redemption and amortizing payments, without the interest payments.</summary>
    public ImmutableArray<CashFlow> Redemptions => redemptions;

    /// <summary>Gets the issue date, or <see langword="null"/> for none.</summary>
    public Date? IssueDate => issueDate;

    private static Comparer<CashFlow> EarlierThan { get; } = Comparer<CashFlow>.Create(CashFlowFunctions.EarlierThan);

    /// <inheritdoc/>
    /// <remarks>
    /// The cash flows' settlement date is the evaluation date, and whether a cash flow on it has occurred follows the
    /// context's <see cref="EvaluationContext.IncludeReferenceDateEvents"/> and
    /// <see cref="EvaluationContext.IncludeTodaysCashFlows"/>.
    /// </remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // this is the Instrument interface, so it doesn't use
        // BondFunctions.  We pass nullopt as includeSettlementDateFlows
        // so that CashFlows::isExpired uses the default setting.
        return CashFlows.IsExpired(cashflows, context, null, context.EvaluationDate);
    }

    /// <inheritdoc/>
    /// <remarks>An expired bond's settlement value is 0.</remarks>
    public override BondResults SetupExpired()
    {
        InstrumentResults expired = base.SetupExpired();
        return new BondResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
            SettlementValue = 0.0,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The bond has no cash flows, or a null one.</exception>
    /// <remarks>
    /// QuantLib's checks on its engine's arguments but the settlement date's, which is never null here. Through an
    /// engine they can't fail: an engine sees a bond with no cash flows as expired first, and the constructors refuse a
    /// null cash flow; a derived bond that writes the protected state could reach them.
    /// </remarks>
    public override void Validate()
    {
        if (cashflows.IsDefaultOrEmpty)
        {
            throw new NanoQLException("no cash flow provided");
        }

        foreach (CashFlow cf in cashflows)
        {
            if (cf is null)
            {
                throw new NanoQLException("null cash flow provided");
            }
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The results aren't a bond's.</exception>
    public override BondResults FetchResults(InstrumentResults results)
    {
        InstrumentResults fetched = base.FetchResults(results);
        if (fetched is not BondResults r)
        {
            throw new ArgumentException("wrong result type", nameof(results));
        }

        return r;
    }

    /// <summary>The notional outstanding at a date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="d">The date; the bond's settlement date when <see langword="null"/>.</param>
    /// <returns>
    /// The notional: 0 after the last date of the notional schedule, and on a date of the schedule the next notional,
    /// since the payment on that date has occurred.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">
    /// The bond has no cash flows, so no notional schedule (QuantLib reads past an empty schedule's end), or the
    /// settlement date can't be computed on QuantLib's empty calendar.
    /// </exception>
    public virtual double Notional(EvaluationContext context, Date? d = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(d, nameof(d));
        Date date = d ?? SettlementDate(context);

        if (notionalSchedule.IsDefaultOrEmpty)
        {
            throw new NanoQLException("no notional schedule: the bond has no cash flows");
        }

        if (date > notionalSchedule[^1]!.Value)
        {
            // after maturity
            return 0.0;
        }

        // After the check above, d is between the schedule
        // boundaries.  We search starting from the second notional
        // date, since the first is null.  After the call to
        // lower_bound, *i is the earliest date which is greater or
        // equal than d.  Its index is greater or equal to 1.
        int index = LowerBound(notionalSchedule, 1, date);

        if (date < notionalSchedule[index]!.Value)
        {
            // no doubt about what to return
            return notionals[index - 1];
        }
        else
        {
            // d is equal to a redemption date.
            // As per bond conventions, the payment has occurred;
            // the bond already changed notional.
            return notionals[index];
        }
    }

    /// <summary>The redemption, when the bond has a single one.</summary>
    /// <returns>The redemption.</returns>
    /// <exception cref="NanoQLException">The bond has no redemption or several.</exception>
    public CashFlow Redemption()
    {
        if (redemptions.Length != 1)
        {
            throw new NanoQLException("multiple redemption cash flows given");
        }

        return redemptions[^1];
    }

    /// <summary>The start date: the earliest accrual start date of the coupons, or date of the other cash flows.</summary>
    /// <returns>The start date.</returns>
    /// <exception cref="ArgumentException">The bond has no cash flows.</exception>
    public Date StartDate() => BondFunctions.StartDate(this);

    /// <summary>The maturity date: the one the bond was built with, or else its cash flows'.</summary>
    /// <returns>The maturity date.</returns>
    /// <exception cref="ArgumentException">The bond has neither a maturity date nor cash flows.</exception>
    public Date MaturityDate() => maturityDate ?? BondFunctions.MaturityDate(this);

    /// <summary>Whether the bond is tradable at a date: whether its notional there isn't 0.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="d">The date; the bond's settlement date when <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the notional isn't 0.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The bond has no cash flows.</exception>
    public bool IsTradable(EvaluationContext context, Date? d = null) => BondFunctions.IsTradable(this, context, d);

    /// <summary>
    /// The settlement date for a trade date: the date advanced by the settlement days on the calendar, and not before
    /// the issue date when there is one.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="d">The trade date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The settlement date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The calendar is QuantLib's empty calendar.</exception>
    public Date SettlementDate(EvaluationContext context, Date? d = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(d, nameof(d));
        Date date = d ?? context.EvaluationDate;

        // usually, the settlement is at T+n...
        Date settlement = (calendar ?? EmptyCalendar.Instance).Advance(date, SettlementDays, TimeUnit.Days);

        // ...but the bond won't be traded until the issue date (if given.)
        if (issueDate is not Date issue)
        {
            return settlement;
        }
        else
        {
            return settlement < issue ? issue : settlement;
        }
    }

    /// <summary>
    /// The theoretical clean price, per 100 of notional, from an engine's results: the dirty price less the accrued
    /// amount at the bond's settlement date.
    /// </summary>
    /// <param name="results">The results of an engine's pricing of this bond, under <paramref name="context"/>.</param>
    /// <param name="context">The evaluation context the engine priced under, which gives the settlement date.</param>
    /// <returns>The clean price; 0 when the notional at the settlement date is 0.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The results hold no settlement value, or the bond has no cash flows.</exception>
    /// <remarks>
    /// The price computed from a flat curve may differ slightly from the one computed from the corresponding yield by
    /// <see cref="CleanPrice(double, DayCounter, Compounding, Frequency, EvaluationContext, Date?)"/>, which is the one
    /// to use for the price from a constant yield.
    /// </remarks>
    public double CleanPrice(BondResults results, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);
        return DirtyPrice(results, context) - AccruedAmount(context, SettlementDate(context));
    }

    /// <summary>
    /// The theoretical dirty price, per 100 of notional, from an engine's results: the settlement value per 100 of the
    /// notional at the bond's settlement date.
    /// </summary>
    /// <param name="results">The results of an engine's pricing of this bond, under <paramref name="context"/>.</param>
    /// <param name="context">The evaluation context the engine priced under, which gives the settlement date.</param>
    /// <returns>The dirty price; 0 when the notional at the settlement date is 0.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The results hold no settlement value, or the bond has no cash flows.</exception>
    public double DirtyPrice(BondResults results, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);
        double currentNotional = Notional(context, SettlementDate(context));
        if (currentNotional == 0.0)
        {
            return 0.0;
        }
        else
        {
            return SettlementValueOf(results) * 100.0 / currentNotional;
        }
    }

    /// <summary>The settlement value for a clean price: the dirty price times the notional at the settlement date.</summary>
    /// <param name="cleanPrice">The clean price, per 100 of notional.</param>
    /// <param name="context">The evaluation context, which gives the settlement date.</param>
    /// <returns>The settlement value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The bond has no cash flows.</exception>
    public double SettlementValue(double cleanPrice, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double dirtyPrice = cleanPrice + AccruedAmount(context, SettlementDate(context));
        return dirtyPrice / 100.0 * Notional(context, SettlementDate(context));
    }

    /// <summary>
    /// The theoretical yield, from an engine's results: the yield that reproduces the engine's clean or dirty price at
    /// the bond's settlement date, as <see cref="BondFunctions"/>' yield solves for it.
    /// </summary>
    /// <param name="results">The results of an engine's pricing of this bond, under <paramref name="context"/>.</param>
    /// <param name="dc">The yield's day counter.</param>
    /// <param name="comp">The yield's compounding.</param>
    /// <param name="freq">The yield's frequency.</param>
    /// <param name="context">The evaluation context the engine priced under, which gives the settlement date.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <param name="guess">The solver's initial guess.</param>
    /// <param name="priceType">The price the yield reproduces: the clean or the dirty one.</param>
    /// <returns>The yield; 0 when the notional at the settlement date is 0.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxEvaluations"/> is negative, or an enum argument isn't a member of its type.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The results hold no settlement value, the bond has no cash flows, or the solver fails.
    /// </exception>
    public double Yield(
        BondResults results,
        DayCounter dc,
        Compounding comp,
        Frequency freq,
        EvaluationContext context,
        double accuracy = 1.0e-8,
        int maxEvaluations = 100,
        double guess = 0.05,
        BondPriceType priceType = BondPriceType.Clean)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        EnumArguments.Check(priceType, nameof(priceType));
        double currentNotional = Notional(context, SettlementDate(context));
        if (currentNotional == 0.0)
        {
            return 0.0;
        }

        var price = new BondPrice(
            priceType == BondPriceType.Clean ? CleanPrice(results, context) : DirtyPrice(results, context), priceType);

        return BondFunctions.Yield(
            this, price, dc, comp, freq, context, SettlementDate(context), accuracy, maxEvaluations, guess);
    }

    /// <summary>The clean price, per 100 of notional, for a yield.</summary>
    /// <param name="yield">The yield.</param>
    /// <param name="dc">The yield's day counter.</param>
    /// <param name="comp">The yield's compounding.</param>
    /// <param name="freq">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The clean price.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// The bond isn't tradable at the settlement date, or a compounded yield has no frequency.
    /// </exception>
    public double CleanPrice(
        double yield,
        DayCounter dc,
        Compounding comp,
        Frequency freq,
        EvaluationContext context,
        Date? settlementDate = null) =>
        BondFunctions.CleanPrice(this, yield, dc, comp, freq, context, settlementDate);

    /// <summary>The dirty price, per 100 of notional, for a yield.</summary>
    /// <param name="yield">The yield.</param>
    /// <param name="dc">The yield's day counter.</param>
    /// <param name="comp">The yield's compounding.</param>
    /// <param name="freq">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The dirty price; 0 when the notional at the settlement date is 0.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">A compounded yield has no frequency.</exception>
    public double DirtyPrice(
        double yield,
        DayCounter dc,
        Compounding comp,
        Frequency freq,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        double currentNotional = Notional(context, settlementDate);
        if (currentNotional == 0.0)
        {
            return 0.0;
        }

        return BondFunctions.CleanPrice(this, yield, dc, comp, freq, context, settlementDate)
            + AccruedAmount(context, settlementDate);
    }

    /// <summary>The yield for a price.</summary>
    /// <param name="price">The clean or dirty price, per 100 of notional.</param>
    /// <param name="dc">The yield's day counter.</param>
    /// <param name="comp">The yield's compounding.</param>
    /// <param name="freq">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <param name="guess">The solver's initial guess.</param>
    /// <returns>The yield; 0 when the notional at the settlement date is 0.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxEvaluations"/> is negative, or an enum argument isn't a member of its type.
    /// </exception>
    /// <exception cref="NanoQLException">The solver fails.</exception>
    public double Yield(
        BondPrice price,
        DayCounter dc,
        Compounding comp,
        Frequency freq,
        EvaluationContext context,
        Date? settlementDate = null,
        double accuracy = 1.0e-8,
        int maxEvaluations = 100,
        double guess = 0.05)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(price);
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        double currentNotional = Notional(context, settlementDate);
        if (currentNotional == 0.0)
        {
            return 0.0;
        }

        return BondFunctions.Yield(
            this, price, dc, comp, freq, context, settlementDate, accuracy, maxEvaluations, guess);
    }

    /// <summary>The accrued amount, per 100 of notional, at a date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="d">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The accrued amount; 0 when the notional at the date is 0.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The bond has no cash flows.</exception>
    public virtual double AccruedAmount(EvaluationContext context, Date? d = null)
    {
        double currentNotional = Notional(context, d);
        if (currentNotional == 0.0)
        {
            return 0.0;
        }

        return BondFunctions.AccruedAmount(this, context, d);
    }

    /// <summary>
    /// The rate of the next coupon after a date: historic, deterministic or expected, depending on the bond and the
    /// date. At the bond's settlement date it is the coupon already fixed and not yet paid.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="d">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The rate; 0 when no coupon is still to be paid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="d"/> is <c>default(Date)</c>, or coupons on the next payment date differ in nominal, accrual
    /// period or day counter.
    /// </exception>
    /// <exception cref="NanoQLException">No coupon is paid on the next payment date.</exception>
    public virtual double NextCouponRate(EvaluationContext context, Date? d = null) =>
        BondFunctions.NextCouponRate(this, context, d);

    /// <summary>
    /// The rate of the last coupon paid at a date: historic, deterministic or expected, depending on the bond and the
    /// date. At the bond's settlement date it is the last one paid.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="d">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The rate; 0 when no coupon has been paid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="d"/> is <c>default(Date)</c>, or coupons on that payment date differ in nominal, accrual period
    /// or day counter.
    /// </exception>
    /// <exception cref="NanoQLException">No coupon is paid on the last payment date.</exception>
    public double PreviousCouponRate(EvaluationContext context, Date? d = null) =>
        BondFunctions.PreviousCouponRate(this, context, d);

    /// <summary>The date of the next cash flow after a date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="d">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when every cash flow has been paid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public Date? NextCashFlowDate(EvaluationContext context, Date? d = null) =>
        BondFunctions.NextCashFlowDate(this, context, d);

    /// <summary>The date of the last cash flow paid at a date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="d">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no cash flow has been paid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public Date? PreviousCashFlowDate(EvaluationContext context, Date? d = null) =>
        BondFunctions.PreviousCashFlowDate(this, context, d);

    /// <summary>
    /// A copy of the bond holding other cash flows:
    /// <see cref="CouponPricerFunctions.SetCouponPricer{T}(T, FloatingRateCouponPricer?)"/>'s step, which QuantLib does in
    /// place on the coupons a built bond holds. The base's copy is memberwise, with the new leg, so the copy keeps the
    /// bond's type, terms, notional schedule and redemptions: the new leg must hold the same dates, nominals and
    /// redemption objects, as a leg with its pricers set does. A derived bond that keeps other state derived from its
    /// cash flows overrides it, and an override returns a copy of the same runtime type.
    /// </summary>
    /// <param name="newCashflows">The new cash flows.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    /// <exception cref="ArgumentException">The leg holds a null cash flow.</exception>
    protected internal virtual Bond WithCashflows(ImmutableArray<CashFlow> newCashflows)
    {
        ImmutableArray<CashFlow> leg = CheckLeg(newCashflows, nameof(newCashflows));
        var copy = (Bond)MemberwiseClone();
        copy.cashflows = leg;
        return copy;
    }

    /// <summary>
    /// Builds the redemptions from the coupons' notionals and adds them to the cash flows: a derived constructor calls
    /// it after writing <see cref="cashflows"/>, which must hold at least one coupon and be sorted by date. It fills the
    /// notional schedule, the notionals and the redemptions.
    /// </summary>
    /// <param name="redemptions">
    /// The redemptions in base 100, multiplying each redemption's amount: 100 leaves it unchanged. Each notional step
    /// <c>i</c> from 1 takes element <c>i</c>, or the last element past the list's end, so element 0 is read only for a
    /// one-element list; empty by default, which redeems at 100.
    /// </param>
    /// <exception cref="NanoQLException">No cash flow is a coupon.</exception>
    /// <remarks>
    /// The redemptions are amortizing payments at each step but the last, a redemption at the last, each sorted after
    /// the coupons of its date by a stable sort, as QuantLib's <c>std::stable_sort</c> does.
    /// </remarks>
    protected void AddRedemptionsToCashflows(IEnumerable<double>? redemptions = null)
    {
        ImmutableArray<double> values = redemptions is null ? [] : [.. redemptions];

        // First, we gather the notional information from the cashflows
        CalculateNotionalsFromCashflows();

        // Then, we create the redemptions based on the notional
        // information and we add them to the cashflows vector after
        // the coupons.
        ImmutableArray<CashFlow>.Builder all = cashflows.ToBuilder();
        ImmutableArray<CashFlow>.Builder added = ImmutableArray.CreateBuilder<CashFlow>();
        for (int i = 1; i < notionalSchedule.Length; ++i)
        {
            double r = i < values.Length ? values[i]
                : !values.IsEmpty ? values[^1]
                : 100.0;
            double amount = r / 100.0 * (notionals[i - 1] - notionals[i]);
            Date paymentDate = notionalSchedule[i]!.Value;
            CashFlow payment;
            if (i < notionalSchedule.Length - 1)
            {
                payment = new AmortizingPayment(amount, paymentDate);
            }
            else
            {
                payment = new Redemption(amount, paymentDate);
            }

            all.Add(payment);
            added.Add(payment);
        }

        this.redemptions = added.ToImmutable();

        // stable_sort now moves the redemptions to the right places
        // while ensuring that they follow coupons with the same date.
        cashflows = [.. all.OrderBy(c => c, EarlierThan)];
    }

    /// <summary>
    /// Builds a single redemption of a notional at a price, on a date, and adds it to the cash flows: a derived
    /// constructor calls it for a bond redeemed once. It fills the notional schedule, the notionals and the
    /// redemptions.
    /// </summary>
    /// <param name="notional">The notional.</param>
    /// <param name="redemption">The redemption price, in base 100.</param>
    /// <param name="date">The redemption date.</param>
    protected void SetSingleRedemption(double notional, double redemption, Date date)
    {
        CashFlow redemptionCashflow = new Redemption(notional * redemption / 100.0, date);
        SetSingleRedemption(notional, redemptionCashflow);
    }

    /// <summary>
    /// Makes a cash flow the single redemption of a notional and appends it to the cash flows, unsorted: a derived
    /// constructor calls it for a bond redeemed once. It fills the notional schedule, the notionals and the
    /// redemptions.
    /// </summary>
    /// <param name="notional">The notional.</param>
    /// <param name="redemption">The redemption.</param>
    /// <exception cref="ArgumentNullException"><paramref name="redemption"/> is <see langword="null"/>.</exception>
    protected void SetSingleRedemption(double notional, CashFlow redemption)
    {
        ArgumentNullException.ThrowIfNull(redemption);
        notionals = [notional, 0.0];
        notionalSchedule = [null, redemption.Date];

        cashflows = cashflows.Add(redemption);
        redemptions = [redemption];
    }

    /// <summary>
    /// Collects the notional schedule from the coupons, in the order of the cash flows: a new notional starts at each
    /// coupon whose contract nominal isn't close to the previous one's, and each ends at the last payment date of its
    /// coupons. It fills the notional schedule and the notionals, not the redemptions: a derived constructor calls it
    /// only when it provides its own redemptions, which it then writes into <see cref="redemptions"/>.
    /// </summary>
    /// <exception cref="NanoQLException">No cash flow is a coupon.</exception>
    protected void CalculateNotionalsFromCashflows()
    {
        ImmutableArray<Date?>.Builder schedule = ImmutableArray.CreateBuilder<Date?>();
        ImmutableArray<double>.Builder values = ImmutableArray.CreateBuilder<double>();

        Date? lastPaymentDate = null;
        schedule.Add(null);
        foreach (CashFlow cashflow in cashflows)
        {
            if (cashflow is not Coupon coupon)
            {
                continue;
            }

            double notional = coupon.ContractNominal;

            // we add the notional only if it is the first one...
            if (values.Count == 0)
            {
                values.Add(coupon.ContractNominal);
                lastPaymentDate = coupon.Date;
            }
            else if (!ComparisonFunctions.Close(notional, values[^1]))
            {
                // ...or if it has changed.
                values.Add(coupon.ContractNominal);

                // in this case, we also add the last valid date for
                // the previous one...
                schedule.Add(lastPaymentDate);

                // ...and store the candidate for this one.
                lastPaymentDate = coupon.Date;
            }
            else
            {
                // otherwise, we just extend the valid range of dates
                // for the current notional.
                lastPaymentDate = coupon.Date;
            }
        }

        if (values.Count == 0)
        {
            throw new NanoQLException("no coupons provided");
        }

        values.Add(0.0);
        schedule.Add(lastPaymentDate);
        notionals = values.ToImmutable();
        notionalSchedule = schedule.ToImmutable();
    }

    /// <summary>
    /// Refuses the bond where an engine for plain bonds reads it, after the expiry check and before the engine's body:
    /// QuantLib's <c>setupArguments</c>, which a bond whose engines take other arguments overrides to refuse.
    /// </summary>
    internal virtual void CheckPlainBondArguments()
    {
    }

    private static ImmutableArray<CashFlow> CheckLeg(ImmutableArray<CashFlow> leg, string paramName)
    {
        if (leg.IsDefault)
        {
            return [];
        }

        if (leg.Any(cf => cf is null))
        {
            throw new ArgumentException("null cash flow in leg", paramName);
        }

        return leg;
    }

    // std::lower_bound over the schedule from a position: the first index whose date isn't earlier than d.
    private static int LowerBound(ImmutableArray<Date?> schedule, int from, Date d)
    {
        int first = from, count = schedule.Length - from;
        while (count > 0)
        {
            int step = count / 2;
            int it = first + step;
            if (schedule[it]!.Value < d)
            {
                first = it + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return first;
    }

    private static double SettlementValueOf(BondResults results) =>
        results.SettlementValue ?? throw new NanoQLException("settlement value not provided");

    private void CheckIssueDate(Date issue)
    {
        if (!(issue < cashflows[0].Date))
        {
            throw new ArgumentException(
                $"issue date ({issue}) must be earlier than first payment date ({cashflows[0].Date})",
                nameof(issueDate));
        }
    }
}
