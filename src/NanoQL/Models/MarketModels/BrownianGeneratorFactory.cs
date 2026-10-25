// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/browniangenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>A factory of <see cref="BrownianGenerator"/>s.</summary>
/// <remarks>
/// Immutable (the S-hes plan's HR3 (iv)): <see cref="Create"/> returns a new generator at each call, so the
/// computations that draw from them share nothing. An extension point, as in QuantLib.
/// </remarks>
public abstract class BrownianGeneratorFactory
{
    /// <summary>Initializes a new instance of the <see cref="BrownianGeneratorFactory"/> class.</summary>
    protected BrownianGeneratorFactory()
    {
    }

    /// <summary>A new generator.</summary>
    /// <param name="factors">The number of factors each step draws.</param>
    /// <param name="steps">The number of steps each path draws.</param>
    /// <returns>A new generator, starting before its first path.</returns>
    public abstract BrownianGenerator Create(int factors, int steps);
}
