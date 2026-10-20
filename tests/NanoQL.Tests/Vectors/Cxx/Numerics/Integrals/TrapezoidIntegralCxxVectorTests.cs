// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's trapezoid vectors, <c>tests/vectors/cxx/math/integrals/trapezoidintegral.json</c>: the iterates of
/// <see cref="Default"/>'s and <see cref="MidPoint"/>'s refinements, which the wheel doesn't expose, each refinement's
/// estimate and count given, and each rule's number of evaluations a refinement multiplies the count by.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TrapezoidIntegralCxxVectorTests
{
    [Fact]
    public void TrapezoidIntegralCxxVector()
    {
        var file = VectorFile.Load("cxx/math/integrals/trapezoidintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            bool midPoint = c.String("rule") == "MidPoint";
            if (c.Expected.TryGetProperty("nbEvalutions", out _))
            {
                check.Output(
                    c, "nbEvalutions", (Func<object?>)(() => midPoint ? MidPoint.NbEvalutions() : Default.NbEvalutions()));
                continue;
            }

            Func<double, double> f = IntegralInputs.Integrands[c.String("f")];
            double a = c.Double("a"), b = c.Double("b");
            double[] I = c.Doubles("I");
            int[] N = c.Ints("N");
            object?[] iterates = [.. Enumerable.Range(0, I.Length).Select(k => (object?)(Func<object?>)(() =>
                midPoint ? MidPoint.Integrate(f, a, b, I[k], N[k]) : Default.Integrate(f, a, b, I[k], N[k])))];
            check.Output(c, "integrate", iterates);
        }

        check.Complete();
    }
}
