// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BrownianBridge vectors, <c>tests/vectors/methods/montecarlo/brownianbridge.json</c>: bridges from
/// <see cref="BrownianBridge"/>'s three constructors, each read through its size, times, weights and indices, five
/// transforms and the refusal of an input of another size.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BrownianBridgeVectorTests
{
    [Fact]
    public void BrownianBridgeVector()
    {
        var file = VectorFile.Load("methods/montecarlo/brownianbridge");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            BrownianBridge bb = c.String("constructor") switch
            {
                "steps" => new BrownianBridge(c.Int("steps")),
                "times" => new BrownianBridge(Numbers(c.Input("times"))),
                "timeGrid" => new BrownianBridge(Grid(c.Input("timeGrid"))),
                string other => throw new InvalidOperationException($"{c.Name}: unknown constructor {other}"),
            };
            check.Output(c, "size", bb.Size);
            check.Output(c, "times", bb.Times);
            check.Output(c, "leftWeight", bb.LeftWeight);
            check.Output(c, "rightWeight", bb.RightWeight);
            check.Output(c, "stdDeviation", bb.StdDeviation);
            check.Output(c, "bridgeIndex", bb.BridgeIndex);
            check.Output(c, "leftIndex", bb.LeftIndex);
            check.Output(c, "rightIndex", bb.RightIndex);
            check.Output(
                c,
                "transform",
                c.Input("variates").EnumerateArray().Select(x => (object?)Transform(bb, Numbers(x))).ToArray());
            double[] wrong = Numbers(c.Input("wrong"));
            check.Output(c, "transformWrongSize", () => Transform(bb, wrong));
        }

        check.Complete();
    }

    // The vectors' grid: {"end", "steps"}, {"times"} or {"times", "steps"}, by TimeGrid's three constructors.
    internal static TimeGrid Grid(JsonElement spec)
    {
        bool hasEnd = spec.TryGetProperty("end", out JsonElement end);
        bool hasSteps = spec.TryGetProperty("steps", out JsonElement steps);
        bool hasTimes = spec.TryGetProperty("times", out JsonElement times);
        return (hasEnd, hasTimes, hasSteps) switch
        {
            (false, false, false) => new TimeGrid(),
            (true, false, true) => new TimeGrid(Number(end), steps.GetInt32()),
            (false, true, false) => new TimeGrid(Numbers(times)),
            (false, true, true) => new TimeGrid(Numbers(times), steps.GetInt32()),
            _ => throw new InvalidOperationException($"unknown time grid spec {spec}"),
        };
    }

    // The wheel's transform: into a new sequence of the input's size.
    private static double[] Transform(BrownianBridge bb, double[] input)
    {
        var output = new double[input.Length];
        bb.Transform(input, output);
        return output;
    }
}
