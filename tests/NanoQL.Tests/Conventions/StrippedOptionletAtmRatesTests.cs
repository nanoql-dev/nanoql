// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A stripped base's at-the-money rates read the context they are given (PR1, S7D16). A
/// <see cref="StrippedOptionlet"/>'s are the index's fixings on its dates, read at each call as QuantLib reads them
/// (<c>strippedoptionlet.cpp:161-166</c>): forecast on 16 June 2025, 0.0398481233043659 and 0.03985031410154605 at 16
/// December 2025 and 16 June 2026, the wheel's; under a later context whose evaluation date is past the first date they
/// need that date's fixing, "Missing Euribor6M Actual/360 fixing for December 16th, 2025" without it, as the wheel's
/// do. A stripper's are the rates its strip computed, whatever the context.
/// </summary>
public class StrippedOptionletAtmRatesTests
{
    [Fact]
    public void TheRatesReadTheContextTheyAreGiven()
    {
        var june = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };
        var january = new EvaluationContext { EvaluationDate = new Date(5, Month.January, 2026) };
        IborIndex index = StripperFixture.Index(june);
        Date d1 = new(16, Month.December, 2025), d2 = new(16, Month.June, 2026);
        var optionlet = new StrippedOptionlet(
            2, new TARGET(), BusinessDayConvention.Following, index, [d1, d2], [0.02, 0.03], [[0.2, 0.2], [0.2, 0.2]],
            new Actual365Fixed(), june);

        Assert.Equal([0.0398481233043659, 0.03985031410154605], optionlet.AtmOptionletRates(june));

        NanoQLException e = Assert.Throws<NanoQLException>(() => optionlet.AtmOptionletRates(january));
        Assert.Equal("Missing Euribor6M Actual/360 fixing for December 16th, 2025", e.Message);

        EvaluationContext withFixing = january with { Fixings = january.Fixings.AddFixing(index, d1, 0.031) };
        Assert.Equal(0.031, optionlet.AtmOptionletRates(withFixing)[0]);

        OptionletStripper1 stripper = StripperFixture.Strip(june);
        Assert.Equal(stripper.AtmOptionletRates(june), stripper.AtmOptionletRates(january));
        Assert.Throws<ArgumentNullException>(() => optionlet.AtmOptionletRates(null!));
    }
}
