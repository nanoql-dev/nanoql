// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Black formula vectors, <c>tests/vectors/pricingengines/blackformula.json</c>: the functions of
/// <see cref="BlackFormulaFunctions"/> the wheel wraps. Black's and Bachelier's formulas over option types, strikes,
/// forwards, standard deviations, displacements and discounts; Black's implied standard deviation (by the safe Newton
/// solver and by Li's iteration, with and without a guess) and Bachelier's implied volatility (Choi's and the exact
/// one) of the prices those formulas give; the in-the-money probabilities, on option types and on payoffs; and every
/// one of them on lists of arguments, their refusals and infinite inputs among them.
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
                case "implied":
                    CheckImplied(check, c);
                    break;
                case "itm":
                    CheckItm(check, c);
                    break;
                case "bachelierImplied":
                {
                    var type = (OptionType)c.Int("optionType");
                    double forward = c.Double("forward"), discount = c.Double("discount"), tte = c.Double("tte");
                    double[][] prices = Prices(c);
                    double[] strikes = c.Doubles("strikes");
                    check.Output(c, "impliedVolChoi", OnPrices(prices, strikes, (p, k) =>
                        BlackFormulaFunctions.BachelierBlackFormulaImpliedVolChoi(type, k, forward, tte, p, discount)));
                    check.Output(c, "impliedVol", OnPrices(prices, strikes, (p, k) =>
                        BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(type, k, forward, tte, p, discount)));
                    break;
                }

                case "bachelierItm":
                {
                    var type = (OptionType)c.Int("optionType");
                    double forward = c.Double("forward");
                    double[] stdDevs = c.Doubles("stdDevs"), strikes = c.Doubles("strikes");
                    check.Output(c, "assetItmProbability", VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
                        BlackFormulaFunctions.BachelierBlackFormulaAssetItmProbability(type, k, forward, sd)));
                    if (c.Expected.TryGetProperty("assetItmProbabilityPayoff", out _))
                    {
                        check.Output(c, "assetItmProbabilityPayoff", VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
                            BlackFormulaFunctions.BachelierBlackFormulaAssetItmProbability(new PlainVanillaPayoff(type, k), forward, sd)));
                    }

                    break;
                }

                case "blackFormulaImpliedStdDev":
                    check.Output(c, "blackFormulaImpliedStdDev", Each(c.Input("arguments"), a => a.Length == 6
                        ? BlackFormulaFunctions.BlackFormulaImpliedStdDev((OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5])
                        : BlackFormulaFunctions.BlackFormulaImpliedStdDev(
                            (OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], (int)a[8])));
                    break;
                case "blackFormulaImpliedStdDevLiRS":
                    check.Output(c, "blackFormulaImpliedStdDevLiRS", Each(c.Input("arguments"), a => a.Length == 6
                        ? BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS((OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5])
                        : BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
                            (OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], a[8], (int)a[9])));
                    break;
                case "blackFormulaCashItmProbability":
                    check.Output(c, "blackFormulaCashItmProbability", Each(c.Input("arguments"), a =>
                        BlackFormulaFunctions.BlackFormulaCashItmProbability((OptionType)(int)a[0], a[1], a[2], a[3], a[4])));
                    break;
                case "blackFormulaAssetItmProbability":
                    check.Output(c, "blackFormulaAssetItmProbability", Each(c.Input("arguments"), a =>
                        BlackFormulaFunctions.BlackFormulaAssetItmProbability((OptionType)(int)a[0], a[1], a[2], a[3], a[4])));
                    break;
                case "bachelierBlackFormulaImpliedVolChoi":
                    check.Output(c, "bachelierBlackFormulaImpliedVolChoi", Each(c.Input("arguments"), a =>
                        BlackFormulaFunctions.BachelierBlackFormulaImpliedVolChoi((OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5])));
                    break;
                case "bachelierBlackFormulaImpliedVol":
                    check.Output(c, "bachelierBlackFormulaImpliedVol", Each(c.Input("arguments"), a =>
                        BlackFormulaFunctions.BachelierBlackFormulaImpliedVol((OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5])));
                    break;
                case "bachelierBlackFormulaAssetItmProbability":
                    check.Output(c, "bachelierBlackFormulaAssetItmProbability", Each(c.Input("arguments"), a =>
                        BlackFormulaFunctions.BachelierBlackFormulaAssetItmProbability((OptionType)(int)a[0], a[1], a[2], a[3])));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    // Black's implied standard deviations of the case's prices, without a guess and, where the case gives one, with it.
    private static void CheckImplied(VectorAssert check, VectorCase c)
    {
        var type = (OptionType)c.Int("optionType");
        double forward = c.Double("forward"), displacement = c.Double("displacement"), discount = c.Double("discount");
        double[][] prices = Prices(c);
        double[] strikes = c.Doubles("strikes");
        check.Output(c, "impliedStdDev", OnPrices(prices, strikes, (p, k) =>
            BlackFormulaFunctions.BlackFormulaImpliedStdDev(type, k, forward, p, discount, displacement)));
        check.Output(c, "liRS", OnPrices(prices, strikes, (p, k) =>
            BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(type, k, forward, p, discount, displacement)));
        if (c.Inputs.TryGetProperty("guess", out _))
        {
            double guess = c.Double("guess"), accuracy = c.Double("accuracy"), omega = c.Double("omega");
            int maxIterations = c.Int("maxIterations");
            check.Output(c, "impliedStdDevGuessed", OnPrices(prices, strikes, (p, k) =>
                BlackFormulaFunctions.BlackFormulaImpliedStdDev(
                    type, k, forward, p, discount, displacement, guess, accuracy, maxIterations)));
            check.Output(c, "liRSPayoffGuessed", OnPrices(prices, strikes, (p, k) =>
                BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
                    new PlainVanillaPayoff(type, k), forward, p, discount, displacement, guess, omega, accuracy, maxIterations)));
        }
    }

    // Black's in-the-money probabilities, on the option type and, where the case has them, on payoffs.
    private static void CheckItm(VectorAssert check, VectorCase c)
    {
        var type = (OptionType)c.Int("optionType");
        double forward = c.Double("forward"), displacement = c.Double("displacement");
        double[] stdDevs = c.Doubles("stdDevs"), strikes = c.Doubles("strikes");
        check.Output(c, "cashItmProbability", VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
            BlackFormulaFunctions.BlackFormulaCashItmProbability(type, k, forward, sd, displacement)));
        check.Output(c, "assetItmProbability", VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
            BlackFormulaFunctions.BlackFormulaAssetItmProbability(type, k, forward, sd, displacement)));
        if (c.Expected.TryGetProperty("cashItmProbabilityPayoff", out _))
        {
            check.Output(c, "cashItmProbabilityPayoff", VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
                BlackFormulaFunctions.BlackFormulaCashItmProbability(new PlainVanillaPayoff(type, k), forward, sd, displacement)));
            check.Output(c, "assetItmProbabilityPayoff", VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
                BlackFormulaFunctions.BlackFormulaAssetItmProbability(new PlainVanillaPayoff(type, k), forward, sd, displacement)));
        }
    }

    // The case's prices, [stdDev][strike], NaN where the pricing formula refused.
    private static double[][] Prices(VectorCase c) =>
        c.Input("prices").EnumerateArray().Select(VolatilityInputs.Reals).ToArray();

    // One output per price, [stdDev][strike], each with its strike.
    private static Func<object?>[][] OnPrices(double[][] prices, double[] strikes, Func<double, double, double> f) =>
        prices.Select(row => row.Select((p, j) => (Func<object?>)(() => f(p, strikes[j]))).ToArray()).ToArray();

    // One output per argument list, each list's numbers read as reals ("NaN" and the infinities among them).
    private static Func<object?>[] Each(JsonElement arguments, Func<double[], double> f) =>
        arguments.EnumerateArray()
            .Select(a => VolatilityInputs.Reals(a))
            .Select(a => (Func<object?>)(() => f(a)))
            .ToArray();
}
