// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's complex Runge-Kutta vectors, <c>tests/vectors/cxx/math/ode/adaptiverungekutta.json</c>:
/// <see cref="AdaptiveRungeKutta{T}"/> over <see cref="Complex"/>, the solutions of the named complex equations
/// (<see cref="OdeEquations"/>) in the scalar and the vector form under four settings, backward and of length zero,
/// and its failures: a step at or below the smallest allowed, and too many steps. A complex value is compared as its
/// real and imaginary parts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AdaptiveRungeKuttaCxxVectorTests
{
    [Fact]
    public void AdaptiveRungeKuttaCxxVector()
    {
        var file = VectorFile.Load("cxx/math/ode/adaptiverungekutta");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] settings = Numbers(c.Input("integrator"));
            var rk = new AdaptiveRungeKutta<Complex>(settings[0], settings[1], settings[2]);
            string equation = c.String("equation");
            double x1 = Number(c.Input("x1")), x2 = Number(c.Input("x2"));
            if (c.Bool("vector"))
            {
                Complex[] y1 = [.. c.Input("y1").EnumerateArray().Select(ComplexOf)];
                check.Output(
                    c,
                    "y",
                    () => rk.Evaluate(OdeEquations.ComplexVector(equation), y1, x1, x2).Select(z => Parts(z)).ToArray());
            }
            else
            {
                Complex y1 = ComplexOf(c.Input("y1"));
                check.Output(c, "y", () => Parts(rk.Evaluate(OdeEquations.ComplexScalar(equation), y1, x1, x2)));
            }
        }

        check.Complete();
    }

    private static Complex ComplexOf(JsonElement e) => new(Number(e[0]), Number(e[1]));

    private static double[] Parts(Complex z) => [z.Real, z.Imaginary];
}
