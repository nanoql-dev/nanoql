// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's rebated exercise vectors,
/// <c>tests/vectors/cxx/pricingengines/swaption/basketgeneratingengine.json</c>: on European, Bermudan and American
/// exercises with one rebate or a rebate per date, the members of
/// <see cref="RebatedExercise"/> the wheel doesn't bind, its rebates, <see cref="RebatedExercise.Rebate"/> and
/// <see cref="RebatedExercise.RebatePaymentDate"/> at each index, and QuantLib's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RebatedExerciseCxxVectorTests
{
    [Fact]
    public void RebatedExerciseCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/swaption/basketgeneratingengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            RebatedExercise Exercise() => (RebatedExercise)NonstandardInputs.ExerciseOf(c.Input("exercise"));
            check.Output(c, "rebates", () => Exercise().Rebates);
            int rebates = c.Expected.GetProperty("rebate").GetArrayLength();
            check.Output(c, "rebate", Enumerable.Range(0, rebates).Select(i => (Func<object?>)(() => Exercise().Rebate(i))));
            int dates = c.Expected.GetProperty("rebatePaymentDate").GetArrayLength();
            check.Output(
                c, "rebatePaymentDate", Enumerable.Range(0, dates).Select(i => (Func<object?>)(() => Exercise().RebatePaymentDate(i))));
        }

        check.Complete();
    }
}
