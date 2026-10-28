// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/riskynotional.hpp ql/experimental/catbonds/riskynotional.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A proportional notional risk: the cumulated losses above an attachment reduce the notional linearly, to
/// nothing at the exhaustion.</summary>
/// <remarks>An extension point, as in QuantLib.</remarks>
[Experimental("NQL0001")]
public class ProportionalNotionalRisk : NotionalRisk
{
    /// <summary>Initializes a new instance of the <see cref="ProportionalNotionalRisk"/> class.</summary>
    /// <param name="paymentOffset">The offset from an event to its reduction's payment.</param>
    /// <param name="attachement">The cumulated loss from which the notional is reduced.</param>
    /// <param name="exhaustion">The cumulated loss that takes the whole notional.</param>
    /// <exception cref="ArgumentNullException"><paramref name="paymentOffset"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The exhaustion isn't above the attachment.</exception>
    public ProportionalNotionalRisk(EventPaymentOffset paymentOffset, double attachement, double exhaustion)
        : base(paymentOffset)
    {
        if (!(attachement < exhaustion))
        {
            throw new ArgumentException("exhaustion level needs to be greater than attachement", nameof(exhaustion));
        }

        Attachement = attachement;
        Exhaustion = exhaustion;
    }

    /// <summary>Gets the cumulated loss from which the notional is reduced.</summary>
    protected double Attachement { get; }

    /// <summary>Gets the cumulated loss that takes the whole notional.</summary>
    protected double Exhaustion { get; }

    /// <inheritdoc/>
    public override void UpdatePath(IReadOnlyList<KeyValuePair<Date, double>> events, NotionalPath path)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(path);
        path.Reset();
        double losses = 0;
        double previousNotional = 1;
        foreach (KeyValuePair<Date, double> e in events)
        {
            losses += e.Value;
            if (losses > Attachement && previousNotional > 0)
            {
                previousNotional = Math.Max(0.0, (Exhaustion - losses) / (Exhaustion - Attachement));
                path.AddReduction(PaymentOffset.PaymentDate(e.Key), previousNotional);
            }
        }
    }
}
