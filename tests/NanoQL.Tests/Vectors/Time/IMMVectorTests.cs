// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>The IMM vector, <c>tests/vectors/time/imm.json</c>.</summary>
[Trait("Oracle", "QuantLib")]
public class IMMVectorTests
{
    [Fact]
    public void IMMVector()
    {
        var file = VectorFile.Load("time/imm");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Date[] dates = c.DateInputs("dates");
            check.Output(c, "mainCycle", dates.Select(d => IMM.IsIMMdate(d, true)).ToArray());
            check.Output(c, "anyMonth", dates.Select(d => IMM.IsIMMdate(d, false)).ToArray());
        }

        check.Complete();
    }
}
