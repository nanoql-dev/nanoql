// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The refusal of a stripped optionlet's non-increasing strikes at a fixing date after the first. QuantLib checks each
/// date's strikes but prints the first date's (<c>strippedoptionlet.cpp:106-113</c>), and reads past them where the
/// first date has fewer: strikes 2%, 3%, 4% at the first date and 2%, 3.5%, 3% at the second give QuantLib's "non
/// increasing strikes at date 1: 2nd is 3.000000 %, 3rd is 4.000000 %", the first date's. nanoql prints the offending
/// date's strikes, "non increasing strikes at date 1: 2nd is 3.500000 %, 3rd is 3.000000 %", and a shorter first date
/// reads nothing past its end.
/// </summary>
public class StrippedOptionletStrikesTextTests
{
    [Fact]
    public void TheTextPrintsTheOffendingDatesStrikes()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };
        Date[] dates = [new Date(16, Month.December, 2025), new Date(16, Month.June, 2026)];
        StrippedOptionlet Optionlet(double[][] strikes) => new(
            2, new TARGET(), BusinessDayConvention.Following, StripperFixture.Index(context), dates, strikes,
            strikes.Select(row => row.Select(_ => 0.2)), new Actual365Fixed(), context);

        ArgumentException e = Assert.Throws<ArgumentException>(() => Optionlet([[0.02, 0.03, 0.04], [0.02, 0.035, 0.03]]));
        Assert.Equal("strikes", e.ParamName);
        Assert.StartsWith(
            "non increasing strikes at date 1: 2nd is 3.500000 %, 3rd is 3.000000 %", e.Message, StringComparison.Ordinal);

        // A first date with fewer strikes: QuantLib reads its third strike past its end.
        e = Assert.Throws<ArgumentException>(() => Optionlet([[0.02, 0.03], [0.02, 0.035, 0.03]]));
        Assert.StartsWith(
            "non increasing strikes at date 1: 2nd is 3.500000 %, 3rd is 3.000000 %", e.Message, StringComparison.Ordinal);
    }
}
