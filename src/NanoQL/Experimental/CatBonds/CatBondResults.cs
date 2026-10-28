// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/catbond.hpp ql/experimental/catbonds/catbond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>The results of a cat bond's calculation: the bond's, with the simulated loss statistics.</summary>
[Experimental("NQL0001")]
public sealed record CatBondResults : BondResults
{
    /// <summary>Gets the probability of a loss of notional; <see langword="null"/> for an expired bond.</summary>
    public double? LossProbability { get; init; }

    /// <summary>Gets the probability of losing the whole notional; <see langword="null"/> for an expired
    /// bond.</summary>
    public double? ExhaustionProbability { get; init; }

    /// <summary>Gets the expected loss, a fraction of the notional; <see langword="null"/> for an expired
    /// bond.</summary>
    public double? ExpectedLoss { get; init; }
}
