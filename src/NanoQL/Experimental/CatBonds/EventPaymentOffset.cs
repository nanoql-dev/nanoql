// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/riskynotional.hpp ql/experimental/catbonds/riskynotional.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The offset from a catastrophe event to the date its notional reduction is paid.</summary>
/// <remarks>An extension point, as in QuantLib.</remarks>
[Experimental("NQL0001")]
public abstract class EventPaymentOffset
{
    /// <summary>The date an event's notional reduction is paid.</summary>
    /// <param name="eventDate">The event's date.</param>
    /// <returns>The payment date.</returns>
    public abstract Date PaymentDate(Date eventDate);
}
