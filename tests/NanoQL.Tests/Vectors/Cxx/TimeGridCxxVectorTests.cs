// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;
using static NanoQL.Tests.Vectors.BrownianBridgeVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's TimeGrid vectors, <c>tests/vectors/cxx/timegrid.json</c>: <see cref="TimeGrid"/>'s members the
/// wheel doesn't expose (<see cref="TimeGrid.Index"/>, <see cref="TimeGrid.ClosestIndex"/>,
/// <see cref="TimeGrid.ClosestTime"/>, <see cref="TimeGrid.MandatoryTimes"/>, <see cref="TimeGrid.At"/>,
/// <see cref="TimeGrid.Empty"/>, <see cref="TimeGrid.Front"/>, <see cref="TimeGrid.Back"/>, the parameterless
/// constructor), with the constructors' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TimeGridCxxVectorTests
{
    [Fact]
    public void TimeGridCxxVector()
    {
        var file = VectorFile.Load("cxx/timegrid");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (!c.Inputs.TryGetProperty("t", out _))
            {
                check.Output(c, "size", () => Grid(c.Input("timeGrid")).Size);
                continue;
            }

            TimeGrid tg = Grid(c.Input("timeGrid"));
            double[] ts = Numbers(c.Input("t"));
            check.Output(c, "size", tg.Size);
            check.Output(c, "empty", tg.Empty);
            check.Output(c, "mandatoryTimes", tg.MandatoryTimes);
            check.Output(c, "closestIndex", ts.Select(t => (object?)tg.ClosestIndex(t)).ToArray());
            if (tg.Empty)
            {
                continue;
            }

            check.Output(c, "index", ts.Select(t => (object?)(Func<object?>)(() => tg.Index(t))).ToArray());
            check.Output(c, "closestTime", ts.Select(t => (object?)tg.ClosestTime(t)).ToArray());
            check.Output(c, "front", tg.Front());
            check.Output(c, "back", tg.Back());
            check.Output(c, "at", Enumerable.Range(0, tg.Size).Select(i => (object?)tg.At(i)).ToArray());
        }

        check.Complete();
    }
}
