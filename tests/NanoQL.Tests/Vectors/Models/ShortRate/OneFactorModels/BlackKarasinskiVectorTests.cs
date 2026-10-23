// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Black-Karasinski vectors, <c>tests/vectors/models/shortrate/onefactormodels/blackkarasinski.json</c>:
/// <see cref="BlackKarasinski"/>'s <see cref="CalibratedModel.Params"/> and <see cref="BlackKarasinski.TermStructure"/>'s
/// discounts; <see cref="CalibratedModelExtensions.SetParams"/> on a fresh model, its refusals compared alone; swaptions
/// on its fitted tree through <see cref="TreeSwaptionEngine"/>, each tree built for its calculation; and the
/// constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackKarasinskiVectorTests
{
    [Fact]
    public void BlackKarasinskiVector()
    {
        var file = VectorFile.Load("models/shortrate/onefactormodels/blackkarasinski");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            switch (c.String("kind"))
            {
                case "model":
                    {
                        BlackKarasinski model = ModelInputs.BlackKarasinskiOf(c.Input("model"), context);
                        check.Output(c, "params", ModelInputs.Elements(model.Params()));
                        check.Output(c, "termStructureDiscount", Numbers(c.Input("curveTimes"))
                            .Select(t => (object?)model.TermStructure.Discount(t))
                            .ToArray());
                        break;
                    }

                case "setParams":
                    {
                        BlackKarasinski model = ModelInputs.BlackKarasinskiOf(c.Input("model"), context);
                        check.Output(c, "paramsBefore", ModelInputs.Elements(model.Params()));
                        BlackKarasinski? changed = null;
                        check.Output(c, "setParams", () =>
                        {
                            changed = model.SetParams(ModelInputs.VectorOf(c.Input("params")));
                            return true;
                        });
                        if (changed is not null)
                        {
                            check.Output(c, "params", ModelInputs.Elements(changed.Params()));
                        }

                        break;
                    }

                case "tree":
                    {
                        Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
                        BlackKarasinski model = ModelInputs.BlackKarasinskiOf(c.Input("model"), context);
                        check.Output(c, "treeNPV", c.Input("steps").EnumerateArray().Select(steps => (Func<object?>)(() =>
                            TreeSwaptionEngine.Calculate(swaption, context, model, steps.GetInt32()).NPV)));
                        break;
                    }

                case "refused":
                    check.Output(c, "built", () => ModelInputs.BlackKarasinskiOf(c.Input("model"), context) is not null);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }
}
