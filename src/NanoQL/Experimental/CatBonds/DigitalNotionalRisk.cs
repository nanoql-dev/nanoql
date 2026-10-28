// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/riskynotional.hpp ql/experimental/catbonds/riskynotional.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A digital notional risk: an event whose loss reaches a threshold takes the whole notional.</summary>
/// <remarks>An extension point, as in QuantLib.</remarks>
[Experimental("NQL0001")]
public class DigitalNotionalRisk : NotionalRisk
{
    /// <summary>Initializes a new instance of the <see cref="DigitalNotionalRisk"/> class.</summary>
    /// <param name="paymentOffset">The offset from an event to its reduction's payment.</param>
    /// <param name="threshold">The loss that takes the notional.</param>
    /// <exception cref="ArgumentNullException"><paramref name="paymentOffset"/> is <see langword="null"/>.</exception>
    public DigitalNotionalRisk(EventPaymentOffset paymentOffset, double threshold)
        : base(paymentOffset)
    {
        Threshold = threshold;
    }

    /// <summary>Gets the loss that takes the notional.</summary>
    protected double Threshold { get; }

    /// <inheritdoc/>
    public override void UpdatePath(IReadOnlyList<KeyValuePair<Date, double>> events, NotionalPath path)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(path);
        path.Reset();
        foreach (KeyValuePair<Date, double> e in events)
        {
            if (e.Value >= Threshold)
            {
                path.AddReduction(PaymentOffset.PaymentDate(e.Key), 0.0);
            }
        }
    }
}
