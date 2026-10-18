// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/simplecashflow.hpp ql/cashflows/simplecashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A predetermined cash flow: an amount paid on a date.</summary>
/// <remarks>Unsealed, as in QuantLib: <see cref="Redemption"/> and <see cref="AmortizingPayment"/> derive from it.</remarks>
public class SimpleCashFlow : CashFlow
{
    private readonly double amount;
    private readonly Date date;

    /// <summary>Initializes a new instance of the <see cref="SimpleCashFlow"/> class.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="date">The payment date.</param>
    public SimpleCashFlow(double amount, Date date)
    {
        if (date.SerialNumber == 0)
        {
            // QuantLib's QL_REQUIRE(date_ != Date(), "null date SimpleCashFlow"), with its wording.
            throw new ArgumentException("null date SimpleCashFlow", nameof(date));
        }
        this.amount = amount;
        this.date = date;
    }

    /// <inheritdoc/>
    public override Date Date => date;

    /// <inheritdoc/>
    public override double Amount(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return amount;
    }
}
