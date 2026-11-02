// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace Acme.Models;

/// <summary>A user's own curve on nanoql's extension point: its discount factor exp(-rt - dt²/2).</summary>
public sealed class ExponentialCurve : YieldTermStructure
{
    private readonly double decay;

    /// <summary>Initializes a new instance of the <see cref="ExponentialCurve"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="rate">The short rate at the reference date.</param>
    /// <param name="decay">The rate's growth with time.</param>
    /// <param name="dayCounter">The day counter.</param>
    public ExponentialCurve(Date referenceDate, double rate, double decay, DayCounter dayCounter)
        : base(referenceDate, null, dayCounter)
    {
        Rate = rate;
        this.decay = decay;
    }

    /// <summary>Gets the short rate at the reference date.</summary>
    public double Rate { get; }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <inheritdoc/>
    protected override double DiscountImpl(double t) => Math.Exp((-Rate * t) - (0.5 * decay * t * t));
}
