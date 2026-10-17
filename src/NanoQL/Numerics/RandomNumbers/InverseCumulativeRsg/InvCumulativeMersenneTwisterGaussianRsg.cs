// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/inversecumulativersg.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib-Python's <c>InvCumulativeMersenneTwisterGaussianRsg</c>: <see cref="InverseCumulativeRsg{TUsg, TIc}"/> over
/// <see cref="RandomSequenceGenerator{MersenneTwisterUniformRng}"/> and <see cref="InverseCumulativeNormal"/>, standard Gaussian pseudo-random
/// sequences.
/// </summary>
/// <remarks>
/// A convenience name: nanoql's own code uses the generic type. A mutable working type, as its base is.
/// </remarks>
public sealed class InvCumulativeMersenneTwisterGaussianRsg
    : InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvCumulativeMersenneTwisterGaussianRsg"/> class with the standard
    /// normal inverse cumulative, QuantLib's default construction.
    /// </summary>
    /// <param name="uniformSequenceGenerator">The uniform sequence generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformSequenceGenerator"/> is null.</exception>
    public InvCumulativeMersenneTwisterGaussianRsg(
        RandomSequenceGenerator<MersenneTwisterUniformRng> uniformSequenceGenerator)
        : base(uniformSequenceGenerator)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvCumulativeMersenneTwisterGaussianRsg"/> class.
    /// </summary>
    /// <param name="uniformSequenceGenerator">The uniform sequence generator, which the generator copies.</param>
    /// <param name="inverseCumulative">The inverse cumulative distribution.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformSequenceGenerator"/> is null.</exception>
    public InvCumulativeMersenneTwisterGaussianRsg(
        RandomSequenceGenerator<MersenneTwisterUniformRng> uniformSequenceGenerator,
        InverseCumulativeNormal inverseCumulative)
        : base(uniformSequenceGenerator, inverseCumulative)
    {
    }

    private InvCumulativeMersenneTwisterGaussianRsg(InvCumulativeMersenneTwisterGaussianRsg source)
        : base(source)
    {
    }

    /// <summary>
    /// Copies the generator deeply, its uniform sequence generator included: the copy's next draw equals this one's,
    /// into its own buffer, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override InvCumulativeMersenneTwisterGaussianRsg Clone() => new(this);
}
