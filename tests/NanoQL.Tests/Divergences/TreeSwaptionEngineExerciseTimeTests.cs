// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A swaption with no exercise time on or after the reference date, priced on a lattice that initializes an asset at
/// a negative time. QuantLib's tree engine initializes the swaption at its last exercise time and rolls it back to the
/// first time not before the reference date, read through <c>*std::find_if</c> (<c>treeswaptionengine.cpp:88-93</c>),
/// which reads past the times when none is: undefined. Its own lattices refuse the negative time first, at the
/// initialization ("using inadequate time grid"), but a lattice of one's own may not, as a short-rate model of one's
/// own may build. nanoql refuses it there, with <see cref="NanoQLException"/>, "no exercise time on or after the
/// reference date".
/// </summary>
public class TreeSwaptionEngineExerciseTimeTests
{
    [Fact]
    public void NoExerciseTimeOnOrAfterTheReferenceDateIsRefused()
    {
        // The curve's reference date is after the exercise, which is after the evaluation date, so the swaption hasn't
        // expired and its exercise time is negative.
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var curve = new FlatForward(new Date(24, Month.January, 2025), 0.03, new Actual365Fixed());
        var model = new HullWhite(curve);
        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(curve))
            .WithFixedRate(0.03)
            .WithEffectiveDate(new Date(22, Month.January, 2025))
            .ToVanillaSwap(context);
        var swaption = new Swaption(swap, new EuropeanExercise(new Date(20, Month.January, 2025)));
        Assert.False(swaption.IsExpired(context));

        // On the model's own lattice, the initialization refuses the negative time first, as QuantLib's does.
        var times = new DiscretizedSwaption(swaption, curve.ReferenceDate, curve.DayCounter, context).MandatoryTimes();
        var grid = new TimeGrid(times.AsSpan(), 10);
        Assert.Throws<NanoQLException>(() => TreeSwaptionEngine.Calculate(swaption, context, model, model.Tree(grid)));

        NanoQLException e = Assert.Throws<NanoQLException>(
            () => TreeSwaptionEngine.Calculate(swaption, context, model, new AnyTimeLattice(grid)));
        Assert.Equal("no exercise time on or after the reference date", e.Message);
    }

    // A lattice that initializes an asset at any time and rolls it back without changing its values.
    internal sealed class AnyTimeLattice(TimeGrid grid) : Lattice(grid)
    {
        public override void Initialize(DiscretizedAsset asset, double time)
        {
            asset.Time = time;
            asset.Reset(1);
        }

        public override void Rollback(DiscretizedAsset asset, double to) => asset.Time = to;

        public override void PartialRollback(DiscretizedAsset asset, double to) => asset.Time = to;

        public override double PresentValue(DiscretizedAsset asset) => 0.0;

        public override Vector Grid(double t) => new(1);
    }
}
