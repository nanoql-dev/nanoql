// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A stripped optionlet's fixing times after the evaluation date moves. QuantLib's <c>StrippedOptionlet</c> computes its
/// times from the evaluation date at construction and never again (<c>strippedoptionlet.cpp:77-81</c>), while its
/// adapter's reference date moves with the evaluation date: on 16 June 2025, with two settlement days, fixing dates 16
/// December 2025, 16 June 2026 and 16 December 2026, volatilities 20%/19%, 22%/21% and 25%/24% at 2% and 3%, the wheel's
/// times are 0.4958904109589041, 0.9945205479452055 and 1.4958904109589042 and its adapter gives 0.22 at the second date
/// and 2%; with the evaluation date moved to 16 September the times stay, the adapter's reference date is 18 September,
/// and it gives 0.2098901098901099 there; a stripped optionlet built on 16 September has the times 0.24383561643835616,
/// 0.7424657534246575 and 1.2438356164383562 and gives 0.22. nanoql's objects have fixed dates (R1): an adapter is built
/// under a context. The stripped optionlet's times are frozen as QuantLib's are, so its adapter under the later context
/// gives QuantLib's moved value, and the optionlet and adapter built under that context QuantLib's fresh one.
/// </summary>
public class StrippedOptionletTimesTests
{
    [Fact]
    public void TheTimesAreTheConstructionsAndTheAdapterIsTheContexts()
    {
        var june = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };
        var september = new EvaluationContext { EvaluationDate = new Date(16, Month.September, 2025) };
        Date d2 = new(16, Month.June, 2026);
        StrippedOptionlet Optionlet(EvaluationContext context) => new(
            2, new TARGET(), BusinessDayConvention.Following, StripperFixture.Index(june),
            [new Date(16, Month.December, 2025), d2, new Date(16, Month.December, 2026)], [0.02, 0.03],
            [[0.20, 0.19], [0.22, 0.21], [0.25, 0.24]], new Actual365Fixed(), context);

        StrippedOptionlet first = Optionlet(june);
        Assert.Equal([0.4958904109589041, 0.9945205479452055, 1.4958904109589042], first.OptionletFixingTimes);
        Assert.Equal(0.22, StrippedOptionletAdapter.Create(first, june).Volatility(d2, 0.02));

        // QuantLib's adapter after the move, on the frozen times.
        StrippedOptionletAdapter moved = StrippedOptionletAdapter.Create(first, september);
        Assert.Equal(new Date(18, Month.September, 2025), moved.ReferenceDate);
        Assert.Equal(0.2098901098901099, moved.Volatility(d2, 0.02));

        // QuantLib's fresh optionlet on the later date.
        StrippedOptionlet fresh = Optionlet(september);
        Assert.Equal([0.24383561643835616, 0.7424657534246575, 1.2438356164383562], fresh.OptionletFixingTimes);
        Assert.Equal(0.22, StrippedOptionletAdapter.Create(fresh, september).Volatility(d2, 0.02));
    }
}
