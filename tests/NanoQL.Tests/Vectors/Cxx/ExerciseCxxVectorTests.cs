// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's exercise vectors, <c>tests/vectors/cxx/exercise.json</c>: <see cref="EarlyExercise.PayoffAtExpiry"/>,
/// which the wheel doesn't give back, on American exercises between two dates and from the latest date only (whose
/// earliest date is <see cref="NanoQL.Time.Date.MinDate"/>, a constructor the wheel doesn't wrap), Bermudan exercises
/// and bare <see cref="EarlyExercise"/>s, with each one's type, dates, last date and dates by index.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExerciseCxxVectorTests
{
    [Fact]
    public void ExerciseCxxVector()
    {
        var file = VectorFile.Load("cxx/exercise");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var e = (EarlyExercise)OptionInputs.ExerciseOf(c.Input("exercise"));
            check.Output(c, "payoffAtExpiry", e.PayoffAtExpiry);
            check.Output(c, "type", e.Type);
            check.Output(c, "dates", e.Dates);
            check.Output(c, "lastDate", () => e.LastDate());
            check.Output(c, "dateAt", Enumerable.Range(0, e.Dates.Length).Select(i => e.DateAt(i)).ToArray());
        }

        check.Complete();
    }
}
