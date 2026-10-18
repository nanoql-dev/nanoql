// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Black formula vectors, <c>tests/vectors/pricingengines/blackformula.json</c>:
/// <see cref="BlackFormulaFunctions.BlackFormula"/> over option types, strikes, forwards, standard deviations,
/// displacements and discounts; <see cref="BlackFormulaFunctions.BachelierBlackFormula"/> over the same, without the
/// displacement; and both on lists of arguments, the refusals of their parameter checks and infinite inputs among them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackFormulaVectorTests
{
    [Fact]
    public void BlackFormulaVector()
    {
        var file = VectorFile.Load("pricingengines/blackformula");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "black":
                {
                    var type = (OptionType)c.Int("optionType");
                    double forward = c.Double("forward"), displacement = c.Double("displacement"), discount = c.Double("discount");
                    check.Output(c, "blackFormula", VolatilityInputs.Grid(
                        c.Doubles("stdDevs"),
                        c.Doubles("strikes"),
                        (sd, k) => BlackFormulaFunctions.BlackFormula(type, k, forward, sd, discount, displacement)));
                    break;
                }

                case "bachelier":
                {
                    var type = (OptionType)c.Int("optionType");
                    double forward = c.Double("forward"), discount = c.Double("discount");
                    check.Output(c, "bachelierBlackFormula", VolatilityInputs.Grid(
                        c.Doubles("stdDevs"),
                        c.Doubles("strikes"),
                        (sd, k) => BlackFormulaFunctions.BachelierBlackFormula(type, k, forward, sd, discount)));
                    break;
                }

                case "blackArguments":
                    check.Output(c, "blackFormula", Each(c.Input("arguments"), a => BlackFormulaFunctions.BlackFormula(
                        (OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5])));
                    break;
                case "bachelierArguments":
                    check.Output(c, "bachelierBlackFormula", Each(c.Input("arguments"), a => BlackFormulaFunctions.BachelierBlackFormula(
                        (OptionType)(int)a[0], a[1], a[2], a[3], a[4])));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    // One output per argument list, each list's numbers read as reals ("NaN" and the infinities among them).
    private static Func<object?>[] Each(JsonElement arguments, Func<double[], double> f) =>
        arguments.EnumerateArray()
            .Select(a => VolatilityInputs.Reals(a))
            .Select(a => (Func<object?>)(() => f(a)))
            .ToArray();
}
