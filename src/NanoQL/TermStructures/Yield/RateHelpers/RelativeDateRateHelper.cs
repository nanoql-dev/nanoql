// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2015 Maddalena Zanzi
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/ratehelpers.hpp ql/termstructures/yield/ratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// The base of a yield helper whose dates follow the evaluation date: QuantLib's <c>RelativeDateRateHelper</c>, a
/// typedef of <c>RelativeDateBootstrapHelper&lt;YieldTermStructure&gt;</c>.
/// </summary>
/// <remarks>
/// A class can't have both of QuantLib's bases, so it derives from <see cref="RateHelper"/>, and the relative-date
/// mechanism lives in <see cref="BootstrapHelper{TTermStructure}"/>. Each derived helper computes its dates in
/// <see cref="InitializeDates"/>, which its constructor calls and the bootstraps call on a copy to re-date it. An
/// extension point.
/// </remarks>
public abstract class RelativeDateRateHelper : RateHelper
{
    /// <summary>Initializes a new instance of the <see cref="RelativeDateRateHelper"/> class.</summary>
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
    protected RelativeDateRateHelper(double quote, EvaluationContext? context, bool updateDates = true)
        : base(quote, context, updateDates)
    {
    }

    /// <inheritdoc/>
    protected abstract override void InitializeDates(EvaluationContext context);
}
