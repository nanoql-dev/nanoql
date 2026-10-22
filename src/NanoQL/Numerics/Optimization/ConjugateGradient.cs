// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2009 Frédéric Degraeve
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/conjugategradient.hpp ql/math/optimization/conjugategradient.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The multi-dimensional conjugate gradient method.</summary>
/// <remarks>
/// <para>
/// Fletcher-Reeves-Polak-Ribiere algorithm adapted from Numerical Recipes in C, 2nd edition. The user has to provide a
/// line-search method and optimization end criteria. The search direction is dᵢ = −f′(xᵢ) + cᵢdᵢ₋₁, where
/// cᵢ = ‖f′(xᵢ)‖²/‖f′(xᵢ₋₁)‖² and d₁ = −f′(x₁). This optimization method requires the knowledge of the gradient of the
/// cost function.
/// </para>
/// <para>Immutable: each minimization runs on a new line search (<see cref="LineSearchBasedMethod"/>).</para>
/// </remarks>
public sealed class ConjugateGradient : LineSearchBasedMethod
{
    /// <summary>Initializes a new instance of the <see cref="ConjugateGradient"/> class.</summary>
    /// <param name="lineSearch">The line search, a prototype; <see langword="null"/> for an
    /// <see cref="ArmijoLineSearch"/> with its defaults.</param>
    public ConjugateGradient(LineSearch? lineSearch = null)
        : base(lineSearch)
    {
    }

    /// <inheritdoc/>
    protected override Vector GetUpdatedDirection(ProblemState P, double gold2, Vector gradient, LineSearch lineSearch)
    {
        ArgumentNullException.ThrowIfNull(P);
        ArgumentNullException.ThrowIfNull(lineSearch);
        return -lineSearch.LastGradient + (P.GradientNormValue!.Value / gold2 * lineSearch.SearchDirection);
    }
}
