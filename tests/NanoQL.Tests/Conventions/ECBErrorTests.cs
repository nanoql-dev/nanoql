// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="ECB"/>'s refusals, with QuantLib's texts (<c>ecb.cpp:183-184</c>, <c>:205-206</c>, <c>:233-234</c>,
/// <c>:269-270</c>): a string that isn't an ECB code and a date that isn't an ECB date are arguments,
/// <see cref="ArgumentException"/> naming them; a date past the known dates is refused by the value's data,
/// <see cref="NanoQLException"/>; a null string is <see cref="ArgumentNullException"/>.
/// </summary>
public class ECBErrorTests
{
    [Fact]
    public void ECBRefusesWithQuantLibsTexts()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        ECB ecb = ECB.Default;

        ArgumentException code = Assert.Throws<ArgumentException>(() => ecb.Date("BAD05", context));
        Assert.Equal("ecbCode", code.ParamName);
        Assert.StartsWith("BAD05 is not a valid ECB code", code.Message, StringComparison.Ordinal);
        code = Assert.Throws<ArgumentException>(() => ECB.NextCode("JUNE9"));
        Assert.Equal("ecbCode", code.ParamName);
        Assert.StartsWith("JUNE9 is not a valid ECB code", code.Message, StringComparison.Ordinal);

        ArgumentException date = Assert.Throws<ArgumentException>(() => ecb.Code(new Date(1, Month.January, 2005)));
        Assert.Equal("ecbDate", date.ParamName);
        Assert.StartsWith("January 1st, 2005 is not a valid ECB date", date.Message, StringComparison.Ordinal);

        NanoQLException after = Assert.Throws<NanoQLException>(() => ecb.NextDate(context));
        Assert.Equal("ECB dates after December 18th, 2024 are unknown", after.Message);
        after = Assert.Throws<NanoQLException>(() => ecb.IsECBdate(new Date(19, Month.December, 2024)));
        Assert.Equal("ECB dates after December 18th, 2024 are unknown", after.Message);
        after = Assert.Throws<NanoQLException>(() => ecb.Date(Month.January, 2025));
        Assert.Equal("ECB dates after December 18th, 2024 are unknown", after.Message);

        Assert.Equal("ecbCode", Assert.Throws<ArgumentNullException>(() => ECB.IsECBcode(null!)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => ecb.NextDate(null!)).ParamName);
    }
}
