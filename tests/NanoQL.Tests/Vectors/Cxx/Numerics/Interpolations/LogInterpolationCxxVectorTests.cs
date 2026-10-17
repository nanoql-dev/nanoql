// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The log-cubic harness vectors, <c>tests/vectors/cxx/math/interpolations/loginterpolation.json</c>:
/// <see cref="LogCubicInterpolation"/>'s constructor over the derivative approximations and some boundary conditions,
/// and <see cref="HarmonicLogCubic"/>, with the value, derivatives and primitive (which throws), and their refusal of a
/// y value of 0.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogInterpolationCxxVectorTests
{
    [Fact]
    public void LogInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/loginterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            InterpolationInputs.CheckMembers(check, c, () => InterpolationInputs.Build(c.Input("interpolation")));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"cxx/math/interpolations/loginterpolation: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }
}
