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
/// The FdmSnapshotCondition vectors,
/// <c>tests/vectors/methods/finitedifferences/stepconditions/fdmsnapshotcondition.json</c>:
/// <see cref="FdmSnapshotCondition"/>'s time, and its values before any <see cref="FdmSnapshotCondition.ApplyTo"/>,
/// after one at another time, after one at its time and after a second one there.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmSnapshotConditionVectorTests
{
    [Fact]
    public void FdmSnapshotConditionVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/stepconditions/fdmsnapshotcondition");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double t = c.Double("t");
            var snapshot = new FdmSnapshotCondition(t);
            double[] a = Doubles(c.Input("a")), b = Doubles(c.Input("b"));
            check.Output(c, "time", snapshot.Time);
            check.Output(c, "before", snapshot.GetValues().ToArray());
            snapshot.ApplyTo(a, c.Double("other"));
            check.Output(c, "afterOther", snapshot.GetValues().ToArray());
            snapshot.ApplyTo(a, t);
            check.Output(c, "afterOwn", snapshot.GetValues().ToArray());
            check.Output(c, "arrayAfterOwn", a);
            snapshot.ApplyTo(b, t);
            check.Output(c, "afterSecond", snapshot.GetValues().ToArray());
        }

        check.Complete();
    }
}
