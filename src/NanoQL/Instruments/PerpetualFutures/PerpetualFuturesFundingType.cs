// SPDX-FileCopyrightText: Copyright (C) 2025 Hiroto Ogawa
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/perpetualfutures.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// A perpetual futures contract's funding type: the cash flow at t + 1, with x the spot, f the futures price, fr the
/// funding rate and i the interest-rate difference.
/// </summary>
public enum PerpetualFuturesFundingType
{
    /// <summary>f(t+1) − f(t) − fr(t)·(f(t) − x(t)) − i(t)·x(t).</summary>
    FundingWithPreviousSpot,

    /// <summary>f(t+1) − f(t) − fr(t)·x(t+1)·(f(t) − x(t))/x(t) − i(t)·x(t+1).</summary>
    FundingWithCurrentSpot,
}
