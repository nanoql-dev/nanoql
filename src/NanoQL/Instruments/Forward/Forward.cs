// SPDX-FileCopyrightText: Copyright (C) 2006 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/forward.hpp ql/instruments/forward.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A forward contract on an underlying, whose derived classes give its spot value and income: the forward value is
/// the spot value less the income, over the discount at the maturity date.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's derived classes set the mutable spot value and income in their <c>performCalculations</c> before the
/// base's runs; here a derived class's calculation passes them to
/// <see cref="Calculate(double, double, EvaluationContext, Date?)"/>, which gives the results. The discount curve
/// discounts the contract's cash flows and gives the forward value; the income curve, which a derived class sets,
/// discounts the underlying's income.
/// </para>
/// <para>
/// QuantLib's class still needs to be rigorously tested, its header says.
/// </para>
/// </remarks>
public abstract class Forward : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="Forward"/> class.</summary>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="businessDayConvention">The business-day convention, which adjusts the maturity date.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="payoff">The payoff.</param>
    /// <param name="valueDate">The value date, from which the contract accrues.</param>
    /// <param name="maturityDate">The maturity date, the underlying's delivery date, adjusted on the calendar.</param>
    /// <param name="discountCurve">The curve relevant to the contract (a repo curve, for instance), or
    /// <see langword="null"/>, QuantLib's empty handle, which can't price.</param>
    /// <param name="incomeDiscountCurve">The curve discounting the underlying's income, or
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/>, <paramref name="calendar"/> or
    /// <paramref name="payoff"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or the convention
    /// isn't a member of its enum.</exception>
    protected Forward(
        DayCounter dayCounter,
        Calendar calendar,
        BusinessDayConvention businessDayConvention,
        int settlementDays,
        ForwardTypePayoff payoff,
        Date valueDate,
        Date maturityDate,
        YieldTermStructure? discountCurve,
        YieldTermStructure? incomeDiscountCurve)
    {
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        EnumArguments.Check(businessDayConvention, nameof(businessDayConvention));
        DayCounter = dayCounter;
        Calendar = calendar;
        BusinessDayConvention = businessDayConvention;
        SettlementDays = settlementDays;
        Payoff = payoff;
        ValueDate = valueDate;
        MaturityDate = calendar.Adjust(maturityDate, businessDayConvention);
        DiscountCurve = discountCurve;
        IncomeDiscountCurve = incomeDiscountCurve;
    }

    /// <summary>Gets the day counter.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>Gets the calendar.</summary>
    public Calendar Calendar { get; }

    /// <summary>Gets the business-day convention.</summary>
    public BusinessDayConvention BusinessDayConvention { get; }

    /// <summary>Gets the settlement days.</summary>
    public int SettlementDays { get; }

    /// <summary>Gets the payoff.</summary>
    public ForwardTypePayoff Payoff { get; }

    /// <summary>Gets the value date, from which the contract accrues.</summary>
    public Date ValueDate { get; }

    /// <summary>Gets the maturity date, adjusted.</summary>
    public Date MaturityDate { get; }

    /// <summary>Gets the curve relevant to the contract, or <see langword="null"/> for none.</summary>
    public YieldTermStructure? DiscountCurve { get; }

    /// <summary>Gets the curve discounting the underlying's income, or <see langword="null"/> for none.</summary>
    public YieldTermStructure? IncomeDiscountCurve { get; }

    /// <summary>The settlement date: the settlement days after the evaluation date, but not before the value
    /// date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The settlement date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public virtual Date SettlementDate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date d = Calendar.Advance(context.EvaluationDate, SettlementDays, TimeUnit.Days);
        return d > ValueDate ? d : ValueDate;
    }

    /// <inheritdoc/>
    /// <remarks>A forward expires once its maturity date has occurred at its settlement date.</remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(MaturityDate).HasOccurred(context, SettlementDate(context));
    }

    /// <summary>The underlying's income between the settlement and the maturity dates, discounted on a curve.</summary>
    /// <param name="incomeDiscountCurve">The curve discounting the income, or <see langword="null"/> for none.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The income.</returns>
    public abstract double SpotIncome(YieldTermStructure? incomeDiscountCurve, EvaluationContext context);

    /// <summary>
    /// The simple yield implied by a spot and a forward value, net of the underlying's income on the income curve:
    /// the rate that compounds the spot value less the income to the forward value from a settlement date to the
    /// maturity date. For a repo at time 0 it reproduces the spot repo rate; for an FRA, the zero rate at its maturity.
    /// </summary>
    /// <param name="underlyingSpotValue">The underlying's spot value.</param>
    /// <param name="forwardValue">The forward value, the strike for the current yield.</param>
    /// <param name="settlementDate">The date the yield runs from.</param>
    /// <param name="compoundingConvention">The yield's compounding, annual where it compounds.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="context">The evaluation context, which the income reads.</param>
    /// <returns>The yield.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> or <paramref name="context"/> is
    /// null.</exception>
    public InterestRate ImpliedYield(
        double underlyingSpotValue,
        double forwardValue,
        Date settlementDate,
        Compounding compoundingConvention,
        DayCounter dayCounter,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(context);
        double t = dayCounter.YearFraction(settlementDate, MaturityDate);
        double compoundingFactor = forwardValue / (underlyingSpotValue - SpotIncome(IncomeDiscountCurve, context));
        return InterestRate.ImpliedRate(compoundingFactor, dayCounter, compoundingConvention, Frequency.Annual, t);
    }

    /// <inheritdoc/>
    public override ForwardResults SetupExpired()
    {
        InstrumentResults expired = base.SetupExpired();
        return new ForwardResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The results aren't a forward's.</exception>
    public override ForwardResults FetchResults(InstrumentResults results)
    {
        InstrumentResults fetched = base.FetchResults(results);
        if (fetched is not ForwardResults r)
        {
            throw new ArgumentException("wrong result type", nameof(results));
        }

        return r;
    }

    /// <summary>
    /// QuantLib's <c>Forward::performCalculations</c> on a derived class's spot value and income: the forward value,
    /// and the NPV, the payoff on it discounted from the maturity date.
    /// </summary>
    /// <param name="spotValue">The underlying's spot value.</param>
    /// <param name="spotIncome">The underlying's income, discounted to the discount curve's reference date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="spotDate">The date the spot value is a value at, carried back to the discount curve's reference date
    /// before the income is netted, or <see langword="null"/> for the reference date, QuantLib's reading.</param>
    /// <returns>The results.</returns>
    /// <exception cref="NanoQLException">There is no discount curve, QuantLib's "null term structure set to
    /// Forward".</exception>
    protected ForwardResults Calculate(
        double spotValue, double spotIncome, EvaluationContext context, Date? spotDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        YieldTermStructure discount = DiscountCurve ?? throw new NanoQLException("null term structure set to Forward");
        double maturityDiscount = discount.Discount(MaturityDate);
        double spotAtReference = spotDate is Date d ? spotValue * discount.Discount(d) : spotValue;
        double forwardValue = (spotAtReference - spotIncome) / maturityDiscount;
        return FetchResults(new ForwardResults
        {
            NPV = Payoff.Evaluate(forwardValue) * maturityDiscount,
            SpotValue = spotValue,
            SpotIncome = spotIncome,
            ForwardValue = forwardValue,
        });
    }
}
