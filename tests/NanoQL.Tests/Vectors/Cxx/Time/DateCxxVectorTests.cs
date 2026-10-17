// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's date vectors, <c>tests/vectors/cxx/time/date.json</c>: <see cref="DateFunctions.ShortDate"/>,
/// which the wheel doesn't expose, on every 97th serial of QuantLib's range, its edges, named dates and the null date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DateCxxVectorTests
{
    [Fact]
    public void DateCxxVector()
    {
        var file = VectorFile.Load("cxx/time/date");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Date?[] dates = c.Input("dates").EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.Null ? (Date?)null : new Date(e.GetInt32()))
                .ToArray();
            check.Output(c, "shortDate", dates.Select(d => (Func<object?>)(() => DateFunctions.ShortDate(d))).ToArray());
        }

        check.Complete();
    }
}
