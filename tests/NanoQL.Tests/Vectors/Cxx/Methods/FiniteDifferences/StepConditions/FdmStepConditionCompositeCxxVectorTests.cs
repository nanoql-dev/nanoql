// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's step condition composite vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.json</c>:
/// <see cref="FdmStepConditionComposite.Conditions"/> of composites of snapshot conditions and of
/// <see cref="FdmStepConditionComposite.JoinConditions"/>, each condition a snapshot's time or a composite's number of
/// conditions, with the stopping times.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmStepConditionCompositeCxxVectorTests
{
    [Fact]
    public void FdmStepConditionCompositeCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/stepconditions/fdmstepconditioncomposite");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[][] times = [.. c.Input("stoppingTimes").EnumerateArray().Select(t => t.EnumerateArray().Select(x => x.GetDouble()).ToArray())];
            var composite = new FdmStepConditionComposite(
                times, c.Doubles("snapshots").Select(t => (StepCondition)new FdmSnapshotCondition(t)));
            if (c.Input("join").ValueKind != JsonValueKind.Null)
            {
                composite = FdmStepConditionComposite.JoinConditions(new FdmSnapshotCondition(c.Double("join")), composite);
            }

            check.Output(c, "stoppingTimes", composite.StoppingTimes.ToArray());
            check.Output(c, "conditions", composite.Conditions.Select(Described).ToArray());
        }

        check.Complete();
    }

    private static object? Described(StepCondition condition) => condition switch
    {
        FdmSnapshotCondition s => new object[] { "snapshot", s.Time },
        FdmStepConditionComposite k => new object[] { "composite", k.Conditions.Length },
        _ => throw new InvalidOperationException("a condition of another type"),
    };
}
