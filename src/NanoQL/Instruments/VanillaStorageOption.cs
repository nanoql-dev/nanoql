// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/vanillastorageoption.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// A storage option: the right, at each of its Bermudan exercise dates, to inject or withdraw up to the change rate
/// into a storage of the given capacity, starting from the given load.
/// </summary>
/// <remarks>
/// Immutable; its payoff a <see cref="NullPayoff"/>, as QuantLib's. Its engines are experimental and not yet ported;
/// the instrument carries QuantLib's terms and checks.
/// </remarks>
public sealed class VanillaStorageOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="VanillaStorageOption"/> class.</summary>
    /// <param name="exercise">The exercise dates.</param>
    /// <param name="capacity">The storage's capacity.</param>
    /// <param name="load">The storage's initial load.</param>
    /// <param name="changeRate">The most the load can change at an exercise date.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exercise"/> is <see langword="null"/>.</exception>
    public VanillaStorageOption(BermudanExercise exercise, double capacity, double load, double changeRate)
        : base(new NullPayoff(), exercise)
    {
        Capacity = capacity;
        Load = load;
        ChangeRate = changeRate;
    }

    /// <summary>Gets the storage's capacity.</summary>
    public double Capacity { get; }

    /// <summary>Gets the storage's initial load.</summary>
    public double Load { get; }

    /// <summary>Gets the most the load can change at an exercise date.</summary>
    public double ChangeRate { get; }

    /// <summary>Whether the option has expired: its last exercise date has occurred under the context.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the option has expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(Exercise.LastDate()).HasOccurred(context);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The capacity or the change rate isn't positive, the load is negative, or the load or the change rate exceeds the
    /// capacity.
    /// </exception>
    public override void Validate()
    {
        base.Validate();
        if (!(Capacity > 0.0 && ChangeRate > 0.0 && Load >= 0.0))
        {
            throw new NanoQLException("positive capacity, load and change rate required");
        }

        if (!(Load <= Capacity && ChangeRate <= Capacity))
        {
            throw new NanoQLException("illegal values load of changeRate");
        }
    }
}
