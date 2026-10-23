// SPDX-FileCopyrightText: Copyright (C) 2013, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/markovfunctional.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>The Markov-functional model's adjustments and smile pretreatments, flags combined with <c>|</c>.</summary>
/// <remarks><see cref="NoPayoffExtrapolation"/> overrides <see cref="ExtrapolatePayoffFlat"/>.</remarks>
[Flags]
public enum MarkovFunctionalModelSettingsAdjustments
{
    /// <summary>No adjustment.</summary>
    AdjustNone = 0,

    /// <summary>The digitals rescaled to the market annuity, experimental.</summary>
    AdjustDigitals = 1 << 0,

    /// <summary>The numeraire rescaled to the yield curve's zero bonds, experimental.</summary>
    AdjustYts = 1 << 1,

    /// <summary>The payoff extrapolated flat past the state grid.</summary>
    ExtrapolatePayoffFlat = 1 << 2,

    /// <summary>The payoff not extrapolated past the state grid.</summary>
    NoPayoffExtrapolation = 1 << 3,

    /// <summary>The input smile made arbitrage free by Kahale's method.</summary>
    KahaleSmile = 1 << 4,

    /// <summary>The smile's right wing extrapolated exponentially.</summary>
    SmileExponentialExtrapolation = 1 << 5,

    /// <summary>Kahale's interpolation inside the smile's arbitrage-free region.</summary>
    KahaleInterpolation = 1 << 6,

    /// <summary>The smile's arbitrageable points removed.</summary>
    SmileDeleteArbitragePoints = 1 << 7,

    /// <summary>A SABR smile fitted to the input smile, made arbitrage free by Kahale's method.</summary>
    SabrSmile = 1 << 8,

    /// <summary>A custom smile, from the settings' factory.</summary>
    CustomSmile = 1 << 9,
}
