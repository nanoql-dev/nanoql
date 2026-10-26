// SPDX-FileCopyrightText: Copyright (C) 2012 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/twoassetbarrieroption.hpp ql/instruments/twoassetbarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// A barrier option on two assets: the first asset's value is compared with the strike for the payoff, while the
/// second's is monitored against the barrier.
/// </summary>
/// <remarks>
/// Immutable and sealed: QuantLib derives nothing from it. As in QuantLib it derives <see cref="Option"/>, not
/// <see cref="OneAssetOption"/>, so its results are an <see cref="InstrumentResults"/>.
/// </remarks>
public sealed class TwoAssetBarrierOption : Option
{
    /// <summary>Initializes a new instance of the <see cref="TwoAssetBarrierOption"/> class.</summary>
    /// <param name="barrierType">The barrier's type, on the second asset.</param>
    /// <param name="barrier">The barrier, on the second asset.</param>
    /// <param name="payoff">The striked payoff, on the first asset.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> isn't a member of its type, with
    /// QuantLib's "unknown type".</exception>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public TwoAssetBarrierOption(BarrierType barrierType, double barrier, StrikedTypePayoff payoff, Exercise exercise)
        : base(Checked(payoff), exercise)
    {
        EnumArguments.Check(barrierType, nameof(barrierType));
        BarrierType = barrierType;
        Barrier = barrier;
    }

    /// <summary>Gets the barrier's type.</summary>
    public BarrierType BarrierType { get; }

    /// <summary>Gets the barrier.</summary>
    public double Barrier { get; }

    /// <summary>Whether the option has expired: its last exercise date has occurred under the context.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the option has expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(Exercise.LastDate()).HasOccurred(context);
    }

    /// <summary>
    /// Whether the second asset's value lies beyond the barrier: below a down barrier, above an up one. QuantLib's
    /// engine's <c>triggered</c>.
    /// </summary>
    /// <param name="underlying">The second asset's value.</param>
    /// <returns><see langword="true"/> when the value lies strictly beyond the barrier.</returns>
    public bool Triggered(double underlying) => BarrierType switch
    {
        BarrierType.DownIn or BarrierType.DownOut => underlying < Barrier,
        _ => underlying > Barrier,
    };

    private static StrikedTypePayoff Checked(StrikedTypePayoff payoff)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return payoff;
    }
}
