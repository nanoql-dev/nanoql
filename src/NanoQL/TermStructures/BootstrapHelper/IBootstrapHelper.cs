// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/bootstraphelper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A bootstrap helper seen without its term structure's type: what a global bootstrap's penalties and initial-guess
/// function read of the helpers they are given (<see cref="IBootstrapState"/>).
/// </summary>
/// <remarks><see cref="BootstrapHelper{TTermStructure}"/> implements it; its members are documented there.</remarks>
public interface IBootstrapHelper
{
    /// <summary>Gets the quote the helper reprices.</summary>
    double Quote { get; }

    /// <summary>Gets the earliest date at which the helper needs data to give its quote.</summary>
    Date? EarliestDate { get; }

    /// <summary>Gets the instrument's maturity date.</summary>
    Date? MaturityDate { get; }

    /// <summary>Gets the latest date at which the helper needs data to give its quote.</summary>
    Date? LatestRelevantDate { get; }

    /// <summary>Gets the pillar date: the node the helper's quote determines.</summary>
    Date? PillarDate { get; }

    /// <summary>Gets the latest date, the pillar date unless a helper sets it apart.</summary>
    Date? LatestDate { get; }

    /// <summary>The quote implied by the term structure the helper is bound to.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The implied quote.</returns>
    double ImpliedQuote(EvaluationContext context);

    /// <summary>The quote minus the implied quote.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The error.</returns>
    double QuoteError(EvaluationContext context);
}
