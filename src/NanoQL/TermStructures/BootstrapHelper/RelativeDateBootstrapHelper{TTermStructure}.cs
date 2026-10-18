// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/bootstraphelper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// The base of a bootstrap helper whose dates follow the evaluation date: QuantLib's
/// <c>RelativeDateBootstrapHelper</c>.
/// </summary>
/// <typeparam name="TTermStructure">The kind of term structure the helper prices against.</typeparam>
/// <remarks>
/// The mechanism lives in <see cref="BootstrapHelper{TTermStructure}"/> (its constructor taking the context, and
/// <see cref="BootstrapHelper{TTermStructure}.InitializeDates"/>), since a family's own relative base derives from that
/// family's base instead (<see cref="RelativeDateRateHelper"/>, QuantLib's <c>RelativeDateRateHelper</c>). Each
/// relative helper overrides <see cref="InitializeDates"/>, which this class makes abstract.
/// </remarks>
public abstract class RelativeDateBootstrapHelper<TTermStructure> : BootstrapHelper<TTermStructure>
    where TTermStructure : TermStructure
{
    /// <summary>Initializes a new instance of the <see cref="RelativeDateBootstrapHelper{TTermStructure}"/> class.</summary>
    /// <param name="quote">The quote the helper reprices.</param>
    /// <param name="context">
    /// The evaluation context whose evaluation date the helper's dates are computed from; required when
    /// <paramref name="updateDates"/> is set.
    /// </param>
    /// <param name="updateDates">
    /// Whether the dates follow the evaluation date: <see langword="false"/> for a helper built with explicit dates.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="updateDates"/> is set and <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    protected RelativeDateBootstrapHelper(double quote, EvaluationContext? context, bool updateDates = true)
        : base(quote, context, updateDates)
    {
    }

    /// <inheritdoc/>
    protected abstract override void InitializeDates(EvaluationContext context);
}
