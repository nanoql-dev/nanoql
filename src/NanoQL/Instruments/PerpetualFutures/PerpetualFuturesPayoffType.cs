// SPDX-FileCopyrightText: Copyright (C) 2025 Hiroto Ogawa
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/perpetualfutures.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A perpetual futures contract's payoff type, on an exchange rate FOR/DOM.</summary>
public enum PerpetualFuturesPayoffType
{
    /// <summary>Margined and settled in DOM.</summary>
    Linear,

    /// <summary>Margined and settled in FOR.</summary>
    Inverse,

    /// <summary>Margined and settled in a third, quanto currency.</summary>
    Quanto,
}
