// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The wheel's rebated exercise vectors, <c>tests/vectors/rebatedexercise.json</c>: Bermudan exercises with a rebate
/// per date, their type, dates and last date, and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RebatedExerciseVectorTests
{
    [Fact]
    public void RebatedExerciseVector()
    {
        var file = VectorFile.Load("rebatedexercise");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => NonstandardInputs.ExerciseOf(c.Input("exercise")));
                continue;
            }

            Exercise e = NonstandardInputs.ExerciseOf(c.Input("exercise"));
            check.Output(c, "type", e.Type);
            check.Output(c, "dates", e.Dates);
            check.Output(c, "lastDate", e.LastDate());
        }

        check.Complete();
    }
}
