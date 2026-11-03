// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/batesmodel.hpp ql/models/equity/batesmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The double-exponential models' constraint on the up-jumps' mean size: in the open interval (0, 1), where the
/// up-jumps' exponential moment is finite; QuantLib's is a positive constraint.
/// </summary>
internal sealed class BatesDoubleExpUpJumpConstraint : Constraint
{
    /// <inheritdoc/>
    public override bool Test(Vector p)
    {
        ArgumentNullException.ThrowIfNull(p);
        foreach (double x in p)
        {
            if (!(x > 0.0 && x < 1.0))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    protected override Vector UpperBoundCore(Vector parameters) => new(parameters.Size, 1.0);

    /// <inheritdoc/>
    protected override Vector LowerBoundCore(Vector parameters) => new(parameters.Size, 0.0);
}
