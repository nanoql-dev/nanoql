// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;
using static NanoQL.Tests.Vectors.BrownianBridgeVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Path vectors, <c>tests/vectors/cxx/methods/montecarlo/path.json</c>: a <see cref="MonteCarloPath"/>
/// built by its user, which the wheel can't build, through every member (<see cref="MonteCarloPath.Length"/>,
/// <see cref="MonteCarloPath.Empty"/>, <see cref="MonteCarloPath.Front"/>, <see cref="MonteCarloPath.Back"/>,
/// <see cref="MonteCarloPath.TimeGrid"/>, the enumerator, the indexer, <see cref="MonteCarloPath.At"/>,
/// <see cref="MonteCarloPath.Value"/> and <see cref="MonteCarloPath.Time"/>), with the constructor's default values
/// and its refusal.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PathCxxVectorTests
{
    [Fact]
    public void PathCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/montecarlo/path");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("path");
            if (!c.Expected.TryGetProperty("timeGrid", out _))
            {
                check.Output(c, "length", () => Path(spec).Length);
                continue;
            }

            MonteCarloPath path = Path(spec);
            int n = path.Length;
            check.Output(c, "length", n);
            check.Output(c, "empty", path.Empty);
            check.Output(c, "timeGrid", Times(path.TimeGrid));
            check.Output(c, "time", Enumerable.Range(0, n).Select(i => (object?)path.Time(i)).ToArray());

            // QuantLib leaves the default values uninitialized (nanoql's are zeros), so only given values are read.
            if (!c.Expected.TryGetProperty("values", out _))
            {
                continue;
            }

            var values = new List<object?>();
            foreach (double x in path)
            {
                values.Add(x);
            }

            check.Output(c, "values", values);
            check.Output(c, "front", path.Front());
            check.Output(c, "back", path.Back());
            check.Output(c, "element", Enumerable.Range(0, n).Select(i => (object?)path[i]).ToArray());
            check.Output(c, "at", Enumerable.Range(0, n).Select(i => (object?)path.At(i)).ToArray());
            check.Output(c, "value", Enumerable.Range(0, n).Select(i => (object?)path.Value(i)).ToArray());
        }

        check.Complete();
    }

    // The harness's path spec: {"timeGrid", "values"}, the values null for the constructor's default.
    private static MonteCarloPath Path(JsonElement spec)
    {
        TimeGrid grid = Grid(spec.GetProperty("timeGrid"));
        JsonElement values = spec.GetProperty("values");
        return values.ValueKind == JsonValueKind.Null ? new MonteCarloPath(grid) : new MonteCarloPath(grid, new Vector(Numbers(values)));
    }

    private static object?[] Times(TimeGrid grid) => [.. Enumerable.Range(0, grid.Size).Select(i => (object?)grid[i])];
}
