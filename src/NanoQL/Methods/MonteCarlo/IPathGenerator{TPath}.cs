// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/montecarlomodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// A path generator: what QuantLib's Monte Carlo model draws its paths from, its traits' <c>path_generator_type</c>
/// (<see cref="PathGenerator{TGsg}"/> for one-dimensional paths, <see cref="MultiPathGenerator{TGsg}"/> for several).
/// </summary>
/// <typeparam name="TPath">The path type.</typeparam>
/// <remarks>
/// A generator is a working type (S2R2): each draw changes it, and the sample's value is a view of its own buffer,
/// valid until its next draw.
/// </remarks>
[WorkingType]
public interface IPathGenerator<TPath>
{
    /// <summary>Draws the next path.</summary>
    /// <returns>The path and its weight.</returns>
    Sample<TPath> Next();

    /// <summary>The antithetic of the last path drawn: the same draws, negated.</summary>
    /// <returns>The path and its weight.</returns>
    Sample<TPath> Antithetic();
}
