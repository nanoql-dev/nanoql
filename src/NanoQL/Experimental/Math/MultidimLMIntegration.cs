// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/latentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A latent model's integration by one-dimensional integrators over a box [a, b] in every dimension: QuantLib's
/// <c>IntegrationBase&lt;MultidimIntegral&gt;</c>, scalar only.
/// </summary>
[Experimental("NQL0001")]
public sealed class MultidimLMIntegration : LMIntegration
{
    private readonly MultidimIntegral integral;
    private readonly ImmutableArray<double> a;
    private readonly ImmutableArray<double> b;

    /// <summary>Initializes a new instance of the <see cref="MultidimLMIntegration"/> class.</summary>
    /// <param name="integrators">One integrator per systemic factor.</param>
    /// <param name="a">Every dimension's lower limit.</param>
    /// <param name="b">Every dimension's upper limit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="integrators"/> or an element is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">As <see cref="MultidimIntegral"/>'s constructor.</exception>
    public MultidimLMIntegration(IReadOnlyCollection<Integrator> integrators, double a, double b)
    {
        ArgumentNullException.ThrowIfNull(integrators);
        integral = new MultidimIntegral(integrators);
        this.a = [.. Enumerable.Repeat(a, integrators.Count)];
        this.b = [.. Enumerable.Repeat(b, integrators.Count)];
    }

    /// <inheritdoc/>
    public override double Integrate(MultidimIntegrand f) => integral.Integrate(f, a.AsSpan(), b.AsSpan());
}
