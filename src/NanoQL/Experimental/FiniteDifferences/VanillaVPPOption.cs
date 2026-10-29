// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/vanillavppoption.hpp ql/experimental/finitedifferences/vanillavppoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>
/// A virtual power plant: the right to run a gas-fired plant at each exercise date, earning power less gas at its heat
/// rate, within its output range, minimum up and down times, start costs and, optionally, a limit on its starts or
/// running hours.
/// </summary>
/// <remarks>
/// The option is its engines' arguments, as QuantLib's <c>arguments</c> carries its members; its payoff is QuantLib's,
/// the average basket payoff of power and gas weighted 1 and −heatRate on the identity.
/// </remarks>
[Experimental("NQL0001")]
public sealed class VanillaVPPOption : MultiAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="VanillaVPPOption"/> class.</summary>
    /// <param name="heatRate">The heat rate: gas burned per unit of power.</param>
    /// <param name="pMin">The minimum output while running.</param>
    /// <param name="pMax">The maximum output.</param>
    /// <param name="tMinUp">The minimum up time, in exercise steps.</param>
    /// <param name="tMinDown">The minimum down time, in exercise steps.</param>
    /// <param name="startUpFuel">The gas a start burns.</param>
    /// <param name="startUpFixCost">A start's fixed cost.</param>
    /// <param name="exercise">The exercise dates.</param>
    /// <param name="nStarts">The most starts, or <see langword="null"/> for no limit.</param>
    /// <param name="nRunningHours">The most running hours, or <see langword="null"/> for no limit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exercise"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public VanillaVPPOption(
        double heatRate,
        double pMin,
        double pMax,
        int tMinUp,
        int tMinDown,
        double startUpFuel,
        double startUpFixCost,
        SwingExercise exercise,
        int? nStarts = null,
        int? nRunningHours = null)
        : base(new AverageBasketPayoff(new IdenticalPayoff(), [1.0, -heatRate]), ExerciseOf(exercise))
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tMinUp);
        ArgumentOutOfRangeException.ThrowIfNegative(tMinDown);
        if (nStarts is int s)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(s, nameof(nStarts));
        }

        if (nRunningHours is int h)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(h, nameof(nRunningHours));
        }

        HeatRate = heatRate;
        PMin = pMin;
        PMax = pMax;
        TMinUp = tMinUp;
        TMinDown = tMinDown;
        StartUpFuel = startUpFuel;
        StartUpFixCost = startUpFixCost;
        SwingExercise = exercise;
        NStarts = nStarts;
        NRunningHours = nRunningHours;
    }

    /// <summary>Gets the heat rate.</summary>
    public double HeatRate { get; }

    /// <summary>Gets the minimum output while running.</summary>
    public double PMin { get; }

    /// <summary>Gets the maximum output.</summary>
    public double PMax { get; }

    /// <summary>Gets the minimum up time, in exercise steps.</summary>
    public int TMinUp { get; }

    /// <summary>Gets the minimum down time, in exercise steps.</summary>
    public int TMinDown { get; }

    /// <summary>Gets the gas a start burns.</summary>
    public double StartUpFuel { get; }

    /// <summary>Gets a start's fixed cost.</summary>
    public double StartUpFixCost { get; }

    /// <summary>Gets the exercise dates.</summary>
    public SwingExercise SwingExercise { get; }

    /// <summary>Gets the most starts, or <see langword="null"/> for no limit.</summary>
    public int? NStarts { get; }

    /// <summary>Gets the most running hours, or <see langword="null"/> for no limit.</summary>
    public int? NRunningHours { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Both a start limit and a running-hour limit are given ("either a start limit or
    /// fuel limit is supported").</exception>
    public override void Validate()
    {
        // QuantLib's "no exercise given" can't fail: the exercise is refused at construction when null.
        if (!(NStarts is null || NRunningHours is null))
        {
            throw new NanoQLException("either a start limit or fuel limit is supported");
        }
    }

    private static SwingExercise ExerciseOf(SwingExercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise;
    }

    // QuantLib's IdenticalPayoff: the price itself
    private sealed class IdenticalPayoff : Payoff
    {
        public override string Name => "IdenticalPayoff";

        public override string Description => Name;

        public override double Evaluate(double price) => price;
    }
}
