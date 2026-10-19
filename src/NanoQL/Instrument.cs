// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instrument.hpp ql/instrument.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>The base class of instruments: an immutable object holding its terms.</summary>
/// <remarks>
/// An engine prices an instrument through the four steps of QuantLib's <c>Instrument::calculate</c>, which the
/// instrument owns: <see cref="IsExpired"/>, <see cref="SetupExpired"/>, <see cref="Validate"/> and
/// <see cref="FetchResults"/>. QuantLib's <c>setPricingEngine</c>, <c>pricingEngine</c> and <c>NPV()</c> become the
/// engine's static <c>Calculate</c>, which returns the results; QuantLib's <c>result&lt;T&gt;(tag)</c> and
/// <c>additionalResults()</c> are <see cref="InstrumentResults.Result{T}"/> and
/// <see cref="InstrumentResults.AdditionalResults"/> on them.
/// </remarks>
public abstract class Instrument
{
    /// <summary>Whether the instrument might still have a value greater than zero.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the instrument has expired.</returns>
    public abstract bool IsExpired(EvaluationContext context);

    /// <summary>The results of an expired instrument: a zero value and error estimate, and no valuation date.</summary>
    /// <returns>The expired results.</returns>
    public virtual InstrumentResults SetupExpired() => new()
    {
        NPV = 0.0,
        ErrorEstimate = 0.0,
        ValuationDate = null,
    };

    /// <summary>Checks the instrument's terms before an engine prices it, as QuantLib's arguments do.</summary>
    public virtual void Validate()
    {
    }

    /// <summary>Reads an engine's results into the instrument's, applying any derivations the instrument makes.</summary>
    /// <param name="results">The engine's results.</param>
    /// <returns>The instrument's results.</returns>
    public virtual InstrumentResults FetchResults(InstrumentResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results;
    }
}
