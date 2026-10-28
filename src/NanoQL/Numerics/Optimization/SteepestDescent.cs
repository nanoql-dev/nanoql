// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2009 Frédéric Degraeve
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/steepestdescent.hpp ql/math/optimization/steepestdescent.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The multi-dimensional steepest-descent method: the search direction is −f′(x).</summary>
/// <remarks>
/// The user has to provide a line-search method and optimization end criteria. Immutable: each minimization runs on a
/// new line search (<see cref="LineSearchBasedMethod"/>).
/// </remarks>
public sealed class SteepestDescent : LineSearchBasedMethod
{
    /// <summary>Initializes a new instance of the <see cref="SteepestDescent"/> class.</summary>
    /// <param name="lineSearch">The line search, a prototype; <see langword="null"/> for an
    /// <see cref="ArmijoLineSearch"/> with its defaults.</param>
    public SteepestDescent(LineSearch? lineSearch = null)
        : base(lineSearch)
    {
    }

    /// <inheritdoc/>
    protected override Vector GetUpdatedDirection(ProblemState P, double gold2, Vector gradient, LineSearch lineSearch)
    {
        ArgumentNullException.ThrowIfNull(lineSearch);
        return -lineSearch.LastGradient;
    }
}
