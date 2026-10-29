// SPDX-FileCopyrightText: Copyright (C) 2018 Sebastian Schlenkrich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/basismodels/swaptioncfs.hpp ql/experimental/basismodels/swaptioncfs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A swaption mapped to deterministic fixed and floating cash flows under a tenor basis, with its exercise times after
/// the discount curve's reference date: QuantLib's <c>SwaptionCashFlows</c>.
/// </summary>
[Experimental("NQL0001")]
public sealed class SwaptionCashFlows : SwapCashFlows
{
    /// <summary>Initializes a new instance of the <see cref="SwaptionCashFlows"/> class, with no flows: QuantLib's
    /// default constructor.</summary>
    public SwaptionCashFlows()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SwaptionCashFlows"/> class.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="discountCurve">The discount curve, whose reference date the times start from.</param>
    /// <param name="context">The evaluation context, whose fixings and forecasts the floating coupons' rates
    /// read.</param>
    /// <param name="contTenorSpread">Whether the floating spreads are continuously compounded and paid at each coupon's
    /// start; otherwise simply compounded and paid at its payment date.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The underlying's floating leg is empty.</exception>
    /// <exception cref="NanoQLException">A flow isn't a coupon, or a rate can't be read.</exception>
    public SwaptionCashFlows(
        Swaption swaption, YieldTermStructure discountCurve, EvaluationContext context, bool contTenorSpread = true)
        : base((swaption ?? throw new ArgumentNullException(nameof(swaption))).Underlying, discountCurve, context, contTenorSpread)
    {
        Swaption = swaption;

        // ... exercise times, the future ones only
        var dc = new Actual365Fixed();
        ExerciseTimes = [.. swaption.Exercise.Dates.Where(d => d > RefDate).Select(d => dc.YearFraction(RefDate, d))];
    }

    /// <summary>Gets the swaption; <see langword="null"/> for no flows.</summary>
    public Swaption? Swaption { get; }

    /// <summary>Gets the exercise times after the reference date, Actual/365 (Fixed).</summary>
    public ImmutableArray<double> ExerciseTimes { get; } = [];
}
