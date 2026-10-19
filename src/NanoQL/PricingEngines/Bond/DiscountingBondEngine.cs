// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/bond/discountingbondengine.hpp ql/pricingengines/bond/discountingbondengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The discounting engine for bonds: it discounts the future cash flows to the curve's reference date, its NPV, and
/// to the bond's settlement date, its settlement value.
/// </summary>
/// <remarks>
/// A static class: <c>Calculate</c> takes the engine's inputs and returns the results, in place of QuantLib's engine
/// object, whose <c>discountCurve()</c> accessor has no instance to belong to. A cash flow on the settlement date
/// never counts in the settlement value, whatever the NPV counts. A bond whose engines take other arguments, such as a
/// callable bond, is refused after the expiry check, as QuantLib's refuses it when it sets up the engine's arguments.
/// </remarks>
public static class DiscountingBondEngine
{
    /// <summary>Prices a bond.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context, which gives the bond's settlement date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="includeSettlementDateFlows">
    /// Whether cash flows on the curve's reference date count in the NPV; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The bond's results: the NPV at the curve's reference date, its valuation date, and the settlement value; no error
    /// estimate. The expired results when the bond has expired.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The settlement date is before the curve's reference date, which the curve refuses.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The bond takes other engines' arguments, or its settlement date can't be computed on QuantLib's empty calendar.
    /// </exception>
    public static BondResults Calculate(
        Bond bond,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        bool? includeSettlementDateFlows = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        if (bond.IsExpired(context))
        {
            return bond.SetupExpired();
        }

        // QuantLib's setupArguments: the refusal of a bond with other arguments, then the settlement date
        bond.CheckPlainBondArguments();
        Date settlementDate = bond.SettlementDate(context);

        bond.Validate();

        Date valuationDate = discountCurve.ReferenceDate;

        bool includeRefDateFlows = includeSettlementDateFlows ?? context.IncludeReferenceDateEvents;

        double value = CashFlows.Npv(
            bond.Cashflows, discountCurve, context, includeRefDateFlows, valuationDate, valuationDate);

        // a bond's cashflow on settlement date is never taken into
        // account, so we might have to play it safe and recalculate
        double settlementValue;
        if (!includeRefDateFlows && valuationDate == settlementDate)
        {
            // same parameters as above, we can avoid another call
            settlementValue = value;
        }
        else
        {
            // no such luck
            settlementValue = CashFlows.Npv(bond.Cashflows, discountCurve, context, false, settlementDate, settlementDate);
        }

        var results = new BondResults
        {
            NPV = value,
            ErrorEstimate = null,
            ValuationDate = valuationDate,
            SettlementValue = settlementValue,
        };
        return bond.FetchResults(results);
    }
}
