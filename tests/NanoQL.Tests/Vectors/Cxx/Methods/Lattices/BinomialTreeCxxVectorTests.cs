// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's binomial tree vectors, <c>tests/vectors/cxx/methods/lattices/binomialtree.json</c>: the seven
/// binomial trees on four Black-Scholes-Merton processes, their <see cref="Tree.Branches"/>,
/// <see cref="Tree.Columns"/>, every column's <see cref="Tree.Size"/>, every node's <see cref="Tree.Underlying"/>, and
/// every node's and branch's
/// <see cref="Tree.Descendant"/> and <see cref="Tree.Probability"/>; then the constructors' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BinomialTreeCxxVectorTests
{
    [Fact]
    public void BinomialTreeCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/lattices/binomialtree");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("tree");
            if (!c.Expected.TryGetProperty("size", out _))
            {
                check.Output(c, "columns", () => TreeOf(spec).Columns);
                continue;
            }

            Tree tree = TreeOf(spec);
            int columns = tree.Columns;
            int[] sizes = [.. Enumerable.Range(0, columns).Select(tree.Size)];
            check.Output(c, "branches", tree.Branches);
            check.Output(c, "columns", columns);
            check.Output(c, "size", sizes.Select(n => (object?)n).ToArray());
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
                        .Select(j => Enumerable.Range(0, 2).Select(b => (object?)tree.Descendant(i, j, b)).ToArray())
                        .ToArray())
                    .ToArray());
            check.Output(
                c,
                "probability",
                Enumerable.Range(0, columns - 1)
                    .Select(i => Enumerable.Range(0, sizes[i])
                        .Select(j => Enumerable.Range(0, 2).Select(b => (object?)tree.Probability(i, j, b)).ToArray())
                        .ToArray())
                    .ToArray());
        }

        check.Complete();
    }

    // The harness's spec: the tree on a Black-Scholes-Merton process of flat forwards and a constant volatility from
    // the reference date on Actual/365 (Fixed), built through its type's Create.
    internal static Tree TreeOf(JsonElement spec)
    {
        var reference = new Date(spec.GetProperty("referenceDate").GetInt32());
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            spec.GetProperty("spot").GetDouble(),
            new FlatForward(reference, spec.GetProperty("q").GetDouble(), dc),
            new FlatForward(reference, spec.GetProperty("r").GetDouble(), dc),
            new BlackConstantVol(reference, new NullCalendar(), spec.GetProperty("volatility").GetDouble(), dc));
        double end = spec.GetProperty("end").GetDouble();
        int steps = spec.GetProperty("steps").GetInt32();
        double strike = spec.GetProperty("strike").GetDouble();
        return spec.GetProperty("type").GetString() switch
        {
            "JarrowRudd" => JarrowRudd.Create(process, end, steps, strike),
            "CoxRossRubinstein" => CoxRossRubinstein.Create(process, end, steps, strike),
            "AdditiveEQPBinomialTree" => AdditiveEQPBinomialTree.Create(process, end, steps, strike),
            "Trigeorgis" => Trigeorgis.Create(process, end, steps, strike),
            "Tian" => Tian.Create(process, end, steps, strike),
            "LeisenReimer" => LeisenReimer.Create(process, end, steps, strike),
            "Joshi4" => Joshi4.Create(process, end, steps, strike),
            var t => throw new ArgumentException($"unknown binomial tree {t}", nameof(spec)),
        };
    }
}
