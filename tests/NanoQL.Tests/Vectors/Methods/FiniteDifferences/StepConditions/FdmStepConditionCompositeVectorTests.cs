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
/// The FdmStepConditionComposite vectors,
/// <c>tests/vectors/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.json</c>: the constructor's
/// sorted, unique stopping times and its conditions applied in order,
/// <see cref="FdmStepConditionComposite.JoinConditions"/>, and <see cref="FdmStepConditionComposite.VanillaComposite"/>
/// for European, American and Bermudan exercises over four dividend schedules, its stopping times and
/// <see cref="FdmStepConditionComposite.ApplyTo"/> at each.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmStepConditionCompositeVectorTests
{
    [Fact]
    public void FdmStepConditionCompositeVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/stepconditions/fdmstepconditioncomposite");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), context);
            FdmInnerValueCalculator calculator = CalculatorOf(c.Input("calculator"), mesher);
            double[] a = Doubles(c.Input("array"));
            var reference = new Date(15, Month.October, 2026);
            DayCounter dc = new Actual365Fixed();
            if (c.Name == "constructor" || c.Name == "joinConditions")
            {
                var snapshot = new FdmSnapshotCondition(c.Double("snapshot"));
                FdmStepConditionComposite composite;
                if (c.Name == "constructor")
                {
                    composite = new FdmStepConditionComposite(
                        [Doubles(c.Input("stoppingTimes"))], [new FdmAmericanStepCondition(mesher, calculator), snapshot]);
                }
                else
                {
                    var bermudan = new FdmBermudanStepCondition(
                        c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32())), reference, dc, mesher, calculator);
                    var inner = new FdmStepConditionComposite(
                        [bermudan.ExerciseTimes], [new FdmAmericanStepCondition(mesher, calculator), bermudan]);
                    composite = FdmStepConditionComposite.JoinConditions(snapshot, inner);
                }

                check.Output(c, "stoppingTimes", composite.StoppingTimes.ToArray());
                check.Output(c, "applyTo", Applied(composite, a, c.Double("t")));
                check.Output(c, "snapshot", snapshot.GetValues().ToArray());
                continue;
            }

            var vanilla = FdmStepConditionComposite.VanillaComposite(
                DividendsOf(c.Input("dividends")),
                ExerciseOf(c.Input("exercise")),
                mesher,
                calculator,
                new Date(c.Int("referenceDate")),
                CashFlowInputs.DayCounterOf(c.Input("dayCounter")),
                context);
            check.Output(c, "stoppingTimes", vanilla.StoppingTimes.ToArray());
            check.Output(
                c, "applyTo", Doubles(c.Input("times")).Select(t => (object?)Applied(vanilla, a, t)).ToArray());
        }

        check.Complete();
    }

    private static Exercise ExerciseOf(System.Text.Json.JsonElement spec)
    {
        Date[] dates = [.. spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
        return spec.GetProperty("type").GetString() switch
        {
            "European" => new EuropeanExercise(dates[^1]),
            "American" => new AmericanExercise(new Date(15, Month.October, 2026), dates[^1]),
            "Bermudan" => new BermudanExercise(dates),
            string s => throw new InvalidOperationException($"unknown exercise '{s}'"),
            null => throw new InvalidOperationException("no exercise type"),
        };
    }
}
