// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The model parameter vectors, <c>tests/vectors/models/parameter.json</c>: <see cref="ConstantParameter"/> from a
/// value and from a constraint alone, <see cref="NullParameter"/> and <see cref="PiecewiseConstantParameter"/>, under
/// the five constraints (<see cref="OptimizationSpecs.Constraint"/>): whether each builds, and after the case's
/// <see cref="Parameter.SetParam"/> calls in order (each a copy, where QuantLib's sets in place)
/// <see cref="Parameter.Size"/>, <see cref="Parameter.Params"/>, <see cref="Parameter.Evaluate"/> at the case's times
/// and <see cref="Parameter.TestParams"/> at its arrays.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ParameterVectorTests
{
    [Fact]
    public void ParameterVector()
    {
        var file = VectorFile.Load("models/parameter");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("parameter");
            Parameter? built = null;
            check.Output(c, "built", () =>
            {
                built = ParameterOf(spec);
                return true;
            });
            if (built is null)
            {
                continue;
            }

            Parameter p = built;
            foreach (JsonElement set in c.Input("set").EnumerateArray())
            {
                p = p.SetParam(set[0].GetInt32(), Number(set[1]));
            }

            check.Output(c, "size", p.Size);
            check.Output(c, "params", ModelInputs.Elements(p.Params()));
            check.Output(c, "evaluate", Numbers(c.Input("times")).Select(t => (object?)p.Evaluate(t)).ToArray());
            check.Output(c, "testParams", c.Input("arrays").EnumerateArray()
                .Select(a => (object?)(Func<object?>)(() => p.TestParams(new Vector(Numbers(a)))))
                .ToArray());
        }

        check.Complete();
    }

    private static Parameter ParameterOf(JsonElement spec)
    {
        switch (spec.GetProperty("kind").GetString())
        {
            case "ConstantParameter":
                Constraint constraint = OptimizationSpecs.Constraint(spec.GetProperty("constraint"));
                JsonElement value = spec.GetProperty("value");
                return value.ValueKind == JsonValueKind.Null
                    ? new ConstantParameter(constraint)
                    : new ConstantParameter(Number(value), constraint);
            case "NullParameter":
                return new NullParameter();
            case "PiecewiseConstantParameter":
                JsonElement c = spec.GetProperty("constraint");
                double[] times = Numbers(spec.GetProperty("times"));
                return c.ValueKind == JsonValueKind.Null
                    ? new PiecewiseConstantParameter(times)
                    : new PiecewiseConstantParameter(times, OptimizationSpecs.Constraint(c));
            case var kind:
                throw new InvalidOperationException($"unknown parameter {kind}");
        }
    }
}
