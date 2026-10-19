// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The tree swaption engine vectors, <c>tests/vectors/pricingengines/swaption/treeswaptionengine.json</c>: European,
/// Bermudan and American swaptions, their exercise dates snapping the swap's or not, on Hull-White's fitted tree and
/// Vasicek's tree, with time steps and on a time grid through the swaptions' times; QuantLib's grid engine pricing
/// again on the lattice it built, which is the oracle of nanoql's lattice overload; and the engine's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TreeSwaptionEngineVectorTests
{
    [Fact]
    public void TreeSwaptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swaption/treeswaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
            YieldTermStructure discountCurve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            HullWhite hullWhite = CalibrationInputs.HullWhiteOf(c.Input("hullWhite"), discountCurve);
            Vasicek vasicek = CalibrationInputs.VasicekOf(c.Input("vasicek"));
            int gridSteps = c.Int("gridSteps");
            double[] gridTimes = c.Doubles("gridTimes");
            if (c.Inputs.TryGetProperty("model", out JsonElement kind))
            {
                ShortRateModel model = kind.GetString() == "HullWhite" ? hullWhite : vasicek;
                JsonElement engine = c.Input("engine");
                check.Output(
                    c,
                    "NPV",
                    () => engine.ValueKind == JsonValueKind.Number
                        ? TreeSwaptionEngine.Calculate(swaption, context, model, engine.GetInt32()).NPV
                        : TreeSwaptionEngine.Calculate(swaption, context, model, new TimeGrid(gridTimes, gridSteps)).NPV);
                continue;
            }

            check.Output(c, "hullWhiteSteps", c.Input("steps").EnumerateArray().Select(steps => (Func<object?>)(() =>
                TreeSwaptionEngine.Calculate(swaption, context, hullWhite, steps.GetInt32()).NPV)));
            check.Output(
                c,
                "hullWhiteGrid",
                () => TreeSwaptionEngine.Calculate(swaption, context, hullWhite, new TimeGrid(gridTimes, gridSteps)).NPV);

            // The lattice built once, priced on twice, as QuantLib's grid engine reuses its own.
            Lattice lattice = hullWhite.Tree(new TimeGrid(gridTimes, gridSteps));
            TreeSwaptionEngine.Calculate(swaption, context, hullWhite, lattice);
            check.Output(c, "hullWhiteLattice", () => TreeSwaptionEngine.Calculate(swaption, context, hullWhite, lattice).NPV);

            check.Output(c, "vasicekSteps", () => TreeSwaptionEngine.Calculate(swaption, context, vasicek, 50, discountCurve).NPV);
            check.Output(
                c,
                "vasicekGrid",
                () => TreeSwaptionEngine.Calculate(swaption, context, vasicek, new TimeGrid(gridTimes, gridSteps), discountCurve).NPV);
        }

        check.Complete();
    }
}
