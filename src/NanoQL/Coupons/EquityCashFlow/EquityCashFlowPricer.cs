// SPDX-FileCopyrightText: Copyright (C) 2023 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/equitycashflow.hpp ql/cashflows/equitycashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>The base of the equity cash flow pricers: the index ratio a flow pays, per unit of notional.</summary>
/// <remarks>
/// QuantLib's pricer keeps the flow's index and dates in members that <c>initialize</c> sets before each
/// <c>price</c>; here <see cref="Price"/> takes the flow, a function of the flow and the context (the S-cms plan's
/// CMD14). An extension point: the quanto pricer derives from it.
/// </remarks>
public abstract class EquityCashFlowPricer
{
    /// <summary>The ratio the flow pays per unit of notional: <c>I(T)/I(0)</c>, less one for a growth-only
    /// flow.</summary>
    /// <param name="cashFlow">The flow.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The ratio.</returns>
    public abstract double Price(EquityCashFlow cashFlow, EvaluationContext context);
}
