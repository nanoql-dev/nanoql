// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/europeanoption.hpp ql/instruments/europeanoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A European option on a single asset: a vanilla option with, in practice, a European exercise.</summary>
public sealed class EuropeanOption : VanillaOption
{
    /// <summary>Initializes a new instance of the <see cref="EuropeanOption"/> class.</summary>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public EuropeanOption(StrikedTypePayoff payoff, Exercise exercise)
        : base(payoff, exercise)
    {
    }
}
