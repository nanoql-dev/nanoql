// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>
/// A user's cost function whose Values writes into its argument, where nanoql's reads it: the user's override doesn't
/// change the form of nanoql's own functions, and is listed with its write lost through them.
/// </summary>
public sealed class WritingCostFunction : CostFunction
{
    /// <inheritdoc/>
    public override Vector Values(Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        x[0] = 1;
        return x.Clone();
    }
}
