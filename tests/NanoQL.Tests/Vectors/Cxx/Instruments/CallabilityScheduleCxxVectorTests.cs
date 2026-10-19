// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's callability vectors, <c>tests/vectors/cxx/instruments/callabilityschedule.json</c>: whether a
/// callability has occurred, at seven reference dates with each of the three include flags, under evaluation dates on
/// and around its date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CallabilityScheduleCxxVectorTests
{
    [Fact]
    public void CallabilityScheduleCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/callabilityschedule");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Callability callability = CallableBondInputs.CallabilityOf(c.Input("callability"));
            Date?[] refs = [.. c.Input("refDates").EnumerateArray().Select(CashFlowInputs.NullableDate)];
            bool?[] flags =
            [
                .. c.Input("flags").EnumerateArray()
                    .Select(f => f.ValueKind == JsonValueKind.Null ? (bool?)null : f.GetBoolean()),
            ];
            check.Output(
                c,
                "hasOccurred",
                refs.SelectMany(r => flags.Select(f => (object?)callability.HasOccurred(context, r, f))).ToArray());
        }

        check.Complete();
    }
}
