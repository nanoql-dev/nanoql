// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Vasicek vectors, <c>tests/vectors/cxx/models/shortrate/onefactormodels/vasicek.json</c>: the
/// members of <see cref="Vasicek"/> the wheel doesn't bind (<see cref="ModelInputs.CheckModel"/>):
/// <see cref="Vasicek.A()"/>, <see cref="Vasicek.B()"/>, <see cref="Vasicek.Sigma"/>, <see cref="Vasicek.Lambda"/>,
/// <see cref="Vasicek.R0"/>, <see cref="Vasicek.Dynamics"/> and its process, the five-argument
/// <see cref="OneFactorAffineModel.DiscountBondOption(OptionType, double, double, double, double)"/>'s default, the
/// model's <see cref="CalibratedModel.Constraint"/>, and the unfitted <see cref="OneFactorModel.Tree"/>'s sizes,
/// discounts and state prices; then the refusal of a tree at a zero sigma.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VasicekCxxVectorTests
{
    [Fact]
    public void VasicekCxxVector()
    {
        var file = VectorFile.Load("cxx/models/shortrate/onefactormodels/vasicek");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name.StartsWith("refused: ", StringComparison.Ordinal))
            {
                ModelInputs.CheckRefusal(check, c);
                continue;
            }

            Vasicek model = ModelInputs.ModelOf(c.Input("model"));
            Assert.IsNotType<HullWhite>(model);
            ModelInputs.CheckModel(check, c, model);
        }

        check.Complete();
    }
}
