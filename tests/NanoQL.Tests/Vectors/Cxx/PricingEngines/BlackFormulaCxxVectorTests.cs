// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Black formula vectors, <c>tests/vectors/cxx/pricingengines/blackformula.json</c>: the overloads
/// of <see cref="BlackFormulaFunctions"/> the wheel doesn't wrap, over the wheel file's grid. On an option type, Black's
/// and Bachelier's forward derivatives and the three approximations of Black's implied standard deviation (of the
/// prices Black's formula gives); on a strike, the derivatives by the standard deviation and Black's vega; every
/// payoff overload the wheel doesn't wrap; and each of them on lists of arguments, their refusals and edge values among
/// them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackFormulaCxxVectorTests
{
    [Fact]
    public void BlackFormulaCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/blackformula");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "black":
                    CheckBlack(check, c);
                    break;
                case "strike":
                {
                    double forward = c.Double("forward"), displacement = c.Double("displacement"), discount = c.Double("discount");
                    double expiry = c.Double("expiry");
                    double[] stdDevs = c.Doubles("stdDevs"), strikes = c.Doubles("strikes");
                    check.Output(c, "stdDevDerivative", VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
                        BlackFormulaStdDevDerivative(k, forward, sd, discount, displacement)));
                    check.Output(c, "secondDerivative", VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
                        BlackFormulaStdDevSecondDerivative(k, forward, sd, discount, displacement)));
                    check.Output(c, "volDerivative", VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
                        BlackFormulaVolDerivative(k, forward, sd, expiry, discount, displacement)));
                    break;
                }

                case "bachelier":
                {
                    double forward = c.Double("forward"), discount = c.Double("discount");
                    double[] stdDevs = c.Doubles("stdDevs"), strikes = c.Doubles("strikes");
                    foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                    {
                        check.Output(c, "forwardDerivative" + type, VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
                            BachelierBlackFormulaForwardDerivative(type, k, forward, sd, discount)));
                    }

                    check.Output(c, "stdDevDerivative", VolatilityInputs.Grid(stdDevs, strikes, (sd, k) =>
                        BachelierBlackFormulaStdDevDerivative(k, forward, sd, discount)));
                    break;
                }

                case "payoff":
                    CheckPayoffs(check, c);
                    break;
                case "payoffArguments":
                    CheckPayoffArguments(check, c);
                    break;
                case "forwardDerivative":
                    check.Output(c, "forwardDerivative", Each(c, a => BlackFormulaForwardDerivative(
                        (OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5])));
                    break;
                case "approximation":
                    check.Output(c, "approximation", Each(c, a => BlackFormulaImpliedStdDevApproximation(
                        (OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5])));
                    break;
                case "chambers":
                    check.Output(c, "chambers", Each(c, a => BlackFormulaImpliedStdDevChambers(
                        (OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5], a[6])));
                    break;
                case "approximationRS":
                    check.Output(c, "approximationRS", Each(c, a => BlackFormulaImpliedStdDevApproximationRS(
                        (OptionType)(int)a[0], a[1], a[2], a[3], a[4], a[5])));
                    break;
                case "stdDevDerivative":
                    check.Output(c, "stdDevDerivative", Each(c, a => BlackFormulaStdDevDerivative(a[0], a[1], a[2], a[3], a[4])));
                    break;
                case "secondDerivative":
                    check.Output(c, "secondDerivative", Each(c, a => BlackFormulaStdDevSecondDerivative(a[0], a[1], a[2], a[3], a[4])));
                    break;
                case "volDerivative":
                    check.Output(c, "volDerivative", Each(c, a => BlackFormulaVolDerivative(a[0], a[1], a[2], a[3], a[4], a[5])));
                    break;
                case "bachelierForwardDerivative":
                    check.Output(c, "bachelierForwardDerivative", Each(c, a => BachelierBlackFormulaForwardDerivative(
                        (OptionType)(int)a[0], a[1], a[2], a[3], a[4])));
                    break;
                case "bachelierStdDevDerivative":
                    check.Output(c, "bachelierStdDevDerivative", Each(c, a => BachelierBlackFormulaStdDevDerivative(a[0], a[1], a[2], a[3])));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    // On an option type: the forward derivative over the grid, and the approximations of the implied standard deviation
    // of the case's prices.
    private static void CheckBlack(VectorAssert check, VectorCase c)
    {
        var type = (OptionType)c.Int("optionType");
        double forward = c.Double("forward"), displacement = c.Double("displacement"), discount = c.Double("discount");
        double[] strikes = c.Doubles("strikes");
        double[][] prices = c.Input("prices").EnumerateArray().Select(VolatilityInputs.Reals).ToArray();
        double[] atmPrices = VolatilityInputs.Reals(c.Input("atmPrices"));
        check.Output(c, "forwardDerivative", VolatilityInputs.Grid(c.Doubles("stdDevs"), strikes, (sd, k) =>
            BlackFormulaForwardDerivative(type, k, forward, sd, discount, displacement)));
        check.Output(c, "approximation", OnPrices(prices, strikes, (i, p, k) =>
            BlackFormulaImpliedStdDevApproximation(type, k, forward, p, discount, displacement)));
        check.Output(c, "chambers", OnPrices(prices, strikes, (i, p, k) =>
            BlackFormulaImpliedStdDevChambers(type, k, forward, p, atmPrices[i], discount, displacement)));
        check.Output(c, "approximationRS", OnPrices(prices, strikes, (i, p, k) =>
            BlackFormulaImpliedStdDevApproximationRS(type, k, forward, p, discount, displacement)));
    }

    // The payoff overloads, on a plain-vanilla payoff of each strike.
    private static void CheckPayoffs(VectorAssert check, VectorCase c)
    {
        var type = (OptionType)c.Int("optionType");
        double forward = c.Double("forward"), displacement = c.Double("displacement"), discount = c.Double("discount");
        double stdDev = c.Double("stdDev"), normalStdDev = c.Double("normalStdDev"), atmPrice = IndexInputs.Real(c.Input("atmPrice"));
        double guess = c.Double("guess"), accuracy = c.Double("accuracy");
        int maxIterations = c.Int("maxIterations");
        double[] strikes = c.Doubles("strikes");
        double[] prices = VolatilityInputs.Reals(c.Input("prices"));
        Func<object?>[] Each(Func<PlainVanillaPayoff, double, double> f) =>
            strikes.Select((k, j) => (Func<object?>)(() => f(new PlainVanillaPayoff(type, k), prices[j]))).ToArray();

        check.Output(c, "blackFormula", Each((p, _) => BlackFormula(p, forward, stdDev, discount, displacement)));
        check.Output(c, "forwardDerivative", Each((p, _) => BlackFormulaForwardDerivative(p, forward, stdDev, discount, displacement)));
        check.Output(c, "approximation", Each((p, price) => BlackFormulaImpliedStdDevApproximation(p, forward, price, discount, displacement)));
        check.Output(c, "chambers", Each((p, price) => BlackFormulaImpliedStdDevChambers(p, forward, price, atmPrice, discount, displacement)));
        check.Output(c, "approximationRS", Each((p, price) => BlackFormulaImpliedStdDevApproximationRS(p, forward, price, discount, displacement)));
        check.Output(c, "impliedStdDev", Each((p, price) => BlackFormulaImpliedStdDev(p, forward, price, discount, displacement)));
        check.Output(c, "impliedStdDevGuessed", Each((p, price) =>
            BlackFormulaImpliedStdDev(p, forward, price, discount, displacement, guess, accuracy, maxIterations)));
        check.Output(c, "stdDevDerivative", Each((p, _) => BlackFormulaStdDevDerivative(p, forward, stdDev, discount, displacement)));
        check.Output(c, "secondDerivative", Each((p, _) => BlackFormulaStdDevSecondDerivative(p, forward, stdDev, discount, displacement)));
        check.Output(c, "bachelierBlackFormula", Each((p, _) => BachelierBlackFormula(p, forward, normalStdDev, discount)));
        check.Output(c, "bachelierForwardDerivative", Each((p, _) => BachelierBlackFormulaForwardDerivative(p, forward, normalStdDev, discount)));
        check.Output(c, "bachelierStdDevDerivative", Each((p, _) => BachelierBlackFormulaStdDevDerivative(p, forward, normalStdDev, discount)));
    }

    // The payoff overloads on lists of [type, strike, forward, x, discount, displacement], x a standard deviation or a
    // price as each overload takes it.
    private static void CheckPayoffArguments(VectorAssert check, VectorCase c)
    {
        double atmPrice = c.Double("atmPrice");
        Func<object?>[] Each(Func<PlainVanillaPayoff, double[], double> f) => [.. c.Input("arguments").EnumerateArray()
            .Select(VolatilityInputs.Reals)
            .Select(a => (Func<object?>)(() => f(new PlainVanillaPayoff((OptionType)(int)a[0], a[1]), a)))];

        check.Output(c, "blackFormula", Each((p, a) => BlackFormula(p, a[2], a[3], a[4], a[5])));
        check.Output(c, "forwardDerivative", Each((p, a) => BlackFormulaForwardDerivative(p, a[2], a[3], a[4], a[5])));
        check.Output(c, "approximation", Each((p, a) => BlackFormulaImpliedStdDevApproximation(p, a[2], a[3], a[4], a[5])));
        check.Output(c, "approximationRS", Each((p, a) => BlackFormulaImpliedStdDevApproximationRS(p, a[2], a[3], a[4], a[5])));
        check.Output(c, "stdDevDerivative", Each((p, a) => BlackFormulaStdDevDerivative(p, a[2], a[3], a[4], a[5])));
        check.Output(c, "secondDerivative", Each((p, a) => BlackFormulaStdDevSecondDerivative(p, a[2], a[3], a[4], a[5])));
        check.Output(c, "chambers", Each((p, a) => BlackFormulaImpliedStdDevChambers(p, a[2], a[3], atmPrice, a[4], a[5])));
        check.Output(c, "impliedStdDev", Each((p, a) => BlackFormulaImpliedStdDev(p, a[2], a[3], a[4], a[5])));
        check.Output(c, "bachelierBlackFormula", Each((p, a) => BachelierBlackFormula(p, a[2], a[3], a[4])));
        check.Output(c, "bachelierForwardDerivative", Each((p, a) => BachelierBlackFormulaForwardDerivative(p, a[2], a[3], a[4])));
        check.Output(c, "bachelierStdDevDerivative", Each((p, a) => BachelierBlackFormulaStdDevDerivative(p, a[2], a[3], a[4])));
    }

    // One output per price, [row][strike], each with its row and strike.
    private static Func<object?>[][] OnPrices(double[][] prices, double[] strikes, Func<int, double, double, double> f) =>
        prices.Select((row, i) => row.Select((p, j) => (Func<object?>)(() => f(i, p, strikes[j]))).ToArray()).ToArray();

    // One output per argument list, each list's numbers read as reals ("NaN" and the infinities among them).
    private static Func<object?>[] Each(VectorCase c, Func<double[], double> f) =>
        c.Input("arguments").EnumerateArray()
            .Select(VolatilityInputs.Reals)
            .Select(a => (Func<object?>)(() => f(a)))
            .ToArray();
}
