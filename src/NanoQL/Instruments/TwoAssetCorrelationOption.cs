// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/twoassetcorrelationoption.hpp ql/instruments/twoassetcorrelationoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// A two-asset correlation option: the second asset's plain payoff at its own strike, paid only if the first asset is
/// in the money at its strike, both of the option's type.
/// </summary>
/// <remarks>Immutable; its payoff is the first asset's plain vanilla payoff at the first strike.</remarks>
public sealed class TwoAssetCorrelationOption : MultiAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="TwoAssetCorrelationOption"/> class.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike1">The first asset's strike, X₁.</param>
    /// <param name="strike2">The second asset's strike, X₂.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exercise"/> is <see langword="null"/>.</exception>
    public TwoAssetCorrelationOption(OptionType type, double strike1, double strike2, Exercise exercise)
        : base(new PlainVanillaPayoff(type, strike1), exercise)
    {
        X2 = strike2;
    }

    /// <summary>Gets the second asset's strike, X₂.</summary>
    public double X2 { get; }
}
