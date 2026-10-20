// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="InflationTermStructureExtensions.SetSeasonality"/> returns a copy and leaves its receiver unchanged,
/// whether it accepts the seasonality or refuses it. QuantLib's <c>setSeasonality</c> changes its curve, and assigns
/// before it checks (<c>inflationtermstructure.cpp:78-82</c>), while <c>MultiplicativePriceSeasonality::isConsistent</c>
/// throws rather than returning false (<c>seasonality.cpp:82-84</c>), so a refused seasonality stays on QuantLib's
/// curve: a zero curve flat at 3%, refused a 24-factor seasonality inconsistent with it, then has a seasonality, and its
/// zero rate on 1 May 2026 is 0.030493063761148242 on the wheel, not 0.03.
/// </summary>
public class SetSeasonalityReceiverTests
{
    [Fact]
    public void TheReceiverIsUnchangedWhetherTheSeasonalityIsAcceptedOrRefused()
    {
        var reference = new Date(15, Month.June, 2024);
        var context = new EvaluationContext { EvaluationDate = reference };
        Date[] dates = [new(1, Month.April, 2024), new(1, Month.April, 2030)];
        var curve = new ZeroInflationCurve(reference, dates, [0.03, 0.03], Frequency.Monthly, new Actual365Fixed());
        double[] twelve = [.. Enumerable.Range(0, 12).Select(i => 1.0 + (i * 0.001))];
        var seasonalityBase = new Date(1, Month.January, 2024);
        var d = new Date(1, Month.May, 2026);

        ZeroInflationCurve seasonal = curve.SetSeasonality(
            new MultiplicativePriceSeasonality(seasonalityBase, Frequency.Monthly, twelve), context);
        Assert.True(seasonal.HasSeasonality);
        Assert.False(curve.HasSeasonality);
        Assert.Equal(0.03, curve.ZeroRate(d));

        var inconsistent = new MultiplicativePriceSeasonality(
            seasonalityBase, Frequency.Monthly, [.. twelve, .. twelve.Reverse()]);
        Assert.Throws<NanoQLException>(() => curve.SetSeasonality(inconsistent, context));

        // QuantLib's: hasSeasonality() true, zeroRate 0.030493063761148242
        Assert.False(curve.HasSeasonality);
        Assert.Equal(0.03, curve.ZeroRate(d));
    }
}
