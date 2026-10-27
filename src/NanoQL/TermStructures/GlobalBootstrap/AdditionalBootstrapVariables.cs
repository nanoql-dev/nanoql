// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2025 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/globalbootstrap.hpp ql/termstructures/globalbootstrap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// Variables a global bootstrap optimizes beside the curve's nodes, such as model parameters its helpers read (a
/// futures helper's convexity adjustment): QuantLib's <c>AdditionalBootstrapVariables</c>.
/// </summary>
/// <remarks>
/// <para>
/// An extension point (U13), immutable: <see cref="Initialize"/> gives the optimizer's guesses for the variables, and
/// <see cref="Values"/> maps the optimizer's argument to the variables' values, which
/// <see cref="GlobalBootstrap.VariableHelpers"/> builds its helpers from at each evaluation (the S-core plan's CR3 (a),
/// (b)). QuantLib's <c>update</c> writes the values into quotes the helpers read; here it returns them.
/// </para>
/// </remarks>
public abstract class AdditionalBootstrapVariables
{
    /// <summary>The optimizer's guesses for the variables, one per variable: QuantLib's <c>initialize</c>.</summary>
    /// <param name="validData">Whether the curve holds a previous bootstrap's data, which a nanoql curve, always built
    /// afresh, never does: its bootstrap passes <see langword="false"/>.</param>
    /// <returns>The guesses, in the optimizer's unconstrained form.</returns>
    public abstract Vector Initialize(bool validData);

    /// <summary>The variables' values from the optimizer's argument: QuantLib's <c>update</c>.</summary>
    /// <param name="x">The optimizer's argument for the variables, one value per variable.</param>
    /// <returns>The values.</returns>
    public abstract ImmutableArray<double> Values(ReadOnlySpan<double> x);
}
