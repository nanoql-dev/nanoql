// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>A constant inertia: each velocity times the optimizer's inertia: QuantLib's <c>TrivialInertia</c>.</summary>
[Experimental("NQL0001")]
public sealed class TrivialInertia : ParticleSwarmOptimizationInertia
{
    private double c0;
    private int M;

    /// <inheritdoc/>
    public override void SetSize(int M, int N, double c0, EndCriteria endCriteria)
    {
        this.c0 = c0;
        this.M = M;
    }

    /// <inheritdoc/>
    public override void SetValues()
    {
        for (int i = 0; i < M; i++)
        {
            Vector v = V[i];
            v *= c0;
        }
    }

    /// <inheritdoc/>
    public override ParticleSwarmOptimizationInertia Clone() => new TrivialInertia();
}
