// SPDX-FileCopyrightText: Copyright (C) 2005 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/bondhelpers.hpp ql/termstructures/yield/bondhelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;

namespace NanoQL.TermStructures;

/// <summary>A rate helper for bootstrapping over bond prices: the bond's clean or dirty price on the curve.</summary>
/// <remarks>
/// <para>
/// The helper prices a plain copy of the bond (<see cref="Bond"/>), as QuantLib's copies it as its base class, so a
/// derived bond's overrides don't reach its price: a callable bond's helper quotes its straight twin's price.
/// <see cref="Bond"/> returns that copy, which shares the bond's cash flows. The copy is priced by
/// <see cref="DiscountingBondEngine"/> on the curve the helper is bound to, and only on that curve: the bond's floating
/// coupons keep forecasting on their own index's curve, as QuantLib relinks only its discounting handle. The engine's
/// <c>includeSettlementDateFlows</c> is unset, so the context's
/// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> decides.
/// </para>
/// <para>
/// It is no relative-date helper: its earliest date, the bond's next cash-flow date, is computed with the context it is
/// built with and never again, while its implied quote reads the context it is given, as QuantLib's reads the current
/// evaluation date through the bond's settlement date. Its latest date is the bond's last cash-flow date, which can be
/// later than the maturity date because of the payment adjustment. Unsealed, an extension point:
/// <see cref="FixedRateBondHelper"/> derives from it.
/// </para>
/// </remarks>
public class BondHelper : RateHelper
{
    private readonly Bond bond;

    /// <summary>Initializes a new instance of the <see cref="BondHelper"/> class.</summary>
    /// <param name="price">The bond's quoted price, per 100 of notional.</param>
    /// <param name="bond">The bond; the helper prices a plain copy of it.</param>
    /// <param name="context">The evaluation context, which gives the earliest date.</param>
    /// <param name="priceType">Whether the price is clean or dirty.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="bond"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="priceType"/> isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// The bond has no cash flows, where QuantLib reads past the end of an empty leg.
    /// </exception>
    public BondHelper(
        double price,
        Bond bond,
        EvaluationContext context,
        BondPriceType priceType = BondPriceType.Clean)
        : base(price)
    {
        ArgumentNullException.ThrowIfNull(context);

        // QuantLib copies the bond unchecked, which is undefined for a null one
        ArgumentNullException.ThrowIfNull(bond);
        EnumArguments.Check(priceType, nameof(priceType));
        this.bond = new Bond(bond);
        PriceType = priceType;

        // the bond's last cashflow date, which can be later than
        // bond's maturity date because of adjustment
        if (this.bond.Cashflows.IsEmpty)
        {
            throw new ArgumentException("bond with no cashflows!", nameof(bond));
        }

        latestDate = this.bond.Cashflows[^1].Date;
        earliestDate = this.bond.NextCashFlowDate(context);
    }

    /// <summary>Gets the bond the helper prices: a plain copy of the bond it was given.</summary>
    public Bond Bond => bond;

    /// <summary>Gets whether the quoted price is clean or dirty.</summary>
    public BondPriceType PriceType { get; }

    /// <inheritdoc/>
    /// <remarks>The bond's clean or dirty price on the bound curve, at the bond's settlement date for the context.</remarks>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        BondResults results = DiscountingBondEngine.Calculate(bond, context, TermStructure!);

        switch (PriceType)
        {
            case BondPriceType.Clean:
                return bond.CleanPrice(results, context);

            case BondPriceType.Dirty:
                return bond.DirtyPrice(results, context);

            default:
                throw new NanoQLException("This price type isn't implemented.");
        }
    }
}
