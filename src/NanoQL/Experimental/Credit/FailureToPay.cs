// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaulttype.hpp ql/experimental/credit/defaulttype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The failure-to-pay atomic event type, with its grace period and the amount required to trigger it.</summary>
/// <remarks>
/// The amount is in dollars by default, as ISDA's documentation sets it, not in the contract's currency, which
/// QuantLib leaves open ("FIX ME").
/// </remarks>
[Experimental("NQL0001")]
public sealed class FailureToPay : DefaultType
{
    /// <summary>Initializes a new instance of the <see cref="FailureToPay"/> class.</summary>
    /// <param name="grace">The grace period: a payment made within it removes the event.</param>
    /// <param name="amount">The minimum defaulted amount that triggers the event.</param>
    public FailureToPay(Period grace, double amount = 1.0e+6)
        : base(AtomicDefaultType.FailureToPay, RestructuringType.XR)
    {
        GracePeriod = grace;
        AmountRequired = amount;
    }

    /// <summary>Gets the minimum defaulted amount that triggers the event.</summary>
    public double AmountRequired { get; }

    /// <summary>Gets the grace period.</summary>
    public Period GracePeriod { get; }
}
