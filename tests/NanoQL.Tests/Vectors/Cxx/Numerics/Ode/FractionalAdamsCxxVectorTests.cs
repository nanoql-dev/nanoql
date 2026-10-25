// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's fractional Adams vectors, <c>tests/vectors/cxx/math/ode/fractionaladams.json</c>: the scheme over
/// <see cref="double"/> and <see cref="Complex"/> on the harness's named equations, and the Riemann-Liouville weights'
/// integral and the integral function on real and complex grids.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FractionalAdamsCxxVectorTests
{
    [Fact]
    public void FractionalAdamsCxxVector()
    {
        var file = VectorFile.Load("cxx/math/ode/fractionaladams");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double alpha = c.Double("alpha");
            if (c.Input("kind").GetString() == "scheme")
            {
                string equation = c.Input("equation").GetString()!;
                double tMax = c.Double("tMax");
                int steps = c.Input("steps").GetInt32();
                check.Output(c, "y", () => Solve(alpha, equation, c.Input("y0"), tMax, steps));
                continue;
            }

            JsonElement y = c.Input("y");
            double dt = c.Double("dt");
            var weights = new RiemannLiouvilleWeights(alpha, c.Input("steps").GetInt32());
            if (y[0].ValueKind == JsonValueKind.Array)
            {
                Complex[] values = [.. y.EnumerateArray().Select(AnalyticPTDHestonEngineCxxVectorTests.ComplexOf)];
                check.Output(c, "weights", () => COSHestonEngineCxxVectorTests.Parts(weights.Integrate<Complex>(values, dt)));
                check.Output(
                    c, "integral", () => COSHestonEngineCxxVectorTests.Parts(FractionalAdamsFunctions.RiemannLiouvilleIntegral<Complex>(values, alpha, dt)));
            }
            else
            {
                double[] values = Doubles(y);
                check.Output(c, "weights", () => weights.Integrate<double>(values, dt));
                check.Output(c, "integral", () => FractionalAdamsFunctions.RiemannLiouvilleIntegral<double>(values, alpha, dt));
            }
        }

        check.Complete();
    }

    // The harness's named equations (tools/cxx/src/math/ode/fractionaladams.cpp), the results as the vectors write them
    private static object Solve(double alpha, string equation, JsonElement y0, double tMax, int steps)
    {
        switch (equation)
        {
            case "power":
            {
                double g = new GammaFunction().Value(5.0) / new GammaFunction().Value(5.0 - alpha);
                return new FractionalAdams<double>(alpha).Solve((t, y) => (g * Math.Pow(t, 4 - alpha)) + (t * t * t * t) - y, y0.GetDouble(), tMax, steps);
            }

            case "linear":
                return new FractionalAdams<double>(alpha).Solve((_, y) => -y, y0.GetDouble(), tMax, steps);
            case "rotation":
                return Parts(new FractionalAdams<Complex>(alpha).Solve(
                    (_, y) => new Complex(0.0, 1.5) * y, AnalyticPTDHestonEngineCxxVectorTests.ComplexOf(y0), tMax, steps));
            default:
            {
                // the rough Heston engine's Riccati coefficients at z = 2 - 0.5i, kappa 0.3, sigma 0.4, rho -0.7
                var z = new Complex(2.0, -0.5);
                var i = new Complex(0.0, 1.0);
                const double Kappa = 0.3, Sigma = 0.4, Rho = -0.7;
                Complex c0 = -0.5 * z * (z + i);
                Complex c1 = (i * (Rho * Sigma) * z) - Kappa;
                var c2 = new Complex(0.5 * Sigma * Sigma, 0.0);
                return Parts(new FractionalAdams<Complex>(alpha).Solve(
                    (_, y) => c0 + ((c1 + (c2 * y)) * y), AnalyticPTDHestonEngineCxxVectorTests.ComplexOf(y0), tMax, steps));
            }
        }
    }

    private static double[][] Parts(Complex[] values) => [.. values.Select(COSHestonEngineCxxVectorTests.Parts)];
}
