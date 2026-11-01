// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/boxmullergaussianrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib-Python's <c>BoxMullerKnuthGaussianRng</c>: <see cref="BoxMullerGaussianRng{TRng}"/> over
/// <see cref="KnuthUniformRng"/>, standard Gaussian deviates.
/// </summary>
/// <remarks>
/// A convenience name: nanoql's own code uses the generic type. A mutable working type, as its base is.
/// </remarks>
[WorkingType]
public sealed class BoxMullerKnuthGaussianRng : BoxMullerGaussianRng<KnuthUniformRng>
{
    /// <summary>Initializes a new instance of the <see cref="BoxMullerKnuthGaussianRng"/> class.</summary>
    /// <param name="uniformGenerator">The uniform generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformGenerator"/> is null.</exception>
    public BoxMullerKnuthGaussianRng(KnuthUniformRng uniformGenerator)
        : base(uniformGenerator)
    {
    }

    private BoxMullerKnuthGaussianRng(BoxMullerKnuthGaussianRng source)
        : base(source)
    {
    }

    /// <summary>
    /// Copies the generator deeply, its uniform generator included: the copy's next draw equals this one's, and the
    /// two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override BoxMullerKnuthGaussianRng Clone() => new(this);
}
