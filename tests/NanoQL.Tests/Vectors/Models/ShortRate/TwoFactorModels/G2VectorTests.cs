// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The G2 vectors, <c>tests/vectors/models/shortrate/twofactormodels/g2.json</c>: <see cref="G2"/> on flat curves
/// (<see cref="ModelInputs"/>): its <see cref="CalibratedModel.Params"/>, <see cref="G2.TermStructure"/>'s discounts,
/// <see cref="G2.Discount"/>, <see cref="G2.DiscountBond(double, double, Vector)"/> and the four-argument
/// <see cref="G2.DiscountBondOption(OptionType, double, double, double)"/>; <see cref="CalibratedModelExtensions.SetParams"/>
/// on a fresh model, its refusals compared alone; swaptions on its tree (<see cref="TwoFactorModelShortRateTree"/> on
/// <see cref="NanoQL.Methods.TreeLattice2D"/>) through <see cref="TreeSwaptionEngine"/>; and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class G2VectorTests
{
    [Fact]
    public void G2Vector()
    {
        var file = VectorFile.Load("models/shortrate/twofactormodels/g2");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            switch (c.String("kind"))
            {
                case "model":
                    {
                        G2 model = ModelInputs.G2Of(c.Input("model"), context);
                        check.Output(c, "params", ModelInputs.Elements(model.Params()));
                        check.Output(c, "termStructureDiscount", Numbers(c.Input("curveTimes"))
                            .Select(t => (object?)model.TermStructure.Discount(t))
                            .ToArray());
                        CheckAffine(check, c, model);
                        break;
                    }

                case "setParams":
                    {
                        G2 model = ModelInputs.G2Of(c.Input("model"), context);
                        check.Output(c, "paramsBefore", ModelInputs.Elements(model.Params()));
                        G2? changed = null;
                        check.Output(c, "setParams", () =>
                        {
                            changed = model.SetParams(ModelInputs.VectorOf(c.Input("params")));
                            return true;
                        });
                        if (changed is not null)
                        {
                            check.Output(c, "params", ModelInputs.Elements(changed.Params()));
                            CheckAffine(check, c, changed);
                        }

                        break;
                    }

                case "tree":
                    {
                        Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
                        YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
                        var g2 = c.Input("g2");
                        var model = new G2(
                            curve,
                            g2.GetProperty("a").GetDouble(),
                            g2.GetProperty("sigma").GetDouble(),
                            g2.GetProperty("b").GetDouble(),
                            g2.GetProperty("eta").GetDouble(),
                            g2.GetProperty("rho").GetDouble());
                        check.Output(c, "treeNPV", c.Input("steps").EnumerateArray().Select(steps => (Func<object?>)(() =>
                            TreeSwaptionEngine.Calculate(swaption, context, model, steps.GetInt32()).NPV)));
                        break;
                    }

                case "refused":
                    check.Output(c, "built", () => ModelInputs.G2Of(c.Input("model"), context) is not null);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckAffine(VectorAssert check, VectorCase c, G2 model)
    {
        check.Output(c, "discount", Numbers(c.Input("times")).Select(t => (Func<object?>)(() => model.Discount(t))).ToArray());
        check.Output(c, "discountBond", ModelInputs.Points(c, "bondPoints")
            .Select(p => (Func<object?>)(() => model.DiscountBond(p[0], p[1], new Vector([p[2], p[3]]))))
            .ToArray());
        check.Output(c, "discountBondOption", ModelInputs.Points(c, "options")
            .Select(o => (Func<object?>)(() => model.DiscountBondOption((OptionType)(int)o[0], o[1], o[2], o[3])))
            .ToArray());
    }
}
