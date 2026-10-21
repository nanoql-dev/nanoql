// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/varianceswap.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The results of a <see cref="VarianceSwap"/>'s engine: the instrument's results and the fair
/// variance.</summary>
public sealed record VarianceSwapResults : InstrumentResults
{
    /// <summary>Gets the fair variance: the strike at which the swap is worth nothing; <see langword="null"/> where the
    /// engine doesn't provide it, or the swap has expired.</summary>
    public double? Variance { get; init; }
}
