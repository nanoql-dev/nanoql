// SPDX-FileCopyrightText: Copyright (C) 2008-2016  Andrea Maggiulli (a.maggiulli@gmail.com)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: src/QLNet/Instruments/Bonds/MBSFixedRateBond.cs @ QLNet-v1.13.2
// Ported from QLNet and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Extensions.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.Extensions.Instruments;

/// <summary>
/// A fixed-rate mortgage pass-through with prepayment: a pool of level-payment mortgages at a weighted-average coupon
/// (WAC), whose expected principal and pass-through interest reach the holder each period, the prepayments as a model
/// gives them.
/// </summary>
/// <remarks>
/// <para>
/// Each period pays a <see cref="FixedRateCoupon"/> at the pass-through rate on the pool's balance at the period's
/// start, its scheduled principal (<see cref="AmortizingPayment"/>), the level-payment schedule's at the WAC scaled to
/// the balance, and its prepayment (<see cref="VoluntaryPrepay"/>), the balance after the scheduled principal times
/// the period's single monthly mortality (SMM); the bond's leg holds them in that order on each date, as a bond's
/// redemptions follow its coupons. The model reads the pool's age: a date of the bond's schedule plus the original
/// length less the remaining one, so a seasoned pool starts its PSA ramp part way.
/// </para>
/// <para>
/// Ported from QLNet, which derives it from its <c>AmortizingFixedRateBond</c>; here it builds on <see cref="Bond"/>,
/// its schedule and the level-payment notionals from <see cref="AmortizingFixedRateBondFunctions"/>. Its departures
/// from QLNet: the notional schedule and the redemptions are the pool's expected ones, so the yields price the pool's
/// balance at the settlement, where QLNet's read the level-payment schedule's; the flows pay on the period ends
/// adjusted by the payment convention on the calendar, which QLNet ignores; a period of a frequency other than monthly
/// survives the product of its months' prepayments (or, for a period counted in weeks or days, its share of a month's),
/// where QLNet applies one month's SMM a period; the monthly yield's bracket widens past QLNet's [0, 1]% a month when
/// that doesn't hold the root; and the yields refuse a pool fully paid off at the settlement, where QLNet's throw on
/// its stale notional, and price the settlement date's flows as paid whatever the context's settings, where QLNet's
/// read them. <see cref="MonthlyYield"/> keeps QLNet's whole periods with no delay;
/// <see cref="Yield(double, int, EvaluationContext, Date?)"/> and <see cref="Price"/> are SIFMA's standard yield and
/// price.
/// </para>
/// </remarks>
public sealed class MBSFixedRateBond : Bond
{
    private readonly IPrepayModel? prepayModel;
    private readonly Period age;
    private readonly Schedule schedule;
    private readonly double passThroughRate;

    /// <summary>Initializes a new instance of the <see cref="MBSFixedRateBond"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">
    /// The calendar of the settlement and the payment dates, or <see langword="null"/>, QuantLib's empty calendar,
    /// which takes only an <see cref="BusinessDayConvention.Unadjusted"/> payment convention.
    /// </param>
    /// <param name="faceAmount">The pool's balance at the start date.</param>
    /// <param name="startDate">The start date of the bond's first period.</param>
    /// <param name="bondTenor">The remaining term, the bond's length.</param>
    /// <param name="originalLength">The loans' original term: the pool's age is it less the remaining term.</param>
    /// <param name="sinkingFrequency">The payment frequency.</param>
    /// <param name="wacRate">The weighted-average coupon of the loans, which sets the scheduled principal.</param>
    /// <param name="passThroughRate">The rate paid to the holder on the pool's balance.</param>
    /// <param name="accrualDayCounter">The pass-through coupons' day counter.</param>
    /// <param name="prepayModel">The prepayment model, or <see langword="null"/> for none.</param>
    /// <param name="paymentConvention">The convention adjusting the payment dates on the calendar.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="accrualDayCounter"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or <paramref name="paymentConvention"/> isn't a member of its
    /// type.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The frequency or the tenor isn't one the level-payment schedule takes (<see
    /// cref="AmortizingFixedRateBondFunctions.SinkingNotionals"/>).
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The calendar is <see langword="null"/> and the payment convention adjusts.
    /// </exception>
    public MBSFixedRateBond(
        int settlementDays,
        Calendar? calendar,
        double faceAmount,
        Date startDate,
        Period bondTenor,
        Period originalLength,
        Frequency sinkingFrequency,
        double wacRate,
        double passThroughRate,
        DayCounter accrualDayCounter,
        IPrepayModel? prepayModel,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        Date? issueDate = null)
        : base(settlementDays, calendar, issueDate)
    {
        ArgumentNullException.ThrowIfNull(accrualDayCounter);
        if (!Enum.IsDefined(paymentConvention))
        {
            throw new ArgumentOutOfRangeException(
                nameof(paymentConvention), paymentConvention, "not a business-day convention");
        }

        this.prepayModel = prepayModel;
        this.passThroughRate = passThroughRate;
        age = originalLength - bondTenor;
        Frequency = sinkingFrequency;
        DayCounter = accrualDayCounter;
        schedule = AmortizingFixedRateBondFunctions.SinkingSchedule(startDate, bondTenor, sinkingFrequency, calendar);
        ImmutableArray<double> scheduled =
            AmortizingFixedRateBondFunctions.SinkingNotionals(bondTenor, sinkingFrequency, wacRate, faceAmount);

        // QLNet's calcBondFactor: the level-payment schedule's notionals over the first, 1 at the start.
        var factors = new double[scheduled.Length];
        for (int i = 0; i < factors.Length; i++)
        {
            factors[i] = i == 0 ? 1 : scheduled[i] / scheduled[0];
        }

        BondFactors = [.. factors];

        var rate = new InterestRate(passThroughRate, accrualDayCounter, Compounding.Simple, Frequency.Annual);
        var flows = ImmutableArray.CreateBuilder<CashFlow>((schedule.Size - 1) * 3);
        double notional = scheduled[0];
        for (int i = 0; i < schedule.Size - 1; ++i)
        {
            double currentNotional = notional;
            double smm = PeriodSMM(i);
            double prepay = (notional * factors[i + 1]) / factors[i] * smm;
            double actualamort = currentNotional * (1 - (factors[i + 1] / factors[i]));
            notional = currentNotional - actualamort - prepay;

            // The coupon, then the scheduled principal, then the prepayment: a bond's redemptions follow its coupons
            // on a date, where QLNet adds the prepayment, the scheduled principal and the coupon.
            Date paymentDate = PaymentDate(calendar, schedule[i + 1], paymentConvention);
            flows.Add(new FixedRateCoupon(paymentDate, currentNotional, rate, schedule[i], schedule[i + 1]));
            flows.Add(new AmortizingPayment(actualamort, paymentDate));
            flows.Add(new VoluntaryPrepay(prepay, paymentDate));
        }

        cashflows = flows.MoveToImmutable();
        maturityDate = schedule[schedule.Size - 1];

        // The pool's notional schedule and its principal, where QLNet keeps the level-payment schedule's.
        CalculateNotionalsFromCashflows();
        redemptions = [.. cashflows.Where(c => c is not Coupon)];
    }

    /// <summary>
    /// Gets the expected cash flows: each period's pass-through coupon, scheduled principal and prepayment, the bond's
    /// own leg.
    /// </summary>
    public ImmutableArray<CashFlow> ExpectedCashflows => cashflows;

    /// <summary>
    /// Gets the level-payment schedule's factors: its notionals over the first, one a date of the bond's schedule, the
    /// last 0.
    /// </summary>
    public ImmutableArray<double> BondFactors { get; }

    /// <summary>Gets the payment frequency.</summary>
    public Frequency Frequency { get; }

    /// <summary>Gets the pass-through coupons' day counter.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>
    /// The prepayment model's single monthly mortality at a date plus the pool's age, QLNet's function. It isn't always
    /// a period's SMM: the flows read the model at the start's anniversaries, the start plus the age plus the months
    /// before the period, which a schedule date plus the age misses by a month from a start after the 28th, and a
    /// period of another frequency survives the product of its months' prepayments.
    /// </summary>
    /// <param name="d">The date, before the pool's age is added.</param>
    /// <returns>The model's SMM at <paramref name="d"/> plus the pool's age; 0 with no model.</returns>
    public double SMM(Date d) => prepayModel is null ? 0 : prepayModel.GetSMM(d + age);

    /// <summary>
    /// The monthly yield at par, QLNet's: the rate per month at which the expected flows after the settlement date,
    /// discounted by whole periods per payment date with no delay, are worth the pool's balance at the settlement date.
    /// </summary>
    /// <remarks>
    /// The solve is QLNet's: Brent from a guess of 0.02% a month over [0, 1]% a month, to 1e-10, in 100 evaluations;
    /// where that range doesn't bracket the root, Brent's step search from the same guess, above −100% a month. On a
    /// pool of another frequency the root is a yield per period, and the result its monthly equivalent. The settlement
    /// date's flows count as paid, as the balance takes them, whatever the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> and
    /// <see cref="EvaluationContext.IncludeTodaysCashFlows"/>.
    /// </remarks>
    /// <param name="context">The evaluation context, whose evaluation date sets the settlement date.</param>
    /// <returns>The yield per month, a fraction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The pool is fully paid off at the settlement date.</exception>
    /// <exception cref="NanoQLException">The solver finds no root.</exception>
    public double MonthlyYield(EvaluationContext context)
    {
        double periodYield = PeriodYield(context);
        return Frequency == Frequency.Monthly
            ? periodYield
            : Math.Pow(1 + periodYield, (int)Frequency / 12.0) - 1;
    }

    /// <summary>
    /// The bond-equivalent yield of <see cref="MonthlyYield"/>, its semi-annual equivalent, as SIFMA converts a monthly
    /// yield: <c>2 ((1 + y)^6 − 1)</c>, or for a pool of another frequency <c>2 ((1 + y)^(f/2) − 1)</c> on its yield
    /// per period.
    /// </summary>
    /// <param name="context">The evaluation context, whose evaluation date sets the settlement date.</param>
    /// <returns>The yield, a fraction a year, compounded semi-annually.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The pool is fully paid off at the settlement date.</exception>
    /// <exception cref="NanoQLException">The solver finds no root.</exception>
    public double BondEquivalentYield(EvaluationContext context)
    {
        if (Frequency == Frequency.Monthly)
        {
            return 2 * (Math.Pow(1 + MonthlyYield(context), 6) - 1);
        }

        return 2 * (Math.Pow(1 + PeriodYield(context), (int)Frequency / 2.0) - 1);
    }

    /// <summary>
    /// SIFMA's standard yield (Uniform Practices, Chapter SF, SF-48): the bond-equivalent yield Y at which the expected
    /// flows due after the settlement, each discounted by <c>(1 + Y/2)^(−2T)</c>, are worth the dollar price.
    /// </summary>
    /// <remarks>
    /// The dollar price is the clean price per 100 of the balance at the start of the period holding the settlement,
    /// times that balance, plus the interest accrued at the pass-through rate from that period's start (SF-46). T is in
    /// years on SIFMA's 30/360 (SF-44), from the settlement to each period's unadjusted end, plus the delay days, so
    /// the payment convention doesn't enter; SIFMA's yield is semi-annual whatever the pool's frequency.
    /// </remarks>
    /// <param name="cleanPrice">The price per 100 of the current balance, without accrued interest.</param>
    /// <param name="delayDays">The actual delay, in days from each period's end to its payment (SF-44).</param>
    /// <param name="context">The evaluation context, whose evaluation date sets the settlement date.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The yield, a fraction a year, compounded semi-annually.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delayDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">The pool is fully paid off at the settlement date.</exception>
    /// <exception cref="NanoQLException">The solver finds no root.</exception>
    public double Yield(double cleanPrice, int delayDays, EvaluationContext context, Date? settlementDate = null)
    {
        Sifma s = SifmaFlows(delayDays, context, settlementDate);
        double dirty = (cleanPrice / 100 * s.Balance) + s.Accrued;
        var solver = new Brent().SetMaxEvaluations(100).SetLowerBound(-2.0);
        return solver.Solve(y => s.Value(y) - dirty, 1.0e-12, passThroughRate, 0.01);
    }

    /// <summary>
    /// SIFMA's standard price at a yield (Uniform Practices, Chapter SF, SF-48): the expected flows' value at the
    /// bond-equivalent yield, less the accrued interest, per 100 of the balance at the start of the period holding the
    /// settlement, as <see cref="Yield(double, int, EvaluationContext, Date?)"/> reads them.
    /// </summary>
    /// <param name="yield">The bond-equivalent yield, a fraction a year, compounded semi-annually.</param>
    /// <param name="delayDays">The actual delay, in days from each period's end to its payment (SF-44).</param>
    /// <param name="context">The evaluation context, whose evaluation date sets the settlement date.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The clean price per 100 of the current balance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delayDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">The pool is fully paid off at the settlement date.</exception>
    public double Price(double yield, int delayDays, EvaluationContext context, Date? settlementDate = null)
    {
        Sifma s = SifmaFlows(delayDays, context, settlementDate);
        return (s.Value(yield) - s.Accrued) / s.Balance * 100;
    }

    /// <summary>
    /// SIFMA's 30/360 day count (SF-44): a February end starts as the 30th, a 31st start as the 30th, and a 31st end
    /// as the 30th after a start on the 30th; never negative.
    /// </summary>
    internal static int Sf44Days(Date d1, Date d2)
    {
        int dd1 = d1.DayOfMonth, dd2 = d2.DayOfMonth;
        if (d1.Month == Month.February && Date.IsEndOfMonth(d1))
        {
            dd1 = 30;
        }

        if (dd1 == 31)
        {
            dd1 = 30;
        }

        if (dd1 == 30 && dd2 == 31)
        {
            dd2 = 30;
        }

        return Math.Max((360 * (d2.Year - d1.Year)) + (30 * (d2.Month - d1.Month)) + (dd2 - dd1), 0);
    }

    /// <summary>
    /// The additive count of months a period spans: each calendar month's share of it, its days in the month over the
    /// month's days, so that consecutive periods' counts add up to the span's.
    /// </summary>
    internal static double MonthsSpanned(Date start, Date end)
    {
        double months = 0;
        Date d = start;
        while (d < end)
        {
            Date next = Date.EndOfMonth(d) + 1;
            Date until = next < end ? next : end;
            months += (double)(until - d) / (Date.EndOfMonth(d).DayOfMonth);
            d = until;
        }

        return months;
    }

    // A period's payment date: its end adjusted on the calendar, refused on QuantLib's empty calendar unless the
    // convention is Unadjusted.
    private static Date PaymentDate(Calendar? calendar, Date end, BusinessDayConvention convention)
    {
        if (calendar is not null)
        {
            return calendar.Adjust(end, convention);
        }

        return convention == BusinessDayConvention.Unadjusted
            ? end
            : throw new NanoQLException("no calendar implementation provided");
    }

    // The SMM of period i: the model's on a monthly pool; otherwise one less the product of its months' survival,
    // each month's SMM at its own start, from the period's first month; a period counted in weeks or days survives its
    // start's monthly survival to the power of the months it spans. A month counted in months is read at the pool's
    // start plus the pool's age and the months before it, one anniversary of the start: QLNet reads a schedule date
    // plus the age, the same date from a start before the 29th, but from a later start a backward schedule's 30th or a
    // February end, which a model counting SF-6's months would read a month short.
    private double PeriodSMM(int i)
    {
        if (prepayModel is null)
        {
            return 0;
        }

        var tenor = new Period(Frequency);
        if (tenor.Units is TimeUnit.Months or TimeUnit.Years && age.Units is TimeUnit.Months or TimeUnit.Years)
        {
            int months = tenor.Units == TimeUnit.Years ? tenor.Length * 12 : tenor.Length;
            int first = (age.Units == TimeUnit.Years ? age.Length * 12 : age.Length) + (i * months);
            if (Frequency == Frequency.Monthly)
            {
                return prepayModel.GetSMM(schedule[0] + new Period(first, TimeUnit.Months));
            }

            double survival = 1;
            for (int k = 0; k < months; k++)
            {
                survival *= 1 - prepayModel.GetSMM(schedule[0] + new Period(first + k, TimeUnit.Months));
            }

            return 1 - survival;
        }

        Date start = schedule[i] + age;
        if (tenor.Units is TimeUnit.Months or TimeUnit.Years)
        {
            int months = tenor.Units == TimeUnit.Years ? tenor.Length * 12 : tenor.Length;
            double survival = 1;
            for (int k = 0; k < months; k++)
            {
                survival *= 1 - prepayModel.GetSMM(start + new Period(k, TimeUnit.Months));
            }

            return 1 - survival;
        }

        return 1 - Math.Pow(1 - prepayModel.GetSMM(start), MonthsSpanned(schedule[i], schedule[i + 1]));
    }

    // The root of QLNet's PVDifference at the pool's balance: the yield per period, a fraction. The settlement date's
    // flows count as paid whatever the context's settings, as the balance takes them (Bond.Notional) and QuantLib's
    // bond functions do; QLNet reads the settings, so its yields move with them there.
    private double PeriodYield(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date settlement = SettlementDate(context);
        double balance = TradableBalance(context, settlement, nameof(context));
        ImmutableArray<CashFlow> leg = cashflows;
        EvaluationContext paid = context with { IncludeReferenceDateEvents = false, IncludeTodaysCashFlows = false };
        double F(double y) => MBSFixedRateBondFunctions.PVDifference(balance, leg, y, settlement, paid);

        var solver = new Brent().SetMaxEvaluations(100);
        double root = F(0.0) * F(1.0) <= 0
            ? solver.Solve(F, 1.0e-10, 0.02, 0.0, 1.0)
            : solver.SetLowerBound(-100.0).Solve(F, 1.0e-10, 0.02, 0.01);
        return root / 100;
    }

    // The pool's balance at the settlement, refused when it is 0, with BondFunctions' text.
    private double TradableBalance(EvaluationContext context, Date settlement, string paramName)
    {
        if (!BondFunctions.IsTradable(this, context, settlement))
        {
            throw new ArgumentException($"non tradable at {settlement} (maturity being {MaturityDate()})", paramName);
        }

        return Notional(context, settlement);
    }

    // SIFMA's flows (SF-46, SF-48): the balance at the start of the period holding the settlement, the accrued
    // interest from that start, and each later period's flows with its time.
    private Sifma SifmaFlows(int delayDays, EvaluationContext context, Date? settlementDate)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(delayDays);
        Date settlement = settlementDate ?? SettlementDate(context);
        TradableBalance(context, settlement, nameof(settlementDate));

        int periods = schedule.Size - 1;
        int holding = 0;
        while (holding < periods - 1 && schedule[holding + 1] <= settlement)
        {
            holding++;
        }

        double balance = ((Coupon)cashflows[holding * 3]).ContractNominal;
        double accrued = balance * passThroughRate * Sf44Days(schedule[holding], settlement) / 360;
        var amounts = new List<double>();
        var times = new List<double>();
        for (int i = 0; i < periods; i++)
        {
            if (schedule[i + 1] <= settlement)
            {
                continue;
            }

            amounts.Add(cashflows[i * 3].Amount(context) + cashflows[(i * 3) + 1].Amount(context)
                + cashflows[(i * 3) + 2].Amount(context));
            times.Add((Sf44Days(settlement, schedule[i + 1]) + delayDays) / 360.0);
        }

        return new Sifma(balance, accrued, [.. amounts], [.. times]);
    }

    private readonly record struct Sifma(double Balance, double Accrued, double[] Amounts, double[] Times)
    {
        public double Value(double y)
        {
            double value = 0;
            for (int k = 0; k < Amounts.Length; k++)
            {
                value += Amounts[k] / Math.Pow(1 + (y / 2), 2 * Times[k]);
            }

            return value;
        }
    }
}
