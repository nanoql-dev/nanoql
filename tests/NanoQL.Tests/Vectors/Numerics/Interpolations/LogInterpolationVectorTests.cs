// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The log interpolations' vectors, <c>tests/vectors/math/interpolations/loginterpolation.json</c>:
/// <see cref="LogLinearInterpolation"/> and the six named log-cubic interpolations the wheel has, on ten node sets, the
/// value, derivative, second derivative and primitive (which throws) at 50 points inside and outside the range; then
/// each constructor's refusal of a y value of 0.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogInterpolationVectorTests
{
    [Fact]
    public void LogInterpolationVector()
    {
        var file = VectorFile.Load("math/interpolations/loginterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] x = InterpolationInputs.Numbers(c.Input("x")), y = InterpolationInputs.Numbers(c.Input("y"));
            string type = c.String("type");
            Interpolation Build() => InterpolationInputs.Build(type, x, y, c.Inputs);
            if (c.Expected.TryGetProperty("construction", out _))
            {
                double at = InterpolationInputs.Numbers(c.Input("points"))[0];
                check.Output(c, "construction", (Func<object?>)(() => Build().Evaluate(at)));
                continue;
            }

            InterpolationInputs.CheckMembers(check, c, Build);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"math/interpolations/loginterpolation: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }
}
