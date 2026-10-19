// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Hull-White vectors, <c>tests/vectors/models/shortrate/onefactormodels/hullwhite.json</c>:
/// <see cref="HullWhite"/> on flat and bootstrapped curves (<see cref="ModelInputs"/>, built in each case's context):
/// its <see cref="CalibratedModel.Params"/>, <see cref="HullWhite.TermStructure"/>'s discounts, and the affine members
/// (<see cref="ModelInputs.CheckAffine"/>); <see cref="CalibratedModelExtensions.SetParams"/> on a fresh model, the
/// parameters before it and, where it succeeds, the new model's outputs, its refusals compared alone, since QuantLib's
/// model has changed in part where nanoql's receiver hasn't; the static <see cref="HullWhite.ConvexityBias"/> with its
/// refusals; and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HullWhiteVectorTests
{
    [Fact]
    public void HullWhiteVector()
    {
        var file = VectorFile.Load("models/shortrate/onefactormodels/hullwhite");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            switch (c.String("kind"))
            {
                case "model":
                    {
                        var model = (HullWhite)ModelInputs.ModelOf(c.Input("model"), context);
                        check.Output(c, "termStructureDiscount", Numbers(c.Input("curveTimes"))
                            .Select(t => (object?)model.TermStructure.Discount(t))
                            .ToArray());
                        ModelInputs.CheckAffine(check, c, model);
                        break;
                    }

                case "setParams":
                    {
                        var model = (HullWhite)ModelInputs.ModelOf(c.Input("model"), context);
                        check.Output(c, "paramsBefore", ModelInputs.Elements(model.Params()));
                        HullWhite? changed = null;
                        check.Output(c, "setParams", () =>
                        {
                            changed = model.SetParams(ModelInputs.VectorOf(c.Input("params")));
                            return true;
                        });
                        if (changed is not null)
                        {
                            ModelInputs.CheckAffine(check, c, changed);
                        }

                        break;
                    }

                case "convexityBias":
                    check.Output(c, "convexityBias", ModelInputs.Points(c, "points")
                        .Select(p => (Func<object?>)(() => HullWhite.ConvexityBias(p[0], p[1], p[2], p[3], p[4])))
                        .ToArray());
                    break;
                case "refused":
                    check.Output(c, "built", () => ModelInputs.ModelOf(c.Input("model"), context) is HullWhite);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }
}
