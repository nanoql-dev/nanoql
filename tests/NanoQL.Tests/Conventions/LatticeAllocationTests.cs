// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The lattice framework's inner loop allocates nothing (playbook §2's lattice row): a rollback step writes into the
/// asset's spare buffer and swaps the two, which the asset reserves at the widest step its rollback passes through,
/// where QuantLib allocates an array per step. The grid is non-uniform, with two short mandatory steps, so the tree is
/// wider at an earlier time than at the asset's initial one, and buffers sized at the initial step would grow. A
/// Bermudan swaption's swap keeps one discount bond for its coupon events, where QuantLib's builds one per event, its
/// buffers reserved at the widest step of the swap's own rollback. The callable bond's rollback, its callabilities
/// applied and its coupons added in place, allocates nothing either. Each rollback is measured on a fresh asset, after
/// a warm-up on another (<see cref="AllocationAssert"/>).
/// </summary>
public class LatticeAllocationTests
{
    private static readonly YieldTermStructure Curve =
        new FlatForward(new Date(15, Month.January, 2026), 0.04, new Actual365Fixed());

    [Fact]
    public void ADiscountBondsRollbackAllocatesNothing()
    {
        OneFactorModelShortRateTree lattice = Tree();

        Roll(new DiscretizedDiscountBond(), lattice);
        DiscretizedDiscountBond bond = AllocationAssert.AllocatesNothing(
            () =>
            {
                var fresh = new DiscretizedDiscountBond();
                fresh.Initialize(lattice, 5.0);
                return fresh;
            },
            fresh => fresh.Rollback(0.0));

        // the fitted tree reprices the curve's discount bond
        Assert.Equal(Curve.Discount(5.0), bond.PresentValue(), 1e-12);
    }

    [Fact]
    public void AnOptionsRollbackAllocatesNothing()
    {
        OneFactorModelShortRateTree lattice = Tree();

        var warmBond = new DiscretizedDiscountBond();
        Roll(new DiscretizedOption(warmBond, ExerciseType.Bermudan, [1.0, 3.0]), lattice, warmBond);
        DiscretizedOption option = AllocationAssert.AllocatesNothing(
            () =>
            {
                var bond = new DiscretizedDiscountBond();
                var fresh = new DiscretizedOption(bond, ExerciseType.Bermudan, [1.0, 3.0]);
                bond.Initialize(lattice, 5.0);
                fresh.Initialize(lattice, 5.0);
                return fresh;
            },
            fresh => fresh.Rollback(0.0));

        // exercised into the bond, the option is worth the bond
        Assert.Equal(Curve.Discount(5.0), option.PresentValue(), 1e-12);
    }

    [Fact]
    public void ABermudanSwaptionsRollbackAllocatesNothing()
    {
        var context = new EvaluationContext { EvaluationDate = Curve.ReferenceDate };
        var model = new HullWhite(Curve, 0.1, 0.01);
        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve))
            .WithFixedRate(0.04)
            .WithForwardStart(new Period(1, TimeUnit.Years))
            .ToVanillaSwap(context);
        Date[] exerciseDates = [.. swap.FixedLeg.Select(c => ((Coupon)c).AccrualStartDate)];
        var swaption = new Swaption(swap, new BermudanExercise(exerciseDates));
        DiscretizedSwaption Asset() => new(swaption, Curve.ReferenceDate, Curve.DayCounter, context);

        // A short step after a floating coupon's reset between the first and the last exercise, above the tree's dx
        // floor's threshold, widens the tree there, so the swap's coupon events in the rollback fall at steps wider
        // than the swaption's first.
        DayCounter dc = Curve.DayCounter;
        double first = dc.YearFraction(Curve.ReferenceDate, exerciseDates[0]);
        double last = dc.YearFraction(Curve.ReferenceDate, exerciseDates[^1]);
        double reset = dc.YearFraction(Curve.ReferenceDate, ((Coupon)swap.FloatingLeg[5]).AccrualStartDate);
        var grid = new TimeGrid([.. Asset().MandatoryTimes(), reset + 0.01], 20);
        var lattice = (OneFactorModelShortRateTree)model.Tree(grid);
        int widest = 0;
        for (int k = grid.Index(first); k < grid.Index(last); k++)
        {
            widest = Math.Max(widest, lattice.Size(k));
        }

        Assert.True(first < reset && reset < last, $"reset {reset}");
        Assert.True(widest > lattice.Size(grid.Index(last)), $"widest {widest}, first {lattice.Size(grid.Index(last))}");

        DiscretizedSwaption warm = Asset();
        warm.Initialize(lattice, last);
        warm.Rollback(first);
        DiscretizedSwaption asset = AllocationAssert.AllocatesNothing(
            () =>
            {
                DiscretizedSwaption fresh = Asset();
                fresh.Initialize(lattice, last);
                return fresh;
            },
            fresh => fresh.Rollback(first));

        // the tree engine's value on the same lattice
        Assert.Equal(TreeSwaptionEngine.Calculate(swaption, context, model, lattice).NPV, asset.PresentValue());
    }

    [Fact]
    public void ACallableBondsRollbackAllocatesNothing()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2026) };
        var calendar = new TARGET();
        var issue = new Date(15, Month.January, 2026);
        Schedule schedule = new MakeSchedule().From(issue).To(new Date(15, Month.January, 2031))
            .WithCalendar(calendar).WithFrequency(Frequency.Semiannual).WithConvention(BusinessDayConvention.Following)
            .WithRule(DateGenerationRule.Backward).ToSchedule();

        // calls on coupon dates, and a put three days before one, which the asset moves to it
        ImmutableArray<Callability> exercises =
        [
            .. Enumerable.Range(1, 4).Select(i => new Callability(
                new BondPrice(101.0, BondPriceType.Clean), CallabilityType.Call, schedule.Date(2 * i))),
            new Callability(new BondPrice(97.0, BondPriceType.Dirty), CallabilityType.Put, schedule.Date(5) - 3),
        ];
        var bond = new CallableFixedRateBond(
            2, 100.0, schedule, [0.045], new Thirty360(Thirty360Convention.BondBasis), BusinessDayConvention.Following,
            100.0, issue, exercises);

        var model = new HullWhite(Curve, 0.1, 0.01);
        var warm = new DiscretizedCallableFixedRateBond(bond, Curve, context);
        Lattice lattice = model.Tree(new TimeGrid(warm.MandatoryTimes().AsSpan(), 40));
        double redemptionTime = Curve.DayCounter.YearFraction(Curve.ReferenceDate, bond.Redemption().Date);
        warm.Initialize(lattice, redemptionTime);
        warm.Rollback(0.0);

        DiscretizedCallableFixedRateBond asset = AllocationAssert.AllocatesNothing(
            () =>
            {
                var fresh = new DiscretizedCallableFixedRateBond(bond, Curve, context);
                fresh.Initialize(lattice, redemptionTime);
                return fresh;
            },
            fresh => fresh.Rollback(0.0));

        // the asset the engine rolls back on that lattice
        Assert.Equal(
            TreeCallableFixedRateBondEngine.Calculate(bond, context, model, lattice).NPV, asset.PresentValue());
    }

    private static OneFactorModelShortRateTree Tree()
    {
        var model = new HullWhite(Curve, 0.1, 0.01);
        var grid = new TimeGrid([0.25, 0.2525, 1.0, 1.003, 3.0, 5.0], 20);
        var lattice = (OneFactorModelShortRateTree)model.Tree(grid);

        // the widest step is earlier than the last one
        int last = grid.Size - 1;
        int widest = 0;
        for (int k = 0; k < last; k++)
        {
            widest = Math.Max(widest, lattice.Size(k));
        }

        Assert.True(widest > lattice.Size(last), $"widest {widest}, last {lattice.Size(last)}");
        return lattice;
    }

    private static void Roll(DiscretizedAsset asset, Lattice lattice, DiscretizedAsset? underlying = null)
    {
        underlying?.Initialize(lattice, 5.0);
        asset.Initialize(lattice, 5.0);
        asset.Rollback(0.0);
    }
}
