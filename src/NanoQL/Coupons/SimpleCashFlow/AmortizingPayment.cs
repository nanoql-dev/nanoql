// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/simplecashflow.hpp ql/cashflows/simplecashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>An amortizing payment: a simple cash flow that more detailed cash-flow analysis can tell apart.</summary>
public sealed class AmortizingPayment : SimpleCashFlow
{
    /// <summary>Initializes a new instance of the <see cref="AmortizingPayment"/> class.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="date">The payment date.</param>
    public AmortizingPayment(double amount, Date date)
        : base(amount, date)
    {
    }
}
