// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Utilities;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The data parsers' vectors, <c>tests/vectors/utilities/dataparsers.json</c>: ISO dates and periods, well formed or
/// refused by QuantLib's own checks.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DataParsersVectorTests
{
    [Fact]
    public void DataParsers()
    {
        var file = VectorFile.Load("utilities/dataparsers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            string[] strings = [.. c.Input("strings").EnumerateArray().Select(e => e.GetString()!)];
            if (c.Name == "parseISO")
            {
                check.Output(c, "date", strings.Select(s => (Func<object?>)(() => DateParser.ParseISO(s))).ToArray());
            }
            else
            {
                check.Output(c, "length", strings.Select(s => (Func<object?>)(() => PeriodParser.Parse(s).Length)).ToArray());
                check.Output(c, "units", strings.Select(s => (Func<object?>)(() => PeriodParser.Parse(s).Units)).ToArray());
            }
        }

        check.Complete();
    }
}
