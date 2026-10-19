// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;
using static NanoQL.Tests.Vectors.BrownianBridgeVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's trinomial tree vectors, <c>tests/vectors/cxx/methods/lattices/trinomialtree.json</c>:
/// <see cref="TrinomialTree"/> on Ornstein-Uhlenbeck processes, on uniform, non-uniform and floored grids, with and
/// without <c>isPositive</c>: <see cref="TrinomialTree.Branches"/>, <see cref="Tree.Columns"/>,
/// <see cref="TrinomialTree.TimeGrid"/>, and every column's <see cref="TrinomialTree.Size"/> and
/// <see cref="TrinomialTree.Dx"/>, every node's <see cref="TrinomialTree.Underlying"/>, and every node's and branch's
/// <see cref="TrinomialTree.Descendant"/> and <see cref="TrinomialTree.Probability"/>; then the constructor's
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TrinomialTreeCxxVectorTests
{
    [Fact]
    public void TrinomialTreeCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/lattices/trinomialtree");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("tree");
            if (!c.Expected.TryGetProperty("size", out _))
            {
                check.Output(c, "columns", () => TreeOf(spec).Columns);
                continue;
            }

            TrinomialTree tree = TreeOf(spec);
            int columns = tree.Columns;
            int[] sizes = [.. Enumerable.Range(0, columns).Select(tree.Size)];
            check.Output(c, "branches", tree.Branches);
            check.Output(c, "columns", columns);
            TimeGrid grid = tree.TimeGrid;
            check.Output(c, "timeGrid", Enumerable.Range(0, grid.Size).Select(i => (object?)grid[i]).ToArray());
            check.Output(c, "size", sizes.Select(n => (object?)n).ToArray());
            check.Output(c, "dx", Enumerable.Range(0, columns).Select(i => (object?)tree.Dx(i)).ToArray());
            check.Output(
                c,
                "underlying",
                Enumerable.Range(0, columns)
                    .Select(i => Enumerable.Range(0, sizes[i]).Select(j => (object?)tree.Underlying(i, j)).ToArray())
                    .ToArray());
            check.Output(
                c,
                "descendant",
                Enumerable.Range(0, columns - 1)
                    .Select(i => Enumerable.Range(0, sizes[i])
                        .Select(j => Enumerable.Range(0, 3).Select(b => (object?)tree.Descendant(i, j, b)).ToArray())
                        .ToArray())
                    .ToArray());
            check.Output(
                c,
                "probability",
                Enumerable.Range(0, columns - 1)
                    .Select(i => Enumerable.Range(0, sizes[i])
                        .Select(j => Enumerable.Range(0, 3).Select(b => (object?)tree.Probability(i, j, b)).ToArray())
                        .ToArray())
                    .ToArray());
        }

        check.Complete();
    }

    // The harness's tree spec: {"process": {"type": "OrnsteinUhlenbeckProcess", "speed", "volatility", "x0", "level"},
    // "timeGrid", "isPositive"}.
    private static TrinomialTree TreeOf(JsonElement spec)
    {
        JsonElement p = spec.GetProperty("process");
        Assert.Equal("OrnsteinUhlenbeckProcess", p.GetProperty("type").GetString());
        var process = new OrnsteinUhlenbeckProcess(
            Number(p.GetProperty("speed")),
            Number(p.GetProperty("volatility")),
            Number(p.GetProperty("x0")),
            Number(p.GetProperty("level")));
        bool isPositive = spec.GetProperty("isPositive").GetBoolean();
        return new TrinomialTree(process, Grid(spec.GetProperty("timeGrid")), isPositive);
    }
}
