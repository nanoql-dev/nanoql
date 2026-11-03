// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/forwardswapquote.hpp ql/quotes/forwardswapquote.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Quotes;

/// <summary>
/// The fair rate of a forward-starting swap of a swap index, plus a spread: QuantLib's <c>ForwardSwapQuote</c>.
/// </summary>
/// <remarks>
/// <para>
/// The swap is the index's underlying swap fixing on the forward start's fixing date: its value date the evaluation
/// date advanced by the index's fixing days on its fixing calendar, its start that date advanced by the forward start,
/// both following; it is priced on the engine the index's underlying swap carries, discounting on the index's exogenous
/// discounting curve or else its forwarding curve. The value is −(floating NPV + spread × floating BPS / 1bp) /
/// (fixed BPS / 1bp).
/// </para>
/// <para>
/// Immutable: the dates and value are computed once, at construction, under the context (§2.2's quote row). QuantLib's
/// quote rebuilds its swap when the evaluation date moves; here a new date is a new quote. QuantLib's <c>isValid</c>
/// isn't ported: a swap that can't be priced is refused at construction.
/// </para>
/// </remarks>
public sealed class ForwardSwapQuote
{
    /// <summary>Initializes a new instance of the <see cref="ForwardSwapQuote"/> class.</summary>
    /// <param name="swapIndex">The swap index.</param>
    /// <param name="spread">The spread over the floating leg; 0 for none, QuantLib's empty handle.</param>
    /// <param name="fwdStart">The forward start, from the value date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="swapIndex"/> or <paramref name="context"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The swap can't be built or priced, or a leg's result isn't available.</exception>
    public ForwardSwapQuote(SwapIndex swapIndex, double spread, Period fwdStart, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(swapIndex);
        ArgumentNullException.ThrowIfNull(context);
        Calendar calendar = swapIndex.FixingCalendar;
        ValueDate = calendar.Advance(
            context.EvaluationDate, swapIndex.FixingDays, TimeUnit.Days, BusinessDayConvention.Following);
        StartDate = calendar.Advance(ValueDate, fwdStart, BusinessDayConvention.Following);
        FixingDate = swapIndex.FixingDate(StartDate);
        VanillaSwap swap = swapIndex.UnderlyingSwap(FixingDate, context);
        FixedVsFloatingSwapResults results = swapIndex.UnderlyingSwapResults(swap, context);

        const double BasisPoint = 1.0e-4;
        double floatingLegNPV = results.FloatingLegNPV ?? throw NotAvailable();
        double spreadNPV = (results.FloatingLegBPS ?? throw NotAvailable()) / BasisPoint * spread;
        double totNPV = -(floatingLegNPV + spreadNPV);
        Value = totNPV / ((results.FixedLegBPS ?? throw NotAvailable()) / BasisPoint);
    }

    /// <summary>Gets the value: the swap's fair rate plus the spread.</summary>
    public double Value { get; }

    /// <summary>Gets the value date.</summary>
    public Date ValueDate { get; }

    /// <summary>Gets the swap's start date.</summary>
    public Date StartDate { get; }

    /// <summary>Gets the swap's fixing date.</summary>
    public Date FixingDate { get; }

    private static NanoQLException NotAvailable() => new("result not available");
}
