// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/claim.hpp ql/instruments/claim.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A claim on the notional of a reference security, including its accrual.</summary>
public sealed class FaceValueAccrualClaim : Claim
{
    private readonly Bond referenceSecurity;

    /// <summary>Initializes a new instance of the <see cref="FaceValueAccrualClaim"/> class.</summary>
    /// <param name="referenceSecurity">The reference security.</param>
    /// <exception cref="ArgumentNullException"><paramref name="referenceSecurity"/> is null.</exception>
    public FaceValueAccrualClaim(Bond referenceSecurity)
    {
        ArgumentNullException.ThrowIfNull(referenceSecurity);
        this.referenceSecurity = referenceSecurity;
    }

    /// <summary>
    /// The amount claimed: <c>notional (1 - recoveryRate - accrual)</c>, the accrual being the security's accrued
    /// amount over its notional at the date.
    /// </summary>
    /// <param name="defaultDate">The default date, or <see langword="null"/> for the security's settlement
    /// date.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="context">The evaluation context the security's amounts are read in.</param>
    /// <returns>The amount.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public override double Amount(Date? defaultDate, double notional, double recoveryRate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double accrual = referenceSecurity.AccruedAmount(context, defaultDate)
                         / referenceSecurity.Notional(context, defaultDate);
        return notional * (1.0 - recoveryRate - accrual);
    }
}
