// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/quantovanillaoption.hpp ql/instruments/quantovanillaoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A quanto vanilla option: a vanilla option on an asset in a foreign currency, paid in the domestic one.</summary>
/// <remarks>
/// Immutable. As in QuantLib it derives <see cref="OneAssetOption"/>, not <see cref="VanillaOption"/>, so a plain
/// engine can't price it; <c>QuantoEngine</c> prices it through a vanilla engine on its terms (the S-exo plan's
/// ER2 (ii)).
/// </remarks>
public sealed class QuantoVanillaOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="QuantoVanillaOption"/> class.</summary>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public QuantoVanillaOption(StrikedTypePayoff payoff, Exercise exercise)
        : base(payoff, exercise)
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
