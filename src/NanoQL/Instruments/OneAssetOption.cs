// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/oneassetoption.hpp ql/instruments/oneassetoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>An option on a single asset.</summary>
/// <remarks>
/// An extension point, as in QuantLib: the vanilla option and the barrier, Asian, cliquet, compound, chooser and
/// other one-asset options derive from it. The option holds its payoff and exercise and is immutable; its engines are
/// static, each returning a <see cref="OneAssetOptionResults"/>.
/// </remarks>
public abstract class OneAssetOption : Option
{
    /// <summary>Initializes a new instance of the <see cref="OneAssetOption"/> class.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    protected OneAssetOption(Payoff payoff, Exercise exercise)
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
    /// <remarks>An expired option's Greeks are all 0, as QuantLib's.</remarks>
    public override OneAssetOptionResults SetupExpired()
    {
        InstrumentResults expired = base.SetupExpired();
        return new OneAssetOptionResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
            Delta = 0.0,
            DeltaForward = 0.0,
            Elasticity = 0.0,
            Gamma = 0.0,
            Theta = 0.0,
            ThetaPerDay = 0.0,
            Vega = 0.0,
            Rho = 0.0,
            DividendRho = 0.0,
            StrikeSensitivity = 0.0,
            ItmCashProbability = 0.0,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The results aren't a one-asset option's.</exception>
    public override OneAssetOptionResults FetchResults(InstrumentResults results)
    {
        InstrumentResults fetched = base.FetchResults(results);
        if (fetched is not OneAssetOptionResults r)
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
