// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The TimeGrid vectors, <c>tests/vectors/timegrid.json</c>: grids from <see cref="TimeGrid"/>'s three constructors,
/// each read through <see cref="TimeGrid.Size"/>, the indexer and <see cref="TimeGrid.Dt"/>, and the constructors'
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TimeGridVectorTests
{
    [Fact]
    public void TimeGridVector()
    {
        var file = VectorFile.Load("timegrid");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Func<TimeGrid> make = c.String("constructor") switch
            {
                "regular" => () => new TimeGrid(c.Double("end"), c.Int("steps")),
                "mandatory" => () => new TimeGrid(Numbers(c.Input("times"))),
                "steps" => () => new TimeGrid(Numbers(c.Input("times")), c.Int("steps")),
                string other => throw new InvalidOperationException($"{c.Name}: unknown constructor {other}"),
            };
            if (!c.Expected.TryGetProperty("times", out _))
            {
                check.Output(c, "size", () => make().Size);
                continue;
            }

            TimeGrid tg = make();
            check.Output(c, "size", tg.Size);
            check.Output(c, "times", Enumerable.Range(0, tg.Size).Select(i => (object?)tg[i]).ToArray());
            check.Output(c, "dt", Enumerable.Range(0, tg.Size - 1).Select(i => (object?)tg.Dt(i)).ToArray());
        }

        check.Complete();
    }
}
