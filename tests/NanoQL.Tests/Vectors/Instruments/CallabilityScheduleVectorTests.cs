// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The callability vectors, <c>tests/vectors/instruments/callabilityschedule.json</c>: calls and puts at clean and
/// dirty prices on dates across QuantLib's range, and their price, type and date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CallabilityScheduleVectorTests
{
    [Fact]
    public void CallabilityScheduleVector()
    {
        var file = VectorFile.Load("instruments/callabilityschedule");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Callability callability = CallableBondInputs.CallabilityOf(c.Input("callability"));
            check.Output(c, "priceAmount", callability.Price.Amount);
            check.Output(c, "priceType", callability.Price.Type);
            check.Output(c, "type", callability.Type);
            check.Output(c, "date", callability.Date);
        }

        check.Complete();
    }
}
