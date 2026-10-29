// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/paymentterm.hpp ql/experimental/commodities/paymentterm.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The event a payment term counts from: QuantLib's <c>PaymentTerm::EventType</c>.</summary>
[Experimental("NQL0001")]
public enum PaymentTermEventType
{
    /// <summary>The trade date.</summary>
    TradeDate = 0,

    /// <summary>The pricing date.</summary>
    PricingDate = 1,
}
