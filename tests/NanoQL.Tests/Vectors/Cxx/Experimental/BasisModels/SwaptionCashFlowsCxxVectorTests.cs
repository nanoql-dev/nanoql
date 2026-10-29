// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swaption cash flow vectors, <c>tests/vectors/cxx/experimental/basismodels/swaptioncfs.json</c>:
/// QuantLib's case's payer swaptions mapped on two curves and one, with continuously and simply compounded spreads.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwaptionCashFlowsCxxVectorTests
{
    [Fact]
    public void SwaptionCashFlowsCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/basismodels/swaptioncfs");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var disc = BasisModelInputs.CurveOf(c.Input("discount"));
            var euribor6m = new Euribor6M(BasisModelInputs.CurveOf(c.Input("projection")));
            JsonElement s = c.Input("swap");
            var start = new Date(s.GetProperty("start").GetInt32());
            var end = new Date(s.GetProperty("end").GetInt32());
            var target = new TARGET();
            var fixedSchedule = new Schedule(
                start, end, new Period(1, TimeUnit.Years), target, BusinessDayConvention.ModifiedFollowing,
                BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, context);
            var floatSchedule = new Schedule(
                start, end, new Period(6, TimeUnit.Months), target, BusinessDayConvention.ModifiedFollowing,
                BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, context);
            var swap = new VanillaSwap(
                SwapType.Payer, s.GetProperty("nominal").GetDouble(), fixedSchedule, s.GetProperty("fixedRate").GetDouble(),
                new Thirty360(Thirty360Convention.BondBasis), floatSchedule, euribor6m, 0.0, euribor6m.DayCounter, context);
            var swaption = new Swaption(swap, new EuropeanExercise(new Date(c.Input("exercise").GetInt32())));
            var cfs = new SwaptionCashFlows(swaption, disc, context, c.Input("contTenorSpread").GetBoolean());
            check.Output(c, "floatTimes", cfs.FloatTimes);
            check.Output(c, "floatWeights", cfs.FloatWeights);
            check.Output(c, "fixedTimes", cfs.FixedTimes);
            check.Output(c, "fixedWeights", cfs.FixedWeights);
            check.Output(c, "annuityWeights", cfs.AnnuityWeights);
            check.Output(c, "exerciseTimes", cfs.ExerciseTimes);
        }

        check.Complete();
    }
}
