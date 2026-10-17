// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/costfunction.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A cost function whose values a function gives, with every other member <see cref="CostFunction"/>'s default.
/// </summary>
/// <remarks>
/// QuantLib's <c>SimpleCostFunction&lt;ValuesFn&gt;</c> is a template over the callable, which its callers give as a
/// lambda; here the callable is a delegate, so the class isn't generic.
/// </remarks>
public sealed class SimpleCostFunction : CostFunction
{
    private readonly Func<Vector, Vector> values;

    /// <summary>Initializes a new instance of the <see cref="SimpleCostFunction"/> class.</summary>
    /// <param name="values">The function giving the values at a point, as a new vector.</param>
    public SimpleCostFunction(Func<Vector, Vector> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        this.values = values;
    }

    /// <inheritdoc/>
    public override Vector Values(Vector x) => values(x);
}
