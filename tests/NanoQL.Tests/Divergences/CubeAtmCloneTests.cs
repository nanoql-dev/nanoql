// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The cube's at-the-money strike (the S-g1d plan's G1R5 (v), #2849): QuantLib 1.44 builds a plain SwapIndex from the
/// base index's conventions (<c>swaptionvolcube.cpp:89-145</c>), where its master, after the pin, clones the base to the
/// tenor (<c>base->clone(swapTenor)</c>, merged as 87065c808), so a base index that overrides <c>clone</c> gives its own
/// clone's fixing. For every swap index QuantLib defines the two agree, the overnight-indexed one included, since none
/// overrides <c>clone(tenor)</c>; nanoql's cube clones the base, as the master. QuantLib's check: the master's code, and
/// for QuantLib's indices 1.44's bits.
/// </summary>
public class CubeAtmCloneTests
{
    [Fact]
    public void TheAtmStrikeIsTheBaseIndexsClonesFixing()
    {
        EvaluationContext context = CubeFixture.Context;
        YieldTermStructure curve = FlatForward.Create(0, new NullCalendar(), 0.05, new Actual365Fixed(), context);
        var own = new SemiannualFixedLegSwapIndex(new Period(10, TimeUnit.Years), curve);
        var cube = new InterpolatedSwaptionVolatilityCube(
            CubeFixture.AtmMatrix(), CubeFixture.Options, CubeFixture.Swaps, CubeFixture.StrikeSpreads, CubeFixture.VolSpreads,
            own, CubeFixture.Index(1), false, context);
        Date option = cube.OptionDateFromTenor(new Period(5, TimeUnit.Years));
        var swapTenor = new Period(10, TimeUnit.Years);

        Assert.Equal(own.Clone(swapTenor).Fixing(option, context), cube.AtmStrike(option, swapTenor, context));

        // QuantLib 1.44's plain index of the base's conventions, an annual fixed leg.
        double plain = new SwapIndex(
            own.FamilyName, swapTenor, own.FixingDays, own.Currency, own.FixingCalendar, own.FixedLegTenor, own.FixedLegConvention,
            own.DayCounter, own.IborIndex).Fixing(option, context);
        Assert.True(Math.Abs(cube.AtmStrike(option, swapTenor, context) - plain) > 1e-5);
    }

    // A swap index of one's own whose clone to a tenor pays its fixed leg semiannually.
    private sealed class SemiannualFixedLegSwapIndex(Period tenor, YieldTermStructure curve)
        : SwapIndex(
            "OwnSwap", tenor, 2, new EURCurrency(), new TARGET(), new Period(1, TimeUnit.Years), BusinessDayConvention.ModifiedFollowing,
            new Thirty360(Thirty360Convention.BondBasis), new Euribor6M(curve))
    {
        public override SwapIndex Clone(Period tenor) => new(
            FamilyName, tenor, FixingDays, Currency, FixingCalendar, new Period(6, TimeUnit.Months), FixedLegConvention, DayCounter, IborIndex);
    }
}
