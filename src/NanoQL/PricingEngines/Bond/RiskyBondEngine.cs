// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2021 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/bond/riskybondengine.hpp ql/pricingengines/bond/riskybondengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The risky engine for bonds: each future cash flow is weighted by its survival probability, and each coupon period
/// adds the recovery on its nominal, paid at the period's midpoint, weighted by its default probability.
/// </summary>
/// <remarks>
/// <para>
/// The engine prices a bond on a default-probability curve and a recovery rate: each cash flow's amount is weighted by
/// the probability of survival to its date and discounted; in each coupon period, from the later of the curve's
/// reference date and the leg's start, the coupon's nominal times the recovery rate is weighted by the probability of
/// default in the period and discounted from its midpoint. The settlement value counts the cash flows after the
/// settlement date, discounted to it.
/// </para>
/// <para>
/// A static class: <see cref="Calculate"/> takes the engine's inputs and returns the results, in place of QuantLib's
/// engine object, as <see cref="DiscountingBondEngine"/> does. An expired bond's results are
/// <see cref="Bond.SetupExpired"/>'s.
/// </para>
/// </remarks>
public static class RiskyBondEngine
{
    /// <summary>Prices a bond.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context, which gives the bond's settlement date.</param>
    /// <param name="defaultTS">The default-probability curve.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="yieldTS">The discount curve.</param>
    /// <returns>
    /// The bond's results: the NPV at the discount curve's reference date, its valuation date, and the settlement
    /// value; no error estimate. The expired results when the bond has expired.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The bond has no cash flows, or a curve refuses a date.</exception>
    /// <exception cref="NanoQLException">
    /// The bond takes other engines' arguments, or its settlement date can't be computed on QuantLib's empty calendar.
    /// </exception>
    public static BondResults Calculate(
        Bond bond,
        EvaluationContext context,
        DefaultProbabilityTermStructure defaultTS,
        double recoveryRate,
        YieldTermStructure yieldTS)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(defaultTS);
        ArgumentNullException.ThrowIfNull(yieldTS);
        if (bond.IsExpired(context))
        {
            return bond.SetupExpired();
        }

        // QuantLib's setupArguments: the refusal of a bond with other arguments, then the settlement date
        bond.CheckPlainBondArguments();
        Date settlementDate = bond.SettlementDate(context);

        bond.Validate();

        Date npvDate = yieldTS.ReferenceDate;
        Date startDate = CashFlows.StartDate(bond.Cashflows);
        Date d1 = npvDate > startDate ? npvDate : startDate;

        double npv = 0.0;
        double settlementValue = 0.0;
        foreach (CashFlow cf in bond.Cashflows)
        {
            Date d2 = cf.Date;
            if (d2 > npvDate)
            {
                double weightedCouponAmount = cf.Amount(context) * defaultTS.SurvivalProbability(d2);
                npv += weightedCouponAmount * yieldTS.Discount(d2);
                if (d2 > settlementDate)
                {
                    settlementValue += weightedCouponAmount * yieldTS.Discount(d2);
                }

                if (cf is Coupon coupon)
                {
                    Date defaultDate = d1 + ((d2 - d1) / 2);
                    double weightedRecovery = coupon.Nominal(context) * recoveryRate
                                              * (defaultTS.SurvivalProbability(d1) - defaultTS.SurvivalProbability(d2));
                    npv += weightedRecovery * yieldTS.Discount(defaultDate);
                    if (d2 > settlementDate)
                    {
                        settlementValue += weightedRecovery * yieldTS.Discount(defaultDate);
                    }

                    d1 = d2;
                }
            }
        }

        var results = new BondResults
        {
            NPV = npv,
            ErrorEstimate = null,
            SettlementValue = settlementValue / yieldTS.Discount(settlementDate),
            ValuationDate = npvDate,
        };
        return bond.FetchResults(results);
    }
}
