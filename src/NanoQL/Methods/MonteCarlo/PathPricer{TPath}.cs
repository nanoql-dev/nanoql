// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/pathpricer.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The base class for path pricers: the value of an option on a given path.</summary>
/// <typeparam name="TPath">The path type.</typeparam>
/// <remarks>An extension point, as in QuantLib: each Monte Carlo engine's pricer derives from it.</remarks>
public abstract class PathPricer<TPath>
{
    /// <summary>Initializes a new instance of the <see cref="PathPricer{TPath}"/> class.</summary>
    protected PathPricer()
    {
    }

    /// <summary>The value on a path: QuantLib's <c>operator()</c>.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The value.</returns>
    public abstract double Evaluate(TPath path);
}
