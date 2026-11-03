// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="MakeYoYInflationCapFloor"/>: an explicit strike after an at-the-money one, or the reverse, is a
/// <see cref="NanoQLException"/> with QuantLib's "ATM strike already given" and "explicit strike already given". The
/// builder is immutable: each setter returns a modified copy, so a builder kept and reused never changes, and
/// <see cref="MakeYoYInflationCapFloor.WithPricingEngine"/>, which sets nothing the instrument holds, returns it
/// unchanged.
/// </summary>
public class MakeYoYInflationCapFloorTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.June, 2024) };

    [Fact]
    public void TheTwoStrikesExcludeEachOther()
    {
        var nominal = new FlatForward(Context.EvaluationDate, 0.04, new Actual365Fixed());
        Assert.Equal(
            "ATM strike already given",
            Assert.Throws<NanoQLException>(() => Builder().WithAtmStrike(nominal).WithStrike(0.03)).Message);
        Assert.Equal(
            "explicit strike already given",
            Assert.Throws<NanoQLException>(() => Builder().WithStrike(0.03).WithAtmStrike(nominal)).Message);
    }

    [Fact]
    public void AKeptBuilderNeverChanges()
    {
        MakeYoYInflationCapFloor kept = Builder().WithStrike(0.03);
        YoYInflationCapFloor before = kept.ToYoYInflationCapFloor(Context);

        YoYInflationCapFloor other = kept.WithNominal(2e6).AsOptionlet().ToYoYInflationCapFloor(Context);
        YoYInflationCapFloor after = kept.ToYoYInflationCapFloor(Context);

        Assert.Single(other.YoYLeg);
        Assert.Equal(before.YoYLeg.Length, after.YoYLeg.Length);
        Assert.Equal(3, after.YoYLeg.Length);
        Assert.Same(kept, kept.WithPricingEngine(null));
    }

    private static MakeYoYInflationCapFloor Builder() =>
        new(
            YoYInflationCapFloorType.Cap,
            new YoYInflationIndex(new UKRPI()),
            3,
            new UnitedKingdom(),
            new Period(2, TimeUnit.Months),
            CPIInterpolationType.Flat);
}
