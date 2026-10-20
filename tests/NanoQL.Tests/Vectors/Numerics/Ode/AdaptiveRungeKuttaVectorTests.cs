// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Runge-Kutta vectors, <c>tests/vectors/math/ode/adaptiverungekutta.json</c>: <see cref="RungeKutta"/>'s solutions
/// of scalar and vector equations (<see cref="OdeEquations"/>) under four settings, the default constructor's among
/// them, backward and of length zero, and its failures: a step at or below the smallest allowed, too many steps, and a
/// step size underflowing.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AdaptiveRungeKuttaVectorTests
{
    [Fact]
    public void AdaptiveRungeKuttaVector()
    {
        var file = VectorFile.Load("math/ode/adaptiverungekutta");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement settings = c.Input("integrator");
            RungeKutta rk = settings.ValueKind == JsonValueKind.Null
                ? new RungeKutta()
                : new RungeKutta(Number(settings[0]), Number(settings[1]), Number(settings[2]));
            string equation = c.String("equation");
            double x1 = Number(c.Input("x1")), x2 = Number(c.Input("x2"));
            if (OdeEquations.IsVector(equation))
            {
                double[] y1 = Numbers(c.Input("y1"));
                check.Output(c, "y", () => rk.Evaluate(OdeEquations.Vector(equation), y1, x1, x2));
            }
            else
            {
                double y1 = Number(c.Input("y1"));
                check.Output(c, "y", () => rk.Evaluate(OdeEquations.Scalar(equation), y1, x1, x2));
            }
        }

        check.Complete();
    }
}
