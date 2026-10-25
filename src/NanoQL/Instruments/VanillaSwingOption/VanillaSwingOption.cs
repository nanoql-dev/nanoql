// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/vanillaswingoption.hpp ql/instruments/vanillaswingoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// A swing option: a number of exercise rights, at least a minimum and at most a maximum of them to be used, each at
/// one of the swing exercise's date times, paying the payoff.
/// </summary>
/// <remarks>
/// Immutable. QuantLib's arguments and their checks are the option itself and <see cref="Validate"/>; its
/// <c>isExpired</c>, the last exercise date's event, is <see cref="OneAssetOption.IsExpired"/> on the context.
/// </remarks>
public sealed class VanillaSwingOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="VanillaSwingOption"/> class.</summary>
    /// <param name="payoff">The payoff, striked for the engines.</param>
    /// <param name="ex">The swing exercise.</param>
    /// <param name="minExerciseRights">The number of rights the holder must use at least.</param>
    /// <param name="maxExerciseRights">The number of rights the holder may use at most.</param>
    /// <exception cref="ArgumentNullException">The payoff or the exercise is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of rights is negative.</exception>
    public VanillaSwingOption(Payoff payoff, SwingExercise ex, int minExerciseRights, int maxExerciseRights)
        : base(payoff, ExerciseOf(ex))
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minExerciseRights);
        ArgumentOutOfRangeException.ThrowIfNegative(maxExerciseRights);
        SwingExercise = ex;
        MinExerciseRights = minExerciseRights;
        MaxExerciseRights = maxExerciseRights;
    }

    /// <summary>Gets the swing exercise.</summary>
    public SwingExercise SwingExercise { get; }

    /// <summary>Gets the number of rights the holder must use at least.</summary>
    public int MinExerciseRights { get; }

    /// <summary>Gets the number of rights the holder may use at most.</summary>
    public int MaxExerciseRights { get; }

    /// <summary>Gets the payoff, where it is striked: QuantLib's arguments' payoff, <see langword="null"/> otherwise.</summary>
    public StrikedTypePayoff? StrikedPayoff => Payoff as StrikedTypePayoff;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The payoff isn't striked, the minimum of rights exceeds the maximum, or the maximum exceeds the number of exercise
    /// date times, with QuantLib's messages.
    /// </exception>
    public override void Validate()
    {
        if (StrikedPayoff is null)
        {
            throw new NanoQLException("no payoff given");
        }

        // QuantLib's "no exercise given" can't fail: the exercise is refused at construction when null.
        if (!(MinExerciseRights <= MaxExerciseRights))
        {
            throw new NanoQLException("minExerciseRights <= maxExerciseRights");
        }

        if (!(SwingExercise.Dates.Length >= MaxExerciseRights))
        {
            throw new NanoQLException("number of exercise rights exceeds number of exercise dates");
        }
    }

    private static SwingExercise ExerciseOf(SwingExercise ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ex;
    }
}
