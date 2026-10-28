// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/inversecumulativerng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib-Python's <c>MoroInvCumulativeXoshiro256StarStarGaussianRng</c>:
/// <see cref="InverseCumulativeRng{TRng, TIc}"/> over <see cref="Xoshiro256StarStarUniformRng"/> and
/// <see cref="MoroInverseCumulativeNormal"/>, standard Gaussian deviates.
/// </summary>
/// <remarks>
/// A convenience name: nanoql's own code uses the generic type. A mutable working type, as its base is.
/// </remarks>
public sealed class MoroInvCumulativeXoshiro256StarStarGaussianRng
    : InverseCumulativeRng<Xoshiro256StarStarUniformRng, MoroInverseCumulativeNormal>
{
    /// <summary>Initializes a new instance of the <see cref="MoroInvCumulativeXoshiro256StarStarGaussianRng"/>
    /// class.</summary>
    /// <param name="uniformGenerator">The uniform generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformGenerator"/> is null.</exception>
    public MoroInvCumulativeXoshiro256StarStarGaussianRng(Xoshiro256StarStarUniformRng uniformGenerator)
        : base(uniformGenerator)
    {
    }

    private MoroInvCumulativeXoshiro256StarStarGaussianRng(MoroInvCumulativeXoshiro256StarStarGaussianRng source)
        : base(source)
    {
    }

    /// <summary>
    /// Copies the generator deeply, its uniform generator included: the copy's next draw equals this one's, and the
    /// two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override MoroInvCumulativeXoshiro256StarStarGaussianRng Clone() => new(this);
}
