// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Hull-White vectors, <c>tests/vectors/cxx/models/shortrate/onefactormodels/hullwhite.json</c>:
/// the members of <see cref="HullWhite"/> the wheel doesn't bind (<see cref="ModelInputs.CheckModel"/>): Vasicek's
/// inspectors, <see cref="HullWhite.Dynamics"/> and its process, the five-argument
/// <see cref="HullWhite.DiscountBondOption(OptionType, double, double, double, double)"/>, the model's
/// <see cref="CalibratedModel.Constraint"/>, and the fitted <see cref="HullWhite.Tree"/>'s sizes, underlying values,
/// discounts and state prices (and, on the first model, its descendants and probabilities); then
/// <see cref="HullWhite.FixedReversion"/> and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HullWhiteCxxVectorTests
{
    [Fact]
    public void HullWhiteCxxVector()
    {
        var file = VectorFile.Load("cxx/models/shortrate/onefactormodels/hullwhite");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("fixedReversion", out _))
            {
                check.Output(c, "fixedReversion", HullWhite.FixedReversion());
            }
            else if (c.Name.StartsWith("refused: ", StringComparison.Ordinal))
            {
                ModelInputs.CheckRefusal(check, c);
            }
            else
            {
                Vasicek model = ModelInputs.ModelOf(c.Input("model"));
                Assert.IsType<HullWhite>(model);
                ModelInputs.CheckModel(check, c, model);
            }
        }

        check.Complete();
    }
}
