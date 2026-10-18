// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The iterative bootstrap vectors, <c>tests/vectors/termstructures/iterativebootstrap.json</c>:
/// <see cref="IterativeBootstrap"/>'s nine parameters on piecewise curves (<see cref="BootstrapInputs"/>), the
/// bootstrap's refusals among them: whether each curve builds, its nodes as <c>[date, value]</c> pairs and their times,
/// one element per curve of the case.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IterativeBootstrapVectorTests
{
    [Fact]
    public void IterativeBootstrapVector()
    {
        var file = VectorFile.Load("termstructures/iterativebootstrap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BootstrapHelper<YieldTermStructure>[] helpers = RateHelperInputs.HelpersOf(c.Input("helpers"), context);
            JsonElement[] specs = [.. c.Input("curves").EnumerateArray()];
            var curves = new BootstrapInputs.Built?[specs.Length];
            check.Output(c, "built", specs.Select((s, k) => (Func<object?>)(() =>
            {
                curves[k] = BootstrapInputs.CurveOf(s, helpers, context);
                return true;
            })).ToArray());
            check.Output(c, "nodes", curves.Select(b => b is null
                ? []
                : b.Nodes.Select(n => (object?)new object?[] { n.Key, n.Value }).ToArray()).ToArray());
            check.Output(c, "times", curves.Select(b => b is null ? [] : b.Times.Cast<object?>().ToArray()).ToArray());
        }

        check.Complete();
    }
}
