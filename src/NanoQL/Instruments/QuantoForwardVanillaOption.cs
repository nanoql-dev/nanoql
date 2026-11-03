// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/quantoforwardvanillaoption.hpp ql/instruments/quantoforwardvanillaoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A quanto forward-start vanilla option: a forward-start option on an asset in a foreign currency, paid in the
/// domestic one.
/// </summary>
/// <remarks>Immutable. As in QuantLib it derives <see cref="ForwardVanillaOption"/>.</remarks>
public sealed class QuantoForwardVanillaOption : ForwardVanillaOption
{
    /// <summary>Initializes a new instance of the <see cref="QuantoForwardVanillaOption"/> class.</summary>
    /// <param name="moneyness">The strike's multiple of the spot at the reset date.</param>
    /// <param name="resetDate">The date the strike is set on.</param>
    /// <param name="payoff">The payoff, whose type the option takes.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reset date is <c>default(Date)</c> (QuantLib's text).</exception>
    public QuantoForwardVanillaOption(double moneyness, Date resetDate, StrikedTypePayoff payoff, Exercise exercise)
        : base(moneyness, resetDate, payoff, exercise)
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
