// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The extended Cox-Ingersoll-Ross vectors,
/// <c>tests/vectors/models/shortrate/onefactormodels/extendedcoxingersollross.json</c>:
/// <see cref="ExtendedCoxIngersollRoss"/>'s <see cref="CalibratedModel.Params"/>, its curve's discounts, the affine
/// members and the four-argument <see cref="ExtendedCoxIngersollRoss.DiscountBondOption"/>;
/// <see cref="CalibratedModelExtensions.SetParams"/> on a fresh model, its refusals compared alone; calibrations with
/// the level and speed fixed, through <see cref="ModelVectorTests.CheckCalibration"/>; and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExtendedCoxIngersollRossVectorTests
{
    [Fact]
    public void ExtendedCoxIngersollRossVector()
    {
        var file = VectorFile.Load("models/shortrate/onefactormodels/extendedcoxingersollross");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "model":
                    CheckModel(check, c, (ExtendedCoxIngersollRoss)CalibrationInputs.ModelOf(c.Input("model")));
                    break;
                case "setParams":
                    {
                        var model = (ExtendedCoxIngersollRoss)CalibrationInputs.ModelOf(c.Input("model"));
                        check.Output(c, "paramsBefore", ModelInputs.Elements(model.Params()));
                        ExtendedCoxIngersollRoss? changed = null;
                        check.Output(c, "setParams", () =>
                        {
                            changed = model.SetParams(ModelInputs.VectorOf(c.Input("params")));
                            return true;
                        });
                        if (changed is not null)
                        {
                            CheckModel(check, c, changed);
                        }

                        break;
                    }

                case "calibration":
                    ModelVectorTests.CheckCalibration(check, c);
                    break;
                case "refused":
                    check.Output(c, "built", () => CalibrationInputs.ModelOf(c.Input("model")) is not null);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckModel(VectorAssert check, VectorCase c, ExtendedCoxIngersollRoss model)
    {
        check.Output(c, "termStructureDiscount", Numbers(c.Input("curveTimes"))
            .Select(t => (object?)model.TermStructure.Discount(t))
            .ToArray());
        CoxIngersollRossVectorTests.CheckAffine(check, c, model);
    }
}
