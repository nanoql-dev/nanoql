// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's discrete-integral vectors, <c>tests/vectors/cxx/math/integrals/discreteintegrals.json</c>:
/// <see cref="DiscreteTrapezoidIntegral"/> and <see cref="DiscreteSimpsonIntegral"/> on sampled arrays, even and
/// uneven, with QuantLib's refusal of arrays of different sizes; <see cref="DiscreteTrapezoidIntegrator"/> and
/// <see cref="DiscreteSimpsonIntegrator"/> on named integrands, forward, reversed and empty, each value with its count
/// of evaluations. The wheel exposes none of them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscreteIntegralsCxxVectorTests
{
    [Fact]
    public void DiscreteIntegralsCxxVector()
    {
        var file = VectorFile.Load("cxx/math/integrals/discreteintegrals");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("DiscreteTrapezoidIntegral", out _))
            {
                double[] x = c.Doubles("x"), f = c.Doubles("f");
                check.Output(c, "DiscreteTrapezoidIntegral", (Func<object?>)(() => DiscreteTrapezoidIntegral.Integrate(x, f)));
                check.Output(c, "DiscreteSimpsonIntegral", (Func<object?>)(() => DiscreteSimpsonIntegral.Integrate(x, f)));
                continue;
            }

            Func<double, double> integrand = IntegralInputs.Integrands[c.String("integrand")];
            double a = c.Double("a"), b = c.Double("b");
            IntegrationResult[] trapezoid =
                [.. c.Ints("trapezoidEvaluations").Select(n => new DiscreteTrapezoidIntegrator(n).IntegrateWithStatistics(integrand, a, b))];
            IntegrationResult[] simpson =
                [.. c.Ints("simpsonEvaluations").Select(n => new DiscreteSimpsonIntegrator(n).IntegrateWithStatistics(integrand, a, b))];
            check.Output(c, "DiscreteTrapezoidIntegrator", trapezoid.Select(r => r.Value).ToArray());
            check.Output(c, "DiscreteTrapezoidIntegrator evaluations", trapezoid.Select(r => r.NumberOfEvaluations).ToArray());
            check.Output(c, "DiscreteSimpsonIntegrator", simpson.Select(r => r.Value).ToArray());
            check.Output(c, "DiscreteSimpsonIntegrator evaluations", simpson.Select(r => r.NumberOfEvaluations).ToArray());
        }

        check.Complete();
    }
}
