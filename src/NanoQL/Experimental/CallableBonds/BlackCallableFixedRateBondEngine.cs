// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/blackcallablebondengine.hpp ql/experimental/callablebonds/blackcallablebondengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The Black-formula engine for callable fixed-rate bonds with a single call or put date: the embedded European
/// option follows the Black "European bond option" treatment of Hull, fourth edition, chapter 20.
/// </summary>
/// <remarks>
/// <para>
/// A static class. The volatility is the quoted forward yield volatility, not a price volatility: it is turned into a
/// forward price volatility through the bond's forward yield and modified duration at the exercise date. QuantLib's
/// quote constructor builds a constant volatility on the null calendar with no settlement days, whose reference date
/// follows the evaluation date: its overload builds that volatility for the context of each call. QuantLib notes the
/// engine as yet untested.
/// </para>
/// <para>
/// The engine reads no spread, so the OAS family can't bracket a spread through it, as QuantLib's can't.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class BlackCallableFixedRateBondEngine
{
    /// <summary>Prices a callable bond with a constant forward yield volatility.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">
    /// The evaluation context, which gives the bond's settlement date and the volatility's reference date.
    /// </param>
    /// <param name="fwdYieldVol">The forward yield volatility.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>
    /// The bond's results: the NPV at the curve's reference date and the settlement value; the expired results when the
    /// bond has expired.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The bond's arguments are inconsistent; the bond doesn't have exactly one call or put date, or it has occurred;
    /// or the forward yield can't be solved for.
    /// </exception>
    public static CallableBondResults Calculate(
        CallableBond bond, EvaluationContext context, double fwdYieldVol, YieldTermStructure discountCurve)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        CallableBondConstantVolatility volatility =
            CallableBondConstantVolatility.Create(0, new NullCalendar(), fwdYieldVol, new Actual365Fixed(), context);
        return Calculate(bond, context, volatility, discountCurve);
    }

    /// <summary>Prices a callable bond with a forward yield volatility structure.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context, which gives the bond's settlement date.</param>
    /// <param name="yieldVolStructure">The forward yield volatilities.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>
    /// The bond's results: the NPV at the curve's reference date and the settlement value; the expired results when the
    /// bond has expired.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The bond's arguments are inconsistent; the bond doesn't have exactly one call or put date, or it has occurred;
    /// the forward yield can't be solved for; or the volatility is queried outside its range.
    /// </exception>
    public static CallableBondResults Calculate(
        CallableBond bond,
        EvaluationContext context,
        CallableBondVolatilityStructure yieldVolStructure,
        YieldTermStructure discountCurve)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(yieldVolStructure);
        ArgumentNullException.ThrowIfNull(discountCurve);
        if (bond.IsExpired(context))
        {
            return bond.SetupExpired();
        }

        CallableBondArguments arguments = bond.SetupArguments(context);
        arguments.Validate();

        (double value, double settlementValue) = Value(arguments, yieldVolStructure, discountCurve, context);
        var results = new CallableBondResults
        {
            NPV = value,
            ErrorEstimate = null,
            ValuationDate = null,
            SettlementValue = settlementValue,
        };
        return bond.FetchResults(results);
    }

    /// <summary>QuantLib's <c>calculate</c>: the bond's value at the curve's reference date and at its settlement date.</summary>
    /// <param name="arguments">The bond's arguments.</param>
    /// <param name="volatility">The forward yield volatilities.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context, which the cash flows read.</param>
    /// <returns>The value and the settlement value.</returns>
    internal static (double Value, double SettlementValue) Value(
        CallableBondArguments arguments,
        CallableBondVolatilityStructure volatility,
        YieldTermStructure discountCurve,
        EvaluationContext context)
    {
        // validate args for Black engine
        if (arguments.PutCallSchedule.Length != 1)
        {
            throw new NanoQLException("Must have exactly one call/put date to use Black Engine");
        }

        Date settle = arguments.SettlementDate;

        // QuantLib reads the first live callability unchecked: past the end when the one callability has occurred.
        if (arguments.CallabilityDates.IsEmpty)
        {
            throw new NanoQLException("must have exercise Date >= settlement Date");
        }

        Date exerciseDate = arguments.CallabilityDates[0];
        if (!(exerciseDate >= settle))
        {
            throw new NanoQLException("must have exercise Date >= settlement Date");
        }

        ImmutableArray<CashFlow> fixedLeg = arguments.Cashflows;

        double value = CashFlows.Npv(fixedLeg, discountCurve, context, false, settle);

        double npv = CashFlows.Npv(fixedLeg, discountCurve, context, false, discountCurve.ReferenceDate);

        double fwdCashPrice = (value - SpotIncome(arguments, discountCurve, context)) /
                              discountCurve.Discount(exerciseDate);

        double cashStrike = arguments.CallabilityPrices[0] * arguments.FaceAmount / 100.0;

        OptionType type = arguments.PutCallSchedule[0].Type == CallabilityType.Call ? OptionType.Call : OptionType.Put;

        double priceVol = ForwardPriceVolatility(arguments, volatility, discountCurve, context);

        double exerciseTime = volatility.DayCounter.YearFraction(volatility.ReferenceDate, exerciseDate);

        double discount = discountCurve.Discount(exerciseDate);
        double discountToSettlement = discount / discountCurve.Discount(settle);

        double embeddedOptionValue =
            BlackFormulaFunctions.BlackFormula(type, cashStrike, fwdCashPrice, priceVol * Math.Sqrt(exerciseTime));

        if (type == OptionType.Call)
        {
            return (npv - (embeddedOptionValue * discount), value - (embeddedOptionValue * discountToSettlement));
        }
        else
        {
            return (npv + (embeddedOptionValue * discount), value + (embeddedOptionValue * discountToSettlement));
        }
    }

    // The present value of the coupons paid during the life of the option, at the settlement date.
    private static double SpotIncome(
        CallableBondArguments arguments, YieldTermStructure discountCurve, EvaluationContext context)
    {
        // settle date of embedded option assumed same as that of bond
        Date settlement = arguments.SettlementDate;
        ImmutableArray<CashFlow> cf = arguments.Cashflows;
        Date optionMaturity = arguments.PutCallSchedule[0].Date;

        /* the following assumes
           1. cashflows are in ascending order !
           2. income = coupons paid between settlementDate() and put/call date
        */
        double income = 0.0;
        for (int i = 0; i < cf.Length - 1; ++i)
        {
            if (!cf[i].HasOccurred(context, settlement, false))
            {
                if (cf[i].HasOccurred(context, optionMaturity, false))
                {
                    income += cf[i].Amount(context) * discountCurve.Discount(cf[i].Date);
                }
                else
                {
                    break;
                }
            }
        }

        return income / discountCurve.Discount(settlement);
    }

    // Converts the yield volatility into a forward price volatility.
    private static double ForwardPriceVolatility(
        CallableBondArguments arguments,
        CallableBondVolatilityStructure volatility,
        YieldTermStructure discountCurve,
        EvaluationContext context)
    {
        Date bondMaturity = arguments.RedemptionDate;
        Date exerciseDate = arguments.CallabilityDates[0];
        ImmutableArray<CashFlow> fixedLeg = arguments.Cashflows;

        // value of bond cash flows at option maturity
        double fwdNpv = CashFlows.Npv(fixedLeg, discountCurve, context, false, exerciseDate);

        // QuantLib's empty day counter refuses its first year fraction, in the yield's solver.
        DayCounter dayCounter = arguments.PaymentDayCounter
            ?? throw new NanoQLException("no day counter implementation provided");
        Frequency frequency = arguments.Frequency;

        // adjust if zero coupon bond (see also bond.cpp)
        if (frequency == Frequency.NoFrequency || frequency == Frequency.Once)
        {
            frequency = Frequency.Annual;
        }

        double fwdYtm = CashFlows.Yield(
            fixedLeg, fwdNpv, dayCounter, Compounding.Compounded, frequency, context, false, exerciseDate);

        var fwdRate = new InterestRate(fwdYtm, dayCounter, Compounding.Compounded, frequency);

        double fwdDur = CashFlows.Duration(fixedLeg, fwdRate, DurationType.Modified, context, false, exerciseDate);

        double cashStrike = arguments.CallabilityPrices[0] * arguments.FaceAmount / 100.0;
        DayCounter volatilityDayCounter = volatility.DayCounter;
        Date referenceDate = volatility.ReferenceDate;
        double exerciseTime = volatilityDayCounter.YearFraction(referenceDate, exerciseDate);
        double maturityTime = volatilityDayCounter.YearFraction(referenceDate, bondMaturity);
        double yieldVol = volatility.Volatility(exerciseTime, maturityTime - exerciseTime, cashStrike);
        double fwdPriceVol = yieldVol * fwdDur * fwdYtm;
        return fwdPriceVol;
    }
}
