// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Libor vectors, <c>tests/vectors/cxx/indexes/ibor/libor.json</c>: <see cref="Libor.JointCalendar"/>,
/// which the wheel doesn't bind, its name and its business days over 2025.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LiborCxxVectorTests
{
    [Fact]
    public void LiborCxxVector()
    {
        var file = VectorFile.Load("cxx/indexes/ibor/libor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Calendar joint = ((Libor)PresetInputs.PresetOf(c.Input("index"))).JointCalendar;
            check.Output(c, "name", joint.Name);
            check.Output(
                c,
                "isBusinessDay",
                c.DateInputs("dates").Select(d => (Func<object?>)(() => joint.IsBusinessDay(d))).ToArray());
        }

        check.Complete();
    }
}
