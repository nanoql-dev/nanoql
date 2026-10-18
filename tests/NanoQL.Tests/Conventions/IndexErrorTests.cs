// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the fixing store and of the indexes' reads, by type, message and parameter, which a vector's
/// <c>{"throws": true}</c> can't tell apart: <c>QL_REQUIRE</c> on an argument is <see cref="ArgumentException"/>, any
/// other check <see cref="NanoQLException"/>, with QuantLib's texts.
/// </summary>
public class IndexErrorTests
{
    private static readonly Date Thursday = new(11, Month.January, 2024);
    private static readonly Date Saturday = new(13, Month.January, 2024);
    private static readonly Date Monday = new(15, Month.January, 2024);

    [Fact]
    public void TheStoresRefusals()
    {
        IborIndex euribor = Euribor();
        IndexFixings store = IndexFixings.Empty.AddFixing(euribor, Thursday, 0.01);

        ArgumentException invalid = Assert.Throws<ArgumentException>(
            () => store.AddFixings(euribor, [Thursday, Saturday], [0.01, 0.02]));
        Assert.Equal("fixingDates", invalid.ParamName);
        Assert.StartsWith(
            "At least one invalid fixing provided: Saturday January 13th, 2024, 0.02",
            invalid.Message,
            StringComparison.Ordinal);

        // A batch with both an invalid date and a conflicting duplicate gives the invalid date's text: QuantLib checks
        // the invalid fixings first (indexmanager.hpp:106-112).
        ArgumentException both = Assert.Throws<ArgumentException>(
            () => store.AddFixings(euribor, [Thursday, Saturday], [0.05, 0.02]));
        Assert.Equal("fixingDates", both.ParamName);
        Assert.StartsWith(
            "At least one invalid fixing provided: Saturday January 13th, 2024, 0.02",
            both.Message,
            StringComparison.Ordinal);

        ArgumentException one = Assert.Throws<ArgumentException>(() => store.AddFixing(euribor, Saturday, 0.02));
        Assert.Equal("fixingDate", one.ParamName);

        ArgumentException duplicated = Assert.Throws<ArgumentException>(() => store.AddFixing(euribor, Thursday, 0.02));
        Assert.Equal("fixingDate", duplicated.ParamName);
        Assert.StartsWith(
            "At least one duplicated fixing provided: January 11th, 2024, 0.02 while 0.01 value is already present",
            duplicated.Message,
            StringComparison.Ordinal);

        ArgumentException series = Assert.Throws<ArgumentException>(
            () => store.AddFixings(euribor, new TimeSeries<double>([Thursday], [0.02])));
        Assert.Equal("fixings", series.ParamName);

        ArgumentException fewer = Assert.Throws<ArgumentException>(() => store.AddFixings(euribor, [Thursday], []));
        Assert.Equal("fixings", fewer.ParamName);
        Assert.StartsWith("fewer values than dates", fewer.Message, StringComparison.Ordinal);

        var weighted = new NoNativeFixings();
        string native = "native fixings not allowed for Weighted6M Actual/360; refer to underlying indices instead";
        Assert.Equal(native, Assert.Throws<NanoQLException>(() => store.AddFixing(weighted, Thursday, 0.01)).Message);
        Assert.Equal(native, Assert.Throws<NanoQLException>(() => store.AddFixings(weighted, [Thursday], [0.01])).Message);
        Assert.Equal(native, Assert.Throws<NanoQLException>(() => store.ClearFixings(weighted)).Message);

        Assert.Equal("index", Assert.Throws<ArgumentNullException>(() => store.AddFixing(null!, Thursday, 0.01)).ParamName);
    }

    [Fact]
    public void TheIndexReadsRefusals()
    {
        IborIndex euribor = Euribor();
        var context = new EvaluationContext { EvaluationDate = Monday };

        ArgumentException fixing = Assert.Throws<ArgumentException>(() => euribor.Fixing(Saturday, context));
        Assert.Equal("fixingDate", fixing.ParamName);
        Assert.StartsWith("Fixing date January 13th, 2024 is not valid", fixing.Message, StringComparison.Ordinal);

        ArgumentException past = Assert.Throws<ArgumentException>(() => euribor.PastFixing(Saturday, context));
        Assert.Equal("fixingDate", past.ParamName);
        Assert.StartsWith("January 13th, 2024 is not a valid fixing date", past.Message, StringComparison.Ordinal);

        ArgumentException value = Assert.Throws<ArgumentException>(() => euribor.ValueDate(Saturday));
        Assert.Equal("fixingDate", value.ParamName);
        Assert.StartsWith("January 13th, 2024 is not a valid fixing date", value.Message, StringComparison.Ordinal);

        Assert.Equal(
            "Missing Euribor6M Actual/360 fixing for January 11th, 2024",
            Assert.Throws<NanoQLException>(() => euribor.Fixing(Thursday, context)).Message);
        Assert.Equal(
            "Missing Euribor6M Actual/360 fixing for January 15th, 2024",
            Assert.Throws<NanoQLException>(
                () => euribor.Fixing(Monday, context with { EnforcesTodaysHistoricFixings = true })).Message);
        Assert.Equal(
            "null term structure set to this instance of Euribor6M Actual/360",
            Assert.Throws<NanoQLException>(() => euribor.Fixing(Monday, context)).Message);

        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => euribor.Fixing(Monday, null!)).ParamName);
    }

    private static IborIndex Euribor() => new(
        "Euribor", new Period(6, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing, true,
        new Actual360());

    private sealed class NoNativeFixings() : IborIndex(
        "Weighted", new Period(6, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing,
        true, new Actual360())
    {
        public override bool AllowsNativeFixings => false;
    }
}
