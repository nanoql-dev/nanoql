// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Vasicek vectors, <c>tests/vectors/models/shortrate/onefactormodels/vasicek.json</c>: <see cref="Vasicek"/>'s
/// <see cref="CalibratedModel.Params"/>, the affine <see cref="OneFactorAffineModel.Discount"/> and both
/// <see cref="OneFactorAffineModel.DiscountBond(double, double, double)"/> forms (the factors one rate or two), and
/// the four-argument <see cref="Vasicek.DiscountBondOption"/>, on twenty models either side of the small-reversion
/// threshold; then the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VasicekVectorTests
{
    [Fact]
    public void VasicekVector()
    {
        var file = VectorFile.Load("models/shortrate/onefactormodels/vasicek");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("built", out _))
            {
                check.Output(c, "built", () => ModelInputs.ModelOf(c.Input("model")) is not null);
                continue;
            }

            ModelInputs.CheckAffine(check, c, ModelInputs.ModelOf(c.Input("model")));
        }

        check.Complete();
    }
}
