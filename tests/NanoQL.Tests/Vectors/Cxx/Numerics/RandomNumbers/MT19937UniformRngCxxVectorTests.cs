// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Mersenne Twister vectors, <c>tests/vectors/cxx/math/randomnumbers/mt19937uniformrng.json</c>:
/// <see cref="MersenneTwisterUniformRng.NextInt32"/>, which the wheel doesn't expose, 700 draws each for six seeds
/// across the full 32-bit range and for five keys of the constructor from a key.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MT19937UniformRngCxxVectorTests
{
    [Fact]
    public void MT19937UniformRngCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/mt19937uniformrng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MersenneTwisterUniformRng rng = c.Inputs.TryGetProperty("seed", out var seed)
                ? new MersenneTwisterUniformRng(seed.GetInt64())
                : new MersenneTwisterUniformRng([.. c.Input("seeds").EnumerateArray().Select(w => w.GetInt64())]);
            check.Output(c, "nextInt32", Enumerable.Range(0, c.Int("draws")).Select(_ => rng.NextInt32()).ToArray());
        }

        check.Complete();
    }
}
