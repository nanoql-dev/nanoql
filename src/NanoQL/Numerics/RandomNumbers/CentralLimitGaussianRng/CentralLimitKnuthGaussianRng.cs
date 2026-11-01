// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/centrallimitgaussianrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib-Python's <c>CentralLimitKnuthGaussianRng</c>: <see cref="CLGaussianRng{TRng}"/> over
/// <see cref="KnuthUniformRng"/>, standard Gaussian deviates.
/// </summary>
/// <remarks>
/// A convenience name: nanoql's own code uses the generic type. A mutable working type, as its base is.
/// </remarks>
[WorkingType]
public sealed class CentralLimitKnuthGaussianRng : CLGaussianRng<KnuthUniformRng>
{
    /// <summary>Initializes a new instance of the <see cref="CentralLimitKnuthGaussianRng"/> class.</summary>
    /// <param name="uniformGenerator">The uniform generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformGenerator"/> is null.</exception>
    public CentralLimitKnuthGaussianRng(KnuthUniformRng uniformGenerator)
        : base(uniformGenerator)
    {
    }

    private CentralLimitKnuthGaussianRng(CentralLimitKnuthGaussianRng source)
        : base(source)
    {
    }

    /// <summary>
    /// Copies the generator deeply, its uniform generator included: the copy's next draw equals this one's, and the
    /// two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override CentralLimitKnuthGaussianRng Clone() => new(this);
}
