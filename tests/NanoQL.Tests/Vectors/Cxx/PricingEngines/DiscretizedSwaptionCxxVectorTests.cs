// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's discretized swaption vectors, <c>tests/vectors/cxx/pricingengines/swaption/discretizedswaption.json</c>:
/// the mandatory times of <see cref="DiscretizedSwaption"/> and of the <see cref="DiscretizedSwap"/> built from the
/// swaption's swap, for European and Bermudan swaptions whose exercise dates snap the swap's or not, on two reference
/// dates and day counters; their values and present values after a rollback on Hull-White's and Vasicek's trees; the
/// known amounts of the coupons reset before the reference date, today's under the context's
/// <see cref="EvaluationContext.IncludeTodaysCashFlows"/>; and the refusal of a past floating coupon whose amount isn't
/// known.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscretizedSwaptionCxxVectorTests
{
    [Fact]
    public void DiscretizedSwaptionCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/swaption/discretizedswaption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
            Date referenceDate = c.DateInput("referenceDate");
            DayCounter dayCounter = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            HullWhite hullWhite = CalibrationInputs.HullWhiteOf(c.Input("hullWhite"), CashFlowInputs.CurveOf(c.Input("hullWhite").GetProperty("curve")));
            int steps = c.Int("steps");
            DiscretizedSwaption Swaption() => new(swaption, referenceDate, dayCounter, context);
            DiscretizedSwap Swap() => new(swaption.Underlying, referenceDate, dayCounter, context);

            if (c.Inputs.TryGetProperty("method", out JsonElement method))
            {
                double[] times = c.Doubles("gridTimes");
                DiscretizedAsset asset = method.GetString() == "DiscretizedSwap::rollback" ? Swap() : Swaption();
                check.Output(c, "presentValue", () => Rollback(asset, hullWhite.Tree(new TimeGrid(times, steps)), times.Max(), 0.0).PresentValue);
                continue;
            }

            double[] swaptionTimes = [.. Swaption().MandatoryTimes()];
            double[] swapTimes = [.. Swap().MandatoryTimes()];
            check.Output(c, "swaptionTimes", swaptionTimes);
            check.Output(c, "swapTimes", swapTimes);

            Lattice hullWhiteTree = hullWhite.Tree(new TimeGrid(swaptionTimes, steps));
            double from = c.Double("swaptionFrom");
            (double[] values, double presentValue) = Rollback(Swaption(), hullWhiteTree, from, c.Double("swaptionTo"));
            check.Output(c, "swaptionValues", values);
            check.Output(c, "swaptionPresentValue", presentValue);
            (values, presentValue) = Rollback(Swaption(), hullWhiteTree, from, 0.0);
            check.Output(c, "swaptionValuesAtZero", values);
            check.Output(c, "swaptionPresentValueAtZero", presentValue);

            Vasicek vasicek = CalibrationInputs.VasicekOf(c.Input("vasicek"));
            check.Output(
                c,
                "vasicekPresentValueAtZero",
                Rollback(Swaption(), vasicek.Tree(new TimeGrid(swaptionTimes, steps)), from, 0.0).PresentValue);

            (values, presentValue) = Rollback(Swap(), hullWhite.Tree(new TimeGrid(swapTimes, steps)), c.Double("swapFrom"), 0.0);
            check.Output(c, "swapValuesAtZero", values);
            check.Output(c, "swapPresentValueAtZero", presentValue);
        }

        check.Complete();
    }

    // The asset initialized on the lattice at a time and rolled back to another: its values and present value there.
    private static (double[] Values, double PresentValue) Rollback(DiscretizedAsset asset, Lattice lattice, double from, double to)
    {
        asset.Initialize(lattice, from);
        asset.Rollback(to);
        return (asset.Values.ToArray(), asset.PresentValue());
    }
}
