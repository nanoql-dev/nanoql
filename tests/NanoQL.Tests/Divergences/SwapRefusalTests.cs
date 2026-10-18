// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The swaps' and their builders' undefined paths, each refused before the undefined step (the playbook's
/// undefined-path rule).
/// </summary>
public class SwapRefusalTests
{
    private static readonly Date Today = new(15, January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly FlatForward Curve = new(Today, 0.03, new Actual365Fixed());

    // M1: QuantLib's builder reads overnightIndex->fixingCalendar() and dayCounter() in its member initializers
    // (makeois.cpp:33-38), a null dereference, undefined under std::shared_ptr and a Boost assertion by default.
    [Fact]
    public void MakeOISRefusesANullIndex()
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() =>
            new MakeOIS(new Period(1, TimeUnit.Years), null!));
        Assert.Equal("overnightIndex", e.ParamName);
    }

    // M2: QuantLib's builder reads index->fixingCalendar(), tenor(), businessDayConvention() and dayCounter() in its
    // member initializers (makevanillaswap.cpp:40-47), a null dereference.
    [Fact]
    public void MakeVanillaSwapRefusesANullIndex()
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() =>
            new MakeVanillaSwap(new Period(5, TimeUnit.Years), null!));
        Assert.Equal("index", e.ParamName);
    }

    // F1: QuantLib's settlement, lookback and lockout days are Natural, and a swap's leg index Size, unsigned types; a
    // negative value from C++ wraps, so MakeVanillaSwap and MakeOIS advance the spot date by about 4.3 billion days
    // (makevanillaswap.cpp:89, makeois.cpp:83-84), the swap's coupons are built with a wrapped lookback or lockout
    // (overnightindexedswap.cpp:174-175), and swap->leg(j) refuses a wrapped leg index by its wrapped value
    // (swap.hpp:111-113).
    // nanoql refuses a negative value on entry.
    [Fact]
    public void NegativeCountsAreRefused()
    {
        var estr = new Estr(Curve);
        var schedule = new Schedule(
            Today, new Date(15, January, 2026), new Period(1, TimeUnit.Years), new TARGET(),
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward, false, Context);

        AssertRefused("settlementDays", () =>
            new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve)).WithSettlementDays(-1));
        AssertRefused("settlementDays", () =>
            new MakeOIS(new Period(1, TimeUnit.Years), estr).WithSettlementDays(-1));
        AssertRefused("lookbackDays", () => new MakeOIS(new Period(1, TimeUnit.Years), estr).WithLookbackDays(-1));
        AssertRefused("lockoutDays", () => new MakeOIS(new Period(1, TimeUnit.Years), estr).WithLockoutDays(-1));
        AssertRefused("lookbackDays", () =>
            new OvernightIndexedSwap(SwapType.Payer, 1e6, schedule, 0.03, new Actual360(), estr, lookbackDays: -1));
        AssertRefused("lockoutDays", () =>
            new OvernightIndexedSwap(SwapType.Payer, 1e6, schedule, 0.03, new Actual360(), estr, lockoutDays: -1));

        var swap = new OvernightIndexedSwap(SwapType.Payer, 1e6, schedule, 0.03, new Actual360(), estr);
        AssertRefused("leg", () =>
            CouponPricerFunctions.SetCouponPricer(swap, -1, new CompoundingOvernightIndexedCouponPricer()));

        // Zero stays QuantLib's.
        Assert.Equal(
            Today,
            new MakeOIS(new Period(1, TimeUnit.Years), estr).WithFixedRate(0.03).WithSettlementDays(0)
                .ToOvernightIndexedSwap(Context).StartDate());
    }

    private static void AssertRefused(string paramName, Func<object> create) =>
        Assert.Equal(paramName, Assert.Throws<ArgumentOutOfRangeException>(create).ParamName);
}
