// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's business-day convention vectors, <c>tests/vectors/cxx/time/businessdayconvention.json</c>: the
/// convention's text, which the wheel doesn't expose, for every convention and for a value on either side, where
/// QuantLib throws.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BusinessDayConventionCxxVectorTests
{
    [Fact]
    public void BusinessDayConventionCxxVector()
    {
        var file = VectorFile.Load("cxx/time/businessdayconvention");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            BusinessDayConvention[] conventions = c.Ints("conventions").Select(b => (BusinessDayConvention)b).ToArray();
            check.Output(
                c,
                "toString",
                conventions.Select(b => (Func<object?>)(() => BusinessDayConventionFunctions.ToString(b))).ToArray());
        }

        check.Complete();
    }
}
