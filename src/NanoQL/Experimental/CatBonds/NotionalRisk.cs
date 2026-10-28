// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/riskynotional.hpp ql/experimental/catbonds/riskynotional.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A cat bond's notional risk: how a path's events reduce its notional.</summary>
/// <remarks>An extension point, as in QuantLib.</remarks>
[Experimental("NQL0001")]
public abstract class NotionalRisk
{
    /// <summary>Initializes a new instance of the <see cref="NotionalRisk"/> class.</summary>
    /// <param name="paymentOffset">The offset from an event to its reduction's payment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="paymentOffset"/> is <see langword="null"/>.</exception>
    protected NotionalRisk(EventPaymentOffset paymentOffset)
    {
        ArgumentNullException.ThrowIfNull(paymentOffset);
        PaymentOffset = paymentOffset;
    }

    /// <summary>Gets the offset from an event to its reduction's payment.</summary>
    protected EventPaymentOffset PaymentOffset { get; }

    /// <summary>Writes a path's notional from its events, in date order.</summary>
    /// <param name="events">The events, their dates and losses.</param>
    /// <param name="path">The notional path, reset first.</param>
    public abstract void UpdatePath(IReadOnlyList<KeyValuePair<Date, double>> events, NotionalPath path);
}
