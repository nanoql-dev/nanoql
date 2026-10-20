// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/claim.hpp ql/instruments/claim.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>The claim associated with a default event: the amount the protection pays.</summary>
/// <remarks>
/// An extension point. The amount takes the evaluation context, because <see cref="FaceValueAccrualClaim"/> reads its
/// bond's accrued amount and notional, which take it.
/// </remarks>
public abstract class Claim
{
    /// <summary>The amount claimed on a default.</summary>
    /// <param name="defaultDate">The default date, or <see langword="null"/>, QuantLib's <c>Date()</c>, which a claim
    /// reading a date takes as its bond's settlement date.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The amount.</returns>
    public abstract double Amount(Date? defaultDate, double notional, double recoveryRate, EvaluationContext context);
}
