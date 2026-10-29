// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealingfunctors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>No reannealing: QuantLib's <c>ReannealingTrivial</c>.</summary>
[Experimental("NQL0001")]
public sealed class ReannealingTrivial : IAnnealingReannealing<ReannealingTrivial>
{
    /// <inheritdoc/>
    public void SetProblem(ProblemState P)
    {
    }

    /// <inheritdoc/>
    public void Reanneal(Vector steps, Vector currentPoint, double aCurrentValue, Vector currTemp)
    {
    }

    /// <inheritdoc/>
    /// <remarks>Stateless: the reannealing itself.</remarks>
    public ReannealingTrivial Clone() => this;
}
