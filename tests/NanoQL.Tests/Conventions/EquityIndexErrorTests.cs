// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="EquityIndex"/>'s refusals, whose texts QuantLib's own cases check (<c>test-suite/equityindex.cpp</c>):
/// an invalid fixing date is a refusal of the argument, <see cref="ArgumentException"/> naming <c>fixingDate</c>; a
/// missing fixing, a missing curve and a missing spot and fixing are refusals of the index's data,
/// <see cref="NanoQLException"/>; a null name or calendar is <see cref="ArgumentNullException"/>.
/// </summary>
public class EquityIndexErrorTests
{
    [Fact]
    public void TheIndexRefusesWithQuantLibsTypes()
    {
        Date today = new(27, Month.January, 2023);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure r = new FlatForward(today, 0.03, new Actual365Fixed());
        var index = new EquityIndex("eqIndex", new TARGET(), new EURCurrency(), r);

        ArgumentException invalid = Assert.Throws<ArgumentException>(
            () => index.Fixing(new Date(1, Month.January, 2023), context));
        Assert.Equal("fixingDate", invalid.ParamName);
        Assert.StartsWith("Fixing date January 1st, 2023 is not valid", invalid.Message, StringComparison.Ordinal);

        NanoQLException missing = Assert.Throws<NanoQLException>(() => index.Fixing(new Date(2, Month.January, 2023), context));
        Assert.Equal("Missing eqIndex fixing for January 2nd, 2023", missing.Message);

        NanoQLException noSpot = Assert.Throws<NanoQLException>(() => index.Fixing(new Date(20, Month.May, 2030), context));
        Assert.Equal("Cannot forecast equity index, missing both spot and historical index", noSpot.Message);

        NanoQLException noCurve = Assert.Throws<NanoQLException>(
            () => index.Clone(null, null, 8700.0).Fixing(new Date(20, Month.May, 2030), context));
        Assert.Equal("null interest rate term structure set to this instance of eqIndex", noCurve.Message);

        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => new EquityIndex(null!, new TARGET(), null)).ParamName);
        Assert.Equal(
            "fixingCalendar", Assert.Throws<ArgumentNullException>(() => new EquityIndex("eq", null!, null)).ParamName);
    }
}
