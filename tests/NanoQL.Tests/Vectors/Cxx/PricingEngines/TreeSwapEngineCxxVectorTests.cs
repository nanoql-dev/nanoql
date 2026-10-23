// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's TreeVanillaSwapEngine vectors, <c>tests/vectors/cxx/pricingengines/swap/treeswapengine.json</c>:
/// <see cref="TreeVanillaSwapEngine"/>, which the wheel doesn't expose, on forward-starting and seasoned swaps, on
/// Hull-White's fitted tree with time steps and on a time grid, and on Vasicek's with the curve given; spot-dated swaps
/// left out (<c>Divergences/TreeKnownCouponTests.cs</c>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TreeSwapEngineCxxVectorTests
{
    [Fact]
    public void TreeSwapEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/swap/treeswapengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            HullWhite hullWhite = CalibrationInputs.HullWhiteOf(c.Input("hullWhite"), curve);
            Vasicek vasicek = CalibrationInputs.VasicekOf(c.Input("vasicek"));
            double[] gridTimes = c.Doubles("gridTimes");
            int gridSteps = c.Int("gridSteps");
            VanillaSwap Swap() => (VanillaSwap)SwaptionInputs.SwaptionOf(c.Input("swaption"), context).Underlying;

            check.Output(c, "hullWhiteSteps", c.Input("hullWhiteSteps").EnumerateArray().Select(steps => (Func<object?>)(() =>
                TreeVanillaSwapEngine.Calculate(Swap(), context, hullWhite, steps.GetInt32()).NPV)));
            check.Output(
                c,
                "hullWhiteGrid",
                () => TreeVanillaSwapEngine.Calculate(Swap(), context, hullWhite, new TimeGrid(gridTimes, gridSteps)).NPV);
            check.Output(c, "vasicekSteps", () => TreeVanillaSwapEngine.Calculate(Swap(), context, vasicek, c.Int("vasicekSteps"), curve).NPV);
            check.Output(
                c,
                "vasicekGrid",
                () => TreeVanillaSwapEngine.Calculate(Swap(), context, vasicek, new TimeGrid(gridTimes, gridSteps), curve).NPV);
        }

        check.Complete();
    }
}
