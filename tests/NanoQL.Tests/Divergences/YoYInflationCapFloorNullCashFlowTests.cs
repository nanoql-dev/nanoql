// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A year-on-year cap on a leg holding a null cash flow. QuantLib builds the instrument, and its <c>isExpired()</c>
/// dereferences each cash flow (<c>inflationcapfloor.cpp:94-99</c>), which is undefined for a null one; the wheel
/// refuses <c>None</c> in a <c>Leg</c>. nanoql refuses it at construction with <see cref="ArgumentException"/>, "null
/// cash flow in leg", from every constructor, naming the leg, as S7's <see cref="CapFloor"/> does.
/// </summary>
public class YoYInflationCapFloorNullCashFlowTests
{
    [Fact]
    public void ANullCashFlowIsRefusedAtConstruction()
    {
        EvaluationContext context = InflationFixture.Context();
        ImmutableArray<CashFlow> leg = new YoYInflationLeg(
                new Schedule(
                    InflationFixture.Today,
                    new Date(15, Month.June, 2026),
                    new Period(1, TimeUnit.Years),
                    new UnitedKingdom(),
                    BusinessDayConvention.Unadjusted,
                    BusinessDayConvention.Unadjusted,
                    DateGenerationRule.Forward,
                    false,
                    context),
                new UnitedKingdom(),
                new YoYInflationIndex(new UKRPI()),
                new Period(2, TimeUnit.Months),
                CPIInterpolationType.Flat)
            .WithNotionals(1e6)
            .WithPaymentDayCounter(new Actual365Fixed())
            .ToLeg();
        ImmutableArray<CashFlow> withNull = leg.Add(null!);

        Action[] builds =
        [
            () => _ = new YoYInflationCap(withNull, [0.03]),
            () => _ = new YoYInflationFloor(withNull, [0.03]),
            () => _ = new YoYInflationCollar(withNull, [0.04], [0.02]),
            () => _ = new YoYInflationCapFloor(YoYInflationCapFloorType.Cap, withNull, [0.03], []),
            () => _ = new YoYInflationCapFloor(YoYInflationCapFloorType.Floor, withNull, [0.03]),
        ];
        foreach (Action build in builds)
        {
            Assert.Throws<ArgumentException>(build);
        }

        // The leg without it builds.
        Assert.Equal(2, new YoYInflationCap(leg, [0.03]).YoYLeg.Length);
    }
}
