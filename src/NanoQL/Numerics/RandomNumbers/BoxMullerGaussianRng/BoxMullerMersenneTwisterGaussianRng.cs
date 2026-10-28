// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/boxmullergaussianrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib-Python's <c>BoxMullerMersenneTwisterGaussianRng</c>: <see cref="BoxMullerGaussianRng{TRng}"/> over
/// <see cref="MersenneTwisterUniformRng"/>, standard Gaussian deviates.
/// </summary>
/// <remarks>
/// A convenience name: nanoql's own code uses the generic type. A mutable working type, as its base is.
/// </remarks>
public sealed class BoxMullerMersenneTwisterGaussianRng : BoxMullerGaussianRng<MersenneTwisterUniformRng>
{
    /// <summary>Initializes a new instance of the <see cref="BoxMullerMersenneTwisterGaussianRng"/> class.</summary>
    /// <param name="uniformGenerator">The uniform generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformGenerator"/> is null.</exception>
    public BoxMullerMersenneTwisterGaussianRng(MersenneTwisterUniformRng uniformGenerator)
        : base(uniformGenerator)
    {
    }

    private BoxMullerMersenneTwisterGaussianRng(BoxMullerMersenneTwisterGaussianRng source)
        : base(source)
    {
    }

    /// <summary>
    /// Copies the generator deeply, its uniform generator included: the copy's next draw equals this one's, and the
    /// two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override BoxMullerMersenneTwisterGaussianRng Clone() => new(this);
}
