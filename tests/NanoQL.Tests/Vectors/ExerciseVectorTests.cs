// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The exercise vectors, <c>tests/vectors/exercise.json</c>: European, American and Bermudan exercises and bare
/// <see cref="Exercise"/>s, their types, dates, last dates and each date by <see cref="Exercise.Date(int)"/> and
/// <see cref="Exercise.DateAt(int)"/>; and the constructors' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExerciseVectorTests
{
    [Fact]
    public void ExerciseVector()
    {
        var file = VectorFile.Load("exercise");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "exercise":
                {
                    Exercise e = OptionInputs.ExerciseOf(c.Input("exercise"));
                    int[] indexes = [.. Enumerable.Range(0, e.Dates.Length)];
                    check.Output(c, "type", e.Type);
                    check.Output(c, "dates", e.Dates);
                    check.Output(c, "lastDate", () => e.LastDate());
                    check.Output(c, "date", indexes.Select(i => e.Date(i)).ToArray());
                    check.Output(c, "dateAt", indexes.Select(i => e.DateAt(i)).ToArray());
                    break;
                }

                case "refused":
                    check.Output(c, "exercise", () => OptionInputs.ExerciseOf(c.Input("exercise")));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }
}
