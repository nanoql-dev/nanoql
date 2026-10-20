// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="FourierIntegration"/>'s vectors, <c>tests/vectors/math/integrals/fourierintegration.json</c>: the
/// Andersen-Piterbarg integration limit, whether each factory's integration is adaptive, the quadratures' numbers of
/// evaluations (their order, which <see cref="FourierIntegration.CalculateWithStatistics"/> returns with each value)
/// and the Laguerre factory's refusal of an order above 192.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FourierIntegrationVectorTests
{
    [Fact]
    public void FourierIntegrationVector()
    {
        var file = VectorFile.Load("math/integrals/fourierintegration");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("limits", out JsonElement limits))
            {
                double c_inf = c.Double("c_inf");
                object?[] values =
                [
                    .. limits.EnumerateArray().Select(l => (Func<object?>)(() => FourierIntegration.AndersenPiterbargIntegrationLimit(
                        c_inf, l.GetProperty("epsilon").GetDouble(), l.GetProperty("v0").GetDouble(), l.GetProperty("t").GetDouble()))),
                ];
                check.Output(c, "limit", values);
                continue;
            }

            string factory = c.String("factory");
            int? order = c.Inputs.TryGetProperty("order", out JsonElement o) ? o.GetInt32() : null;
            Func<FourierIntegration> make = () => factory switch
            {
                "gaussLaguerre" => FourierIntegration.GaussLaguerre(order!.Value),
                "gaussLegendre" => FourierIntegration.GaussLegendre(order!.Value),
                "gaussChebyshev" => FourierIntegration.GaussChebyshev(order!.Value),
                "gaussChebyshev2nd" => FourierIntegration.GaussChebyshev2nd(order!.Value),
                "gaussLobatto" => FourierIntegration.GaussLobatto(1e-8, 1e-8),
                "simpson" => FourierIntegration.Simpson(1e-8),
                "trapezoid" => FourierIntegration.Trapezoid(1e-8),
                _ => throw new ArgumentOutOfRangeException(nameof(c), factory, "unknown factory"),
            };

            if (c.Expected.TryGetProperty("isAdaptiveIntegration", out _))
            {
                check.Output(c, "isAdaptiveIntegration", make().IsAdaptiveIntegration);
            }

            if (c.Expected.TryGetProperty("numberOfEvaluations", out _))
            {
                // a quadrature's count, its order, whatever it integrates
                check.Output(
                    c, "numberOfEvaluations", (Func<object?>)(() => make().CalculateWithStatistics(1.0, x => 0.0).NumberOfEvaluations));
            }
        }

        check.Complete();
    }
}
