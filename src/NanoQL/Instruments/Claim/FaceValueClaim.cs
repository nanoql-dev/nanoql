// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/claim.hpp ql/instruments/claim.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A claim on a notional: the notional less the recovery.</summary>
public sealed class FaceValueClaim : Claim
{
    /// <summary>The amount claimed: <c>notional (1 - recoveryRate)</c>, whatever the date.</summary>
    /// <param name="defaultDate">The default date, which the claim doesn't read.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="context">The evaluation context, which the claim doesn't read.</param>
    /// <returns>The amount.</returns>
    public override double Amount(Date? defaultDate, double notional, double recoveryRate, EvaluationContext context) =>
        notional * (1.0 - recoveryRate);
}
