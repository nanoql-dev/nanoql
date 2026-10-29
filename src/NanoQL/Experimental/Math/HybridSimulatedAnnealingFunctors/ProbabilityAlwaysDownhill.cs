// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealingfunctors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A probability that accepts only the points that improve on the current one: depending on the problem, the
/// optimizer is then very unlikely to escape a local optimum: QuantLib's <c>ProbabilityAlwaysDownhill</c>.
/// </summary>
[Experimental("NQL0001")]
public sealed class ProbabilityAlwaysDownhill : IAnnealingProbability<ProbabilityAlwaysDownhill>
{
    /// <inheritdoc/>
    public bool Accept(double currentValue, double newValue, Vector temp) => currentValue > newValue;

    /// <inheritdoc/>
    public ProbabilityAlwaysDownhill Clone() => this;
}
