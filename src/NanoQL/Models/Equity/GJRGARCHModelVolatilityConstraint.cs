// SPDX-FileCopyrightText: Copyright (C) 2008 Yee Man Chan
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/gjrgarchmodel.hpp ql/models/equity/gjrgarchmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The constraint β + γ ≥ 0 on a GJR-GARCH model's parameters, read from β and γ, the third and fourth of
/// <see cref="GJRGARCHModel"/>'s: QuantLib's <c>GJRGARCHModel::VolatilityConstraint</c>.
/// </summary>
public sealed class GJRGARCHModelVolatilityConstraint : Constraint
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The parameters are fewer than four, which QuantLib reads past.</exception>
    public override bool Test(Vector p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.Size < 4)
        {
            throw new ArgumentException("the volatility constraint reads four parameters", nameof(p));
        }

        double beta = p[2];
        double gamma = p[3];
        return beta + gamma >= 0.0;
    }
}
