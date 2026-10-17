// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/costfunction.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A transformation of an optimization's parameters, and its inverse: an extension point, as in QuantLib, for the
/// calibrations that optimize over transformed parameters.
/// </summary>
public abstract class ParametersTransformation
{
    /// <summary>The transformed parameters.</summary>
    /// <param name="x">The parameters.</param>
    /// <returns>A new vector.</returns>
    public abstract Vector Direct(Vector x);

    /// <summary>The parameters whose transformation is <paramref name="x"/>.</summary>
    /// <param name="x">The transformed parameters.</param>
    /// <returns>A new vector.</returns>
    public abstract Vector Inverse(Vector x);
}
