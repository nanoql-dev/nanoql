// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmBermudanStepCondition vectors,
/// <c>tests/vectors/methods/finitedifferences/stepconditions/fdmbermudanstepcondition.json</c>:
/// <see cref="FdmBermudanStepCondition"/>'s exercise times for four schedules, and its
/// <see cref="FdmBermudanStepCondition.ApplyTo"/> at each and between them, on two meshers; and the refusal of an array
/// of another size.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmBermudanStepConditionVectorTests
{
    [Fact]
    public void FdmBermudanStepConditionVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/stepconditions/fdmbermudanstepcondition");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            var condition = new FdmBermudanStepCondition(
                c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32())),
                new Date(c.Int("referenceDate")),
                CashFlowInputs.DayCounterOf(c.Input("dayCounter")),
                mesher,
                CalculatorOf(c.Input("calculator"), mesher));
            double[] a = Doubles(c.Input("array"));
            double[] times = Doubles(c.Input("times"));
            check.Output(c, "exerciseTimes", condition.ExerciseTimes.ToArray());
            check.Output(c, "applyTo", times.Select(t => (object?)Applied(condition, a, t)).ToArray());
            double[] shortArray = a[..^1];
            check.Output(c, "applyToShort", () =>
            {
                condition.ApplyTo(shortArray, condition.ExerciseTimes[^1]);
                return null;
            });
        }

        check.Complete();
    }
}
