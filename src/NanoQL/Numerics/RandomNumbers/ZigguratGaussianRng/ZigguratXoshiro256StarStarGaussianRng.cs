// SPDX-FileCopyrightText: Copyright (C) 2024 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/zigguratgaussianrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib-Python's <c>ZigguratXoshiro256StarStarGaussianRng</c>: <see cref="ZigguratGaussianRng{TRng}"/> over
/// <see cref="Xoshiro256StarStarUniformRng"/>, standard Gaussian deviates.
/// </summary>
/// <remarks>
/// A convenience name: nanoql's own code uses the generic type. A mutable working type, as its base is.
/// </remarks>
public sealed class ZigguratXoshiro256StarStarGaussianRng : ZigguratGaussianRng<Xoshiro256StarStarUniformRng>
{
    /// <summary>Initializes a new instance of the <see cref="ZigguratXoshiro256StarStarGaussianRng"/> class.</summary>
    /// <param name="uint64Generator">The generator, which the Gaussian generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uint64Generator"/> is null.</exception>
    public ZigguratXoshiro256StarStarGaussianRng(Xoshiro256StarStarUniformRng uint64Generator)
        : base(uint64Generator)
    {
    }

    private ZigguratXoshiro256StarStarGaussianRng(ZigguratXoshiro256StarStarGaussianRng source)
        : base(source)
    {
    }

    /// <summary>
    /// Copies the generator deeply, its uniform generator included: the copy's next draw equals this one's, and the
    /// two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override ZigguratXoshiro256StarStarGaussianRng Clone() => new(this);
}
