// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;
using static NanoQL.Tests.Vectors.VanillaFdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmSimpleSwingCondition vectors, <c>tests/vectors/methods/finitedifferences/stepconditions/
/// fdmsimpleswingcondition.json</c>: <see cref="FdmSimpleSwingCondition"/> applied at each exercise time and between
/// them over two numbers of rights, two payoffs and three minimums, and refused on an array of another size; and
/// <see cref="FdmTimeDepDirichletBoundary"/>'s and <see cref="FdmDiscountDirichletBoundary"/>'s applications in each
/// direction and on each side after setting three times.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmSimpleSwingConditionVectorTests
{
    [Fact]
    public void FdmSimpleSwingConditionVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/stepconditions/fdmsimpleswingcondition");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            double[] a = Doubles(c.Input("array"));
            double[] times = Doubles(c.Input("times"));
            if (c.Expected.TryGetProperty("applyTo", out _))
            {
                var condition = new FdmSimpleSwingCondition(
                    Doubles(c.Input("exerciseTimes")),
                    mesher,
                    new FdmLogInnerValue(SwingPayoffOf(c.Input("payoff")), mesher, 0),
                    1,
                    c.Int("minExercises"));
                check.Output(c, "applyTo", times.Select(t => (object?)Applied(condition, a, t)).ToArray());
                double[] shortArray = a[..^1];
                check.Output(c, "applyToShort", () =>
                {
                    condition.ApplyTo(shortArray, condition.ExerciseTimes[^1]);
                    return null;
                });
                continue;
            }

            BoundaryCondition<FdmLinearOp> bc = BoundaryOf(c.Input("boundary"), mesher);
            var afterApplying = new List<object?>();
            var afterSolving = new List<object?>();
            foreach (double t in times)
            {
                bc.SetTime(t);
                double[] x = [.. a], y = [.. a];
                bc.ApplyAfterApplying(x);
                bc.ApplyAfterSolving(y);
                afterApplying.Add(x);
                afterSolving.Add(y);
            }

            check.Output(c, "applyAfterApplying", afterApplying);
            check.Output(c, "applyAfterSolving", afterSolving);
        }

        check.Complete();
    }

    // the time-dependent boundary at a + b·t, or the discount boundary
    private static BoundaryCondition<FdmLinearOp> BoundaryOf(JsonElement spec, FdmMesher mesher)
    {
        int direction = spec.GetProperty("direction").GetInt32();
        BoundaryConditionSide side = spec.GetProperty("side").GetString() == "lower" ? BoundaryConditionSide.Lower : BoundaryConditionSide.Upper;
        if (spec.GetProperty("type").GetString() == "timeDependent")
        {
            double a = spec.GetProperty("a").GetDouble(), b = spec.GetProperty("b").GetDouble();
            return new FdmTimeDepDirichletBoundary(mesher, t => a + (b * t), direction, side);
        }

        return new FdmDiscountDirichletBoundary(
            mesher,
            CurveOf(spec.GetProperty("rTS")),
            spec.GetProperty("maturityTime").GetDouble(),
            spec.GetProperty("valueOnBoundary").GetDouble(),
            direction,
            side);
    }
}
