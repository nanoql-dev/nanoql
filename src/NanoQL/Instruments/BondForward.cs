// SPDX-FileCopyrightText: Copyright (C) 2006 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2022 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bondforward.hpp ql/instruments/bondforward.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A forward contract on a bond: the bond's dirty price at spot, less its coupons paid before delivery, carried to the
/// delivery date; a repo, for instance, at its repurchase price.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's <c>spotValue()</c> is the bond's dirty price on the bond's own engine; here
/// <see cref="Calculate(EvaluationContext, Func{Bond, EvaluationContext, BondResults})"/> takes the bond's engine, and
/// <see cref="CleanForwardPrice"/> and <see cref="ForwardPrice"/> read its results.
/// </para>
/// <para>
/// The income, the coupons paid after the settlement date and up to the delivery date discounted on the income curve,
/// is per 100 of the bond's notional at its settlement date, as the dirty price is, where QuantLib's sums the coupons'
/// amounts on the bond's face (<c>bondforward.cpp:59-83</c>), so on a face other than 100 delivering after a coupon its
/// forward value mixes the two. The forward value and the prices are then per 100.
/// </para>
/// <para>
/// The spot value, the bond's dirty price, is a value at the bond's settlement date: it is carried back to the discount
/// curve's reference date before the income, discounted to that date, is netted and the rest carried to delivery, where
/// QuantLib's grows the settlement-date price from the reference date (<c>bondforward.cpp:86-87</c>,
/// <c>forward.cpp:61-65</c>), so for a bond that doesn't settle on the curve's reference date its forward is high by
/// 1/P(ref, settlement). On a bond settling on the reference date the two agree. The clean forward price takes the
/// accrued amount at delivery on the forward's basis, per 100 of the notional at settlement, where QuantLib's subtracts
/// it per 100 of the notional at delivery (<c>bondforward.cpp:49-51</c>), another basis once the bond amortizes between
/// the two.
/// </para>
/// </remarks>
public sealed class BondForward : Forward
{
    /// <summary>Initializes a new instance of the <see cref="BondForward"/> class.</summary>
    /// <param name="valueDate">The value date, from which the contract accrues.</param>
    /// <param name="maturityDate">The delivery date, adjusted on the calendar.</param>
    /// <param name="type">The position.</param>
    /// <param name="strike">The strike, per 100 of notional.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="businessDayConvention">The business-day convention.</param>
    /// <param name="bond">The bond delivered.</param>
    /// <param name="discountCurve">The curve relevant to the contract, or <see langword="null"/>, QuantLib's empty
    /// handle, which can't price.</param>
    /// <param name="incomeDiscountCurve">The curve discounting the bond's coupons before delivery, or
    /// <see langword="null"/>, QuantLib's empty handle, which can't price an income.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/>, <paramref name="calendar"/> or
    /// <paramref name="bond"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or an enum argument
    /// isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException"><paramref name="strike"/> is negative.</exception>
    public BondForward(
        Date valueDate,
        Date maturityDate,
        PositionType type,
        double strike,
        int settlementDays,
        DayCounter dayCounter,
        Calendar calendar,
        BusinessDayConvention businessDayConvention,
        Bond bond,
        YieldTermStructure? discountCurve = null,
        YieldTermStructure? incomeDiscountCurve = null)
        : base(
            dayCounter,
            calendar,
            businessDayConvention,
            settlementDays,
            new ForwardTypePayoff(type, strike),
            valueDate,
            maturityDate,
            discountCurve,
            incomeDiscountCurve)
    {
        ArgumentNullException.ThrowIfNull(bond);
        Bond = bond;
    }

    /// <summary>Gets the bond delivered.</summary>
    public Bond Bond { get; }

    /// <summary>
    /// Prices the forward: QuantLib's <c>Instrument::calculate</c>, the bond's dirty price on its engine as the spot
    /// value and its coupons before delivery as the income.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="bondEngine">The bond's engine, which QuantLib's bond holds.</param>
    /// <returns>The results; an expired forward's NPV is zero and its other results <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">There is no discount curve, or no income curve where a coupon falls before
    /// delivery, or the bond's engine refuses.</exception>
    public ForwardResults Calculate(EvaluationContext context, Func<Bond, EvaluationContext, BondResults> bondEngine)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(bondEngine);
        if (IsExpired(context))
        {
            return SetupExpired();
        }

        Validate();
        double spotValue = SpotValue(context, bondEngine);
        double spotIncome = SpotIncome(IncomeDiscountCurve, context);
        return Calculate(spotValue, spotIncome, context, Bond.SettlementDate(context));
    }

    /// <summary>The bond's dirty price on its engine, per 100 of notional.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="bondEngine">The bond's engine.</param>
    /// <returns>The spot value.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public double SpotValue(EvaluationContext context, Func<Bond, EvaluationContext, BondResults> bondEngine)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(bondEngine);
        return Bond.DirtyPrice(bondEngine(Bond, context), context);
    }

    /// <inheritdoc/>
    /// <remarks>The coupons paid after the settlement date and up to the delivery date, per 100 of the bond's notional
    /// at its settlement date; the cash flows are taken in date order, as QuantLib's assume.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">A coupon falls in the window and there is no income curve.</exception>
    public override double SpotIncome(YieldTermStructure? incomeDiscountCurve, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double income = 0.0;
        Date settlement = SettlementDate(context);
        foreach (CashFlow cf in Bond.Cashflows)
        {
            if (!cf.HasOccurred(context, settlement, false))
            {
                if (cf.HasOccurred(context, MaturityDate, false))
                {
                    YieldTermStructure curve = incomeDiscountCurve
                        ?? throw new NanoQLException("empty Handle cannot be dereferenced");
                    income += cf.Amount(context) * curve.Discount(cf.Date);
                }
                else
                {
                    break;
                }
            }
        }

        if (income == 0.0)
        {
            return 0.0;
        }

        double notional = Bond.Notional(context, Bond.SettlementDate(context));
        return notional == 0.0 ? 0.0 : income * 100.0 / notional;
    }

    /// <summary>The clean forward price: the forward value less the bond's accrued amount at delivery, both per 100 of
    /// the bond's notional at its settlement date.</summary>
    /// <param name="results">The forward's results.</param>
    /// <param name="context">The evaluation context the results were computed under.</param>
    /// <returns>The clean forward price, per 100 of notional.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">The results hold no forward value, QuantLib's "result not
    /// available".</exception>
    public double CleanForwardPrice(ForwardResults results, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(context);

        // the accrued amount is per 100 of the notional at delivery: on the forward's basis once the bond amortizes
        // between settlement and delivery
        double accrued = Bond.AccruedAmount(context, MaturityDate);
        double settlementNotional = Bond.Notional(context, Bond.SettlementDate(context));
        double deliveryNotional = Bond.Notional(context, MaturityDate);
        if (deliveryNotional != settlementNotional && settlementNotional != 0.0)
        {
            accrued *= deliveryNotional / settlementNotional;
        }

        return ForwardPrice(results) - accrued;
    }

    /// <summary>The dirty forward price: the forward value.</summary>
    /// <param name="results">The forward's results.</param>
    /// <returns>The forward price, per 100 of notional.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    /// <exception cref="NanoQLException">The results hold no forward value, QuantLib's "result not
    /// available".</exception>
    public static double ForwardPrice(ForwardResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.ForwardValue ?? throw new NanoQLException("result not available");
    }
}
