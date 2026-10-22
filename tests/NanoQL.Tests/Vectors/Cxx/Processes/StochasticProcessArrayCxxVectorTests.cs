// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's StochasticProcessArray vectors, <c>tests/vectors/cxx/processes/stochasticprocessarray.json</c>:
/// the members of <see cref="StochasticProcessArray"/> the wheel doesn't expose, <c>Apply</c> at 10 states, the time of
/// 5 dates (the first process's, or its refusal), each process's x0 through <c>Process</c>, and <c>Correlation</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StochasticProcessArrayCxxVectorTests
{
    [Fact]
    public void StochasticProcessArrayCxxVector()
    {
        var file = VectorFile.Load("cxx/processes/stochasticprocessarray");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var p = new StochasticProcessArray(
                StochasticProcessArrayVectorTests.Processes(c), MatrixVectorTests.FromRows(c.Input("correlation")));
            var states = c.Input("states").EnumerateArray()
                .Select(s => (X: new Vector(Numbers(s[0])), Dx: new Vector(Numbers(s[1])))).ToArray();
            NanoQL.Time.Date[] dates = c.DateInputs("dates");
            check.Output(c, "apply", states.Select(s => (object?)p.Apply(s.X, s.Dx).AsSpan().ToArray()).ToArray());
            check.Output(c, "time", dates.Select(d => (object?)(Func<object?>)(() => p.Time(d))).ToArray());
            check.Output(c, "process", Enumerable.Range(0, p.Size).Select(i => (object?)p.Process(i).X0).ToArray());
            check.Output(c, "correlation", MatrixVectorTests.Rows(p.Correlation()));
        }

        check.Complete();
    }
}
