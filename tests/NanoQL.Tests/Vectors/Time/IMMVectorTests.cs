// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The IMM vectors, <c>tests/vectors/time/imm.json</c>: <see cref="IMM.IsIMMdate"/> on every day of 2026 to 2028, then
/// <see cref="IMM"/>'s other functions, as <see cref="ASXVectorTests"/> checks <see cref="ASX"/>'s.
/// </summary>
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
            if (!c.Inputs.TryGetProperty("kind", out _))
            {
                Date[] dates = c.DateInputs("dates");
                check.Output(c, "mainCycle", dates.Select(d => IMM.IsIMMdate(d, true)).ToArray());
                check.Output(c, "anyMonth", dates.Select(d => IMM.IsIMMdate(d, false)).ToArray());
                continue;
            }

            DateFunctionInputs.Check(
                check,
                c,
                new DateFunctionInputs.Functions(
                    IMM.IsIMMdate,
                    IMM.IsIMMcode,
                    IMM.Code,
                    IMM.Date,
                    IMM.NextDate,
                    IMM.NextDate,
                    IMM.NextCode,
                    IMM.NextCode));
        }

        check.Complete();
    }
}
