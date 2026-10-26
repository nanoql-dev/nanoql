// SPDX-FileCopyrightText: Copyright (C) 2011 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/writerextensibleoption.hpp ql/instruments/writerextensibleoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// A writer-extensible option: out of the money at its first exercise date, it is extended to a later exercise date
/// at an amended strike.
/// </summary>
/// <remarks>Immutable; it expires at its second exercise, as QuantLib's.</remarks>
public sealed class WriterExtensibleOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="WriterExtensibleOption"/> class.</summary>
    /// <param name="payoff1">The payoff at the first exercise.</param>
    /// <param name="exercise1">The first exercise.</param>
    /// <param name="payoff2">The payoff at the second exercise.</param>
    /// <param name="exercise2">The second exercise.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public WriterExtensibleOption(
        PlainVanillaPayoff payoff1, Exercise exercise1, PlainVanillaPayoff payoff2, Exercise exercise2)
        : base(payoff1, exercise1)
    {
        ArgumentNullException.ThrowIfNull(payoff2);
        ArgumentNullException.ThrowIfNull(exercise2);
        Payoff2 = payoff2;
        Exercise2 = exercise2;
    }

    /// <summary>Gets the payoff at the second exercise.</summary>
    public PlainVanillaPayoff Payoff2 { get; }

    /// <summary>Gets the second exercise.</summary>
    public Exercise Exercise2 { get; }

    /// <summary>Whether the option has expired: its second exercise date has occurred under the context.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the option has expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(Exercise2.LastDate()).HasOccurred(context);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The second exercise isn't after the first.</exception>
    public override void Validate()
    {
        base.Validate();
        if (!(Exercise2.LastDate() > Exercise.LastDate()))
        {
            throw new NanoQLException("second exercise date is not later than the first");
        }
    }
}
