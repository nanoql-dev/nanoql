// SPDX-FileCopyrightText: Copyright (C) 2023 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/equitycashflow.hpp ql/cashflows/equitycashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The quanto pricer of an equity cash flow paid in another currency: the index's ratio forecast on its dividend curve
/// quanto-adjusted by the equity and exchange-rate volatilities and their correlation, struck at the index's fixing,
/// and discounted on the payment currency's curve.
/// </summary>
/// <remarks>
/// QuantLib's handles may be empty, and its <c>initialize</c> refuses them when the flow is priced; here a null input
/// is refused when the flow is priced, with QuantLib's texts. An index without a dividend curve takes QuantLib's flat
/// zero curve, built per call under the context.
/// </remarks>
public sealed class EquityQuantoCashFlowPricer : EquityCashFlowPricer
{
    private readonly YieldTermStructure? quantoCurrencyTermStructure;
    private readonly BlackVolTermStructure? equityVolatility;
    private readonly BlackVolTermStructure? fxVolatility;
    private readonly double? correlation;

    /// <summary>Initializes a new instance of the <see cref="EquityQuantoCashFlowPricer"/> class.</summary>
    /// <param name="quantoCurrencyTermStructure">The payment currency's curve, or <see langword="null"/>, QuantLib's
    /// empty handle.</param>
    /// <param name="equityVolatility">The equity's Black volatility, or <see langword="null"/>.</param>
    /// <param name="fxVolatility">The exchange rate's Black volatility, or <see langword="null"/>.</param>
    /// <param name="correlation">The correlation of the equity and the exchange rate, or
    /// <see langword="null"/>.</param>
    public EquityQuantoCashFlowPricer(
        YieldTermStructure? quantoCurrencyTermStructure,
        BlackVolTermStructure? equityVolatility,
        BlackVolTermStructure? fxVolatility,
        double? correlation)
    {
        this.quantoCurrencyTermStructure = quantoCurrencyTermStructure;
        this.equityVolatility = equityVolatility;
        this.fxVolatility = fxVolatility;
        this.correlation = correlation;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The fixing date is before the base date; an input is missing; or the payment
    /// currency's curve and the two volatilities have different reference dates; each with QuantLib's text.</exception>
    public override double Price(EquityCashFlow cashFlow, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(cashFlow);
        ArgumentNullException.ThrowIfNull(context);

        // QuantLib's initialize
        EquityIndex index = cashFlow.EquityIndex;
        Date baseDate = cashFlow.BaseDate()!.Value;
        Date fixingDate = cashFlow.FixingDate;
        if (!(fixingDate >= baseDate))
        {
            throw new NanoQLException("Fixing date cannot fall before base date.");
        }

        YieldTermStructure quantoCurve = quantoCurrencyTermStructure
            ?? throw new NanoQLException("Quanto currency term structure handle cannot be empty.");
        BlackVolTermStructure equityVol = equityVolatility
            ?? throw new NanoQLException("Equity volatility term structure handle cannot be empty.");
        BlackVolTermStructure fxVol = fxVolatility
            ?? throw new NanoQLException("FX volatility term structure handle cannot be empty.");
        double rho = correlation ?? throw new NanoQLException("Correlation handle cannot be empty.");
        if (!(quantoCurve.ReferenceDate == equityVol.ReferenceDate && equityVol.ReferenceDate == fxVol.ReferenceDate))
        {
            throw new NanoQLException(
                "Quanto currency term structure, equity and FX volatility need to have the same reference date.");
        }

        // QuantLib's price
        double strike = index.Fixing(fixingDate, context);
        YieldTermStructure dividend = index.EquityDividendCurve
            ?? FlatForward.Create(0, new NullCalendar(), 0.0, new Actual365Fixed(), context);
        YieldTermStructure interest = index.EquityInterestRateCurve
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");
        var quantoTermStructure = new QuantoTermStructure(dividend, quantoCurve, interest, equityVol, strike, fxVol, 1.0, rho);
        EquityIndex quantoIndex = index.Clone(quantoCurve, quantoTermStructure, index.Spot);
        double i0 = quantoIndex.Fixing(baseDate, context);
        double i1 = quantoIndex.Fixing(fixingDate, context);
        return cashFlow.GrowthOnly ? (i1 / i0) - 1.0 : i1 / i0;
    }
}
