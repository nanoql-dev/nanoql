// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/multiassetoption.hpp ql/instruments/multiassetoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>An option on several assets.</summary>
/// <remarks>
/// An extension point, as in QuantLib: the basket, Margrabe and two-asset correlation options derive from it, and so do
/// the experimental Everest, Himalaya and Pagoda options. The option holds its payoff and exercise and is immutable;
/// its engines are static, each returning a <see cref="MultiAssetOptionResults"/>.
/// </remarks>
public abstract class MultiAssetOption : Option
{
    /// <summary>Initializes a new instance of the <see cref="MultiAssetOption"/> class.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    protected MultiAssetOption(Payoff payoff, Exercise exercise)
        : base(PayoffOf(payoff), exercise)
    {
    }

    /// <summary>Whether the option has expired: its last exercise date has occurred under the context.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the option has expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise has no date.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(Exercise.LastDate()).HasOccurred(context);
    }

    /// <inheritdoc/>
    /// <remarks>An expired option's NPV and Greeks are all 0, as QuantLib's.</remarks>
    public override MultiAssetOptionResults SetupExpired()
    {
        InstrumentResults expired = base.SetupExpired();
        return new MultiAssetOptionResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
            Delta = 0.0,
            Gamma = 0.0,
            Theta = 0.0,
            Vega = 0.0,
            Rho = 0.0,
            DividendRho = 0.0,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The results aren't a multi-asset option's.</exception>
    public override MultiAssetOptionResults FetchResults(InstrumentResults results)
    {
        InstrumentResults fetched = base.FetchResults(results);
        if (fetched is not MultiAssetOptionResults r)
        {
            throw new ArgumentException("no greeks returned from pricing engine", nameof(results));
        }

        return r;
    }

    private static Payoff PayoffOf(Payoff payoff)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return payoff;
    }
}
