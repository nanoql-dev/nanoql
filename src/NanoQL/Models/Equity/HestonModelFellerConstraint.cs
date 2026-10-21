// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/hestonmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The Feller constraint on a Heston model's parameters, σ ≥ 0 and σ² &lt; 2κθ, read from θ, κ and σ, the first three
/// of <see cref="HestonModel"/>'s: QuantLib's <c>HestonModel::FellerConstraint</c>.
/// </summary>
public sealed class HestonModelFellerConstraint : Constraint
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The parameters are fewer than three, which QuantLib reads past.</exception>
    public override bool Test(Vector p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.Size < 3)
        {
            throw new ArgumentException("the Feller constraint reads three parameters", nameof(p));
        }

        double theta = p[0];
        double kappa = p[1];
        double sigma = p[2];
        return sigma >= 0.0 && sigma * sigma < 2.0 * kappa * theta;
    }
}
