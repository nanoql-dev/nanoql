// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;
using static NanoQL.Tests.Vectors.VanillaFdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmSimpleStorageCondition vectors, <c>tests/vectors/methods/finitedifferences/stepconditions/
/// fdmsimplestoragecondition.json</c>: <see cref="FdmSimpleStorageCondition"/> applied at each exercise time and
/// between them on two storage grids, under a call's and a put's inner value at three change rates, and refused on an
/// array one short.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmSimpleStorageConditionVectorTests
{
    [Fact]
    public void FdmSimpleStorageConditionVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/stepconditions/fdmsimplestoragecondition");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            double[] a = Doubles(c.Input("array"));
            var condition = new FdmSimpleStorageCondition(
                Doubles(c.Input("exerciseTimes")),
                mesher,
                new FdmLogInnerValue(SwingPayoffOf(c.Input("payoff")), mesher, 0),
                c.Double("changeRate"));
            check.Output(c, "applyTo", Doubles(c.Input("times")).Select(t => (object?)Applied(condition, a, t)).ToArray());
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
