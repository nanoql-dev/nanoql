// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Francois Botha
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/lookbackoption.hpp ql/instruments/lookbackoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// A continuous fixed-strike lookback option: a call pays the maximum the underlying reaches over the option's life
/// less the strike, a put the strike less the minimum.
/// </summary>
/// <remarks>
/// Immutable. Unsealed: <see cref="ContinuousPartialFixedLookbackOption"/> derives from it, as in QuantLib; an engine
/// for the full-period option refuses the partial one held as this type, QuantLib's "wrong argument type" (the S-bar
/// plan's SD8).
/// </remarks>
public class ContinuousFixedLookbackOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="ContinuousFixedLookbackOption"/> class.</summary>
    /// <param name="minmax">The extremum observed so far: the maximum for a call, the minimum for a put.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public ContinuousFixedLookbackOption(double minmax, StrikedTypePayoff payoff, Exercise exercise)
        : base(Checked(payoff), exercise)
    {
        Minmax = minmax;
    }

    /// <summary>Gets the extremum observed so far.</summary>
    public double Minmax { get; }

    /// <summary>Checks the option's terms before an engine prices it: QuantLib's <c>arguments::validate</c>.</summary>
    /// <exception cref="NanoQLException">The extremum is negative or not a number.</exception>
    public override void Validate()
    {
        base.Validate();

        // QuantLib's "null prior extremum" can't fail: the extremum isn't nullable
        if (!(Minmax >= 0.0))
        {
            throw new NanoQLException($"nonnegative prior extremum required: {Minmax.ToString(System.Globalization.CultureInfo.InvariantCulture)} not allowed");
        }
    }

    private static StrikedTypePayoff Checked(StrikedTypePayoff payoff)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return payoff;
    }
}
