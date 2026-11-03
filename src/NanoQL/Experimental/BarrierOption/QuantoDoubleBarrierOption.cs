// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/quantodoublebarrieroption.hpp ql/experimental/barrieroption/quantodoublebarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>
/// A quanto double barrier option: a double barrier option on an asset in a foreign currency, paid in the domestic
/// one.
/// </summary>
/// <remarks>
/// Immutable and sealed: QuantLib derives nothing from it. <c>QuantoEngine</c> prices it through a double-barrier
/// engine on its own terms, its results a <see cref="QuantoOptionResults"/> (<see cref="QuantoBarrierOption"/>'s form).
/// </remarks>
[Experimental("NQL0001")]
public sealed class QuantoDoubleBarrierOption : DoubleBarrierOption
{
    /// <summary>Initializes a new instance of the <see cref="QuantoDoubleBarrierOption"/> class.</summary>
    /// <param name="barrierType">The barrier's type.</param>
    /// <param name="barrierLo">The lower barrier.</param>
    /// <param name="barrierHi">The upper barrier.</param>
    /// <param name="rebate">The rebate.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public QuantoDoubleBarrierOption(
        DoubleBarrierType barrierType,
        double barrierLo,
        double barrierHi,
        double rebate,
        StrikedTypePayoff payoff,
        Exercise exercise)
        : base(barrierType, barrierLo, barrierHi, rebate, payoff, exercise)
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
