// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/lossdistribution.hpp ql/experimental/credit/lossdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// A homogeneous or binomial loss distribution with its by-products: the members QuantLib's
/// <c>LossDistBinomial</c> and <c>LossDistHomogeneous</c> keep mutable and reset at each call (the S-xcr plan's XR5).
/// </summary>
/// <param name="Distribution">The loss distribution.</param>
/// <param name="Size">The number of names, QuantLib's <c>size()</c>.</param>
/// <param name="Volume">Each name's volume, QuantLib's <c>volume()</c>.</param>
/// <param name="Probability">The probability of each number of defaults, 0 to <paramref name="Size"/>.</param>
/// <param name="ExcessProbability">The probability of each number of defaults or more.</param>
[Experimental("NQL0001")]
public sealed record LossDistResult(
    Distribution Distribution,
    int Size,
    double Volume,
    ImmutableArray<double> Probability,
    ImmutableArray<double> ExcessProbability);
