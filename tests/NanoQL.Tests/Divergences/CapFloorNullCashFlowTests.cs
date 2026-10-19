// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A cap on a leg holding a null cash flow. QuantLib builds the instrument, registering with each cash flow, and its
/// <c>isExpired()</c> dereferences each (<c>capfloor.cpp:142-146, 172-177</c>), which is undefined for a null one; the
/// wheel refuses <c>None</c> in a <c>Leg</c> ("in method 'new_Leg', argument 1 of type 'std::vector&lt;
/// ext::shared_ptr&lt; CashFlow &gt; &gt; const &amp;'"). nanoql refuses it at construction with
/// <see cref="ArgumentException"/>, "null cash flow in leg", as a swap does, from every constructor, naming the leg; a
/// <c>default</c> array, which isn't a leg, is refused alike.
/// </summary>
public class CapFloorNullCashFlowTests
{
    [Fact]
    public void ANullCashFlowIsRefusedAtConstruction()
    {
        var today = new Date(16, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var index = new Euribor6M(new FlatForward(today, 0.04, new Actual365Fixed()));
        var schedule = new Schedule(
            new Date(18, Month.June, 2025), new Date(18, Month.June, 2026), new Period(6, TimeUnit.Months), new TARGET(),
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false,
            context);
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, index).WithNotionals([1.0]).ToLeg(context);
        ImmutableArray<CashFlow> withNull = leg.Add(null!);

        Action[] builds =
        [
            () => _ = new Cap(withNull, [0.03]),
            () => _ = new Floor(withNull, [0.03]),
            () => _ = new Collar(withNull, [0.04], [0.02]),
            () => _ = new CapFloor(CapFloorType.Cap, withNull, [0.03], []),
            () => _ = new CapFloor(CapFloorType.Floor, withNull, [0.03]),
        ];
        foreach (Action build in builds)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(build);
            Assert.Equal("floatingLeg", e.ParamName);
            Assert.StartsWith("null cash flow in leg", e.Message, StringComparison.Ordinal);
        }

        Assert.Equal("floatingLeg", Assert.Throws<ArgumentException>(() => new Cap(default, [0.03])).ParamName);

        // The leg without it builds.
        Assert.Equal(2, new Cap(leg, [0.03]).FloatingLeg.Length);
    }
}
