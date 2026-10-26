// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/basketoption.hpp ql/instruments/basketoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A basket option on several assets: a basket payoff and an exercise.</summary>
/// <remarks>Its engines are static classes, each returning a <see cref="MultiAssetOptionResults"/>.</remarks>
public sealed class BasketOption : MultiAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="BasketOption"/> class.</summary>
    /// <param name="payoff">The basket payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public BasketOption(BasketPayoff payoff, Exercise exercise)
        : base(payoff, exercise)
    {
    }
}
