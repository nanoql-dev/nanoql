// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Cox-Ingersoll-Ross vectors, <c>tests/vectors/models/shortrate/onefactormodels/coxingersollross.json</c>:
/// <see cref="CoxIngersollRoss"/>'s <see cref="CalibratedModel.Params"/>, the affine members and the four-argument
/// <see cref="CoxIngersollRoss.DiscountBondOption"/>; <see cref="CalibratedModelExtensions.SetParams"/> on a fresh
/// model, its refusals compared alone; calibrations with the level and speed fixed, through
/// <see cref="ModelVectorTests.CheckCalibration"/>; and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CoxIngersollRossVectorTests
{
    [Fact]
    public void CoxIngersollRossVector()
    {
        var file = VectorFile.Load("models/shortrate/onefactormodels/coxingersollross");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "model":
                    CheckAffine(check, c, CalibrationInputs.ModelOf(c.Input("model")));
                    break;
                case "setParams":
                    {
                        OneFactorAffineModel model = CalibrationInputs.ModelOf(c.Input("model"));
                        check.Output(c, "paramsBefore", ModelInputs.Elements(model.Params()));
                        OneFactorAffineModel? changed = null;
                        check.Output(c, "setParams", () =>
                        {
                            changed = model.SetParams(ModelInputs.VectorOf(c.Input("params")));
                            return true;
                        });
                        if (changed is not null)
                        {
                            CheckAffine(check, c, changed);
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

    /// <summary>Compares the parameters, the discounts, both discount-bond forms and the options.</summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="model">The model.</param>
    internal static void CheckAffine(VectorAssert check, VectorCase c, OneFactorAffineModel model)
    {
        check.Output(c, "params", ModelInputs.Elements(model.Params()));
        check.Output(c, "discount", Numbers(c.Input("times")).Select(t => (Func<object?>)(() => model.Discount(t))).ToArray());
        double[][] bonds = ModelInputs.Points(c, "bondPoints");
        check.Output(c, "discountBond", bonds.Select(b => (Func<object?>)(() => model.DiscountBond(b[0], b[1], b[2]))).ToArray());
        check.Output(
            c,
            "discountBondFactors",
            bonds.Select(b => (Func<object?>)(() => model.DiscountBond(b[0], b[1], new Vector([b[2]])))).ToArray());
        check.Output(c, "discountBondOption", ModelInputs.Points(c, "options")
            .Select(o => (Func<object?>)(() => model.DiscountBondOption((OptionType)(int)o[0], o[1], o[2], o[3])))
            .ToArray());
    }
}
