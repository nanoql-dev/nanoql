// SPDX-FileCopyrightText: Copyright (C) 2008 Siarhei Novik (snovik@gmail.com)
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 , 2010 Andrea Maggiulli (a.maggiulli@gmail.com)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: src/QLNet/Cashflows/SimpleCashFlow.cs @ QLNet-v1.13.2
// Ported from QLNet and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Extensions.Coupons;

/// <summary>
/// A voluntary prepayment: principal a mortgage pool's borrowers pay ahead of its schedule, as a simple cash flow, so
/// that an analysis can tell it from the scheduled principal (<see cref="AmortizingPayment"/>).
/// </summary>
public sealed class VoluntaryPrepay : SimpleCashFlow
{
    /// <summary>Initializes a new instance of the <see cref="VoluntaryPrepay"/> class.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="date">The payment date.</param>
    public VoluntaryPrepay(double amount, Date date)
        : base(amount, date)
    {
    }
}
