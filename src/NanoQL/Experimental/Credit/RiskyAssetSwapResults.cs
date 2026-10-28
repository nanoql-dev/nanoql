// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/riskyassetswap.hpp ql/experimental/credit/riskyassetswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The results of a <see cref="RiskyAssetSwap"/>'s calculation.</summary>
[Experimental("NQL0001")]
public sealed record RiskyAssetSwapResults : InstrumentResults
{
    /// <summary>Gets the spread at which the package is worth par; <see langword="null"/> for an expired swap.</summary>
    public double? FairSpread { get; init; }
}
