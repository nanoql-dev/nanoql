// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The tree engine's spread on a lattice reused across calls. QuantLib's time-grid engine builds its tree once and keeps
/// it (<c>latticeshortratemodelengine.hpp:80-95</c>), and sets a spread on it only when the spread isn't 0
/// (<c>treecallablebondengine.cpp:69-74</c>), so the solver's last trial spread stays on the tree: on testCached's
/// callable bond with an 80-step grid through its mandatory times, after a clean price at a spread, the wheel's OAS for
/// a clean price of 104 is 0.0, where its time-steps engine gives 0.011561251367301573, and its NPV after the OAS is
/// 11205.506593769049, against 11205.506591750021 before. nanoql's lattices are immutable and its engines hold nothing:
/// the lattice overload, given one tree for every call, and the grid overload price at the spread each call is given,
/// so the grid engine's OAS is the time-steps engine's, and the NPV after it is the NPV before.
/// </summary>
public class GridEngineSpreadTests
{
    [Fact]
    public void ASharedLatticeKeepsNoSpreadAcrossCalls()
    {
        var today = new Date(3, Month.June, 2004);
        var context = new EvaluationContext { EvaluationDate = today };
        var calendar = new TARGET();
        Date settlement = calendar.Advance(today, 3, TimeUnit.Days);
        var curve = new FlatForward(settlement, 0.032, new Actual365Fixed());
        var model = new HullWhite(curve);
        Date issue = calendar.Adjust(today - 100);
        Date maturity = calendar.Advance(issue, 10, TimeUnit.Years);
        Schedule schedule = new MakeSchedule().From(issue).To(maturity).WithCalendar(calendar)
            .WithFrequency(Frequency.Semiannual).WithConvention(BusinessDayConvention.ModifiedFollowing)
            .WithRule(DateGenerationRule.Backward).ToSchedule();
        ImmutableArray<Callability> calls =
        [
            .. Enumerable.Range(1, 4).Select(i => new Callability(
                new BondPrice(110.0, BondPriceType.Clean), CallabilityType.Call,
                calendar.Advance(issue, 2 * i, TimeUnit.Years))),
        ];
        var bond = new CallableFixedRateBond(
            3, 10000.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis),
            BusinessDayConvention.ModifiedFollowing, 100.0, issue, calls);

        // the wheel's grid: the bond's mandatory times, with 80 steps
        var grid = new TimeGrid(new DiscretizedCallableFixedRateBond(bond, curve, context).MandatoryTimes().AsSpan(), 80);
        Lattice lattice = model.Tree(grid);
        CallableBondResults Shared(CallableBond b, EvaluationContext x, double s) =>
            TreeCallableFixedRateBondEngine.Calculate(b, x, model, lattice, curve, s);
        CallableBondResults Grid(CallableBond b, EvaluationContext x, double s) =>
            TreeCallableFixedRateBondEngine.Calculate(b, x, model, grid, curve, s);
        CallableBondResults Steps(CallableBond b, EvaluationContext x, double s) =>
            TreeCallableFixedRateBondEngine.Calculate(b, x, model, 80, curve, s);

        var dc = new Actual365Fixed();
        foreach (Func<CallableBond, EvaluationContext, double, CallableBondResults> engine in
                 new Func<CallableBond, EvaluationContext, double, CallableBondResults>[] { Shared, Grid })
        {
            double before = engine(bond, context, 0.0).NPV!.Value;
            _ = bond.CleanPriceOAS(0.0125, curve, dc, Compounding.Compounded, Frequency.Semiannual, engine, context);
            double oas = bond.OAS(104.0, curve, dc, Compounding.Compounded, Frequency.Semiannual, engine, context);
            double after = engine(bond, context, 0.0).NPV!.Value;

            // QuantLib's grid engine: an OAS of 0.0 and an NPV of 11205.506593769049 after it
            Assert.Equal(0.011561251367301573, oas);
            Assert.Equal(
                bond.OAS(104.0, curve, dc, Compounding.Compounded, Frequency.Semiannual, Steps, context), oas);
            Assert.Equal(11205.506591750021, before);
            Assert.Equal(before, after);
            Assert.NotEqual(11205.506593769049, after);
        }
    }
}
