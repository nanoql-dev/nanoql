// SPDX-FileCopyrightText: Copyright (C) 2008 Paul Farrington
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/quantobarrieroption.hpp ql/instruments/quantobarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// A quanto barrier option: a barrier option on an asset in a foreign currency, paid in the domestic one.
/// </summary>
/// <remarks>
/// Immutable and sealed: QuantLib derives nothing from it. <c>QuantoEngine</c> prices it through a barrier engine on its
/// own terms, its results a <see cref="QuantoOptionResults"/> (the S-bar plan's BR2 (iii), the S-exo plan's ER2
/// (ii)'s form).
/// </remarks>
public sealed class QuantoBarrierOption : BarrierOption
{
    /// <summary>Initializes a new instance of the <see cref="QuantoBarrierOption"/> class.</summary>
    /// <param name="barrierType">The barrier's type.</param>
    /// <param name="barrier">The barrier.</param>
    /// <param name="rebate">The rebate.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public QuantoBarrierOption(
        BarrierType barrierType, double barrier, double rebate, StrikedTypePayoff payoff, Exercise exercise)
        : base(barrierType, barrier, rebate, payoff, exercise)
    {
    }

    /// <inheritdoc/>
    /// <remarks>An expired option's Greeks and quanto sensitivities are 0, as QuantLib's.</remarks>
    public override QuantoOptionResults SetupExpired() => new(base.SetupExpired())
    {
        Qvega = 0.0,
        Qrho = 0.0,
        Qlambda = 0.0,
    };

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The results aren't a quanto option's.</exception>
    public override QuantoOptionResults FetchResults(InstrumentResults results)
    {
        OneAssetOptionResults fetched = base.FetchResults(results);
        if (fetched is not QuantoOptionResults r)
        {
            throw new ArgumentException("no quanto results returned from pricing engine", nameof(results));
        }

        return r;
    }
}
