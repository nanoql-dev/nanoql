// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the Black formulas' refusals, which the vectors record only as throws: QuantLib's
/// <c>QL_REQUIRE</c>s on the parameters (<c>checkParameters</c>, then the standard deviation and the discount;
/// <c>blackformula.cpp:40-54, 67-70, 719-722</c>) throw <see cref="ArgumentException"/> on the argument each names,
/// with QuantLib's texts in C#'s number formatting and in QuantLib's order; the <c>QL_ENSURE</c> on the result
/// throws <see cref="NanoQLException"/>; and an option type outside <see cref="OptionType"/>, whose sign QuantLib
/// would compute with, <see cref="ArgumentOutOfRangeException"/> with the text of QuantLib's stream operator. The
/// other formulas follow the same mapping; QuantLib's checks of values they compute (a normalized price, a time value,
/// the iterations' convergence) throw <see cref="NanoQLException"/>; a refusal inside a callee (the solver's, an
/// approximation's, the type overload a payoff overload calls) reaches the caller as the callee raises it; a negative
/// <c>maxIterations</c>, QuantLib's <c>Natural</c>, throws <see cref="ArgumentOutOfRangeException"/>; and a null payoff
/// <see cref="ArgumentNullException"/>.
/// </summary>
public class BlackFormulaErrorTests
{
    private const OptionType Call = OptionType.Call;

    [Fact]
    public void TheBlackFormulasRefusals()
    {
        AssertArgument("displacement", "displacement (-0.01) must be non-negative", () => BlackFormulaFunctions.BlackFormula(
            Call, 0.03, 0.05, 0.2, 0.9, -0.01));
        AssertArgument("displacement", "displacement (NaN) must be non-negative", () => BlackFormulaFunctions.BlackFormula(
            Call, 0.03, 0.05, 0.2, 0.9, double.NaN));
        AssertArgument("strike", "strike + displacement (-0.03 + 0.01) must be non-negative", () => BlackFormulaFunctions.BlackFormula(
            Call, -0.03, 0.05, 0.2, 0.9, 0.01));
        AssertArgument("forward", "forward + displacement (-0.05 + 0.01) must be positive", () => BlackFormulaFunctions.BlackFormula(
            Call, 0.03, -0.05, 0.2, 0.9, 0.01));
        AssertArgument("forward", "forward + displacement (0 + 0) must be positive", () => BlackFormulaFunctions.BlackFormula(
            Call, 0.03, 0.0, 0.2));
        AssertArgument("stdDev", "stdDev (-0.2) must be non-negative", () => BlackFormulaFunctions.BlackFormula(
            Call, 0.03, 0.05, -0.2, 0.9, 0.01));
        AssertArgument("discount", "discount (0) must be positive", () => BlackFormulaFunctions.BlackFormula(
            Call, 0.03, 0.05, 0.2, 0.0));

        // QuantLib's order: checkParameters' displacement, strike and forward, then the standard deviation and the
        // discount.
        AssertArgument("displacement", "displacement (-0.01) must be non-negative", () => BlackFormulaFunctions.BlackFormula(
            Call, -0.03, -0.05, -0.2, 0.0, -0.01));
        AssertArgument("strike", "strike + displacement (-0.03 + 0) must be non-negative", () => BlackFormulaFunctions.BlackFormula(
            Call, -0.03, -0.05, -0.2, 0.0));
        AssertArgument("forward", "forward + displacement (-0.05 + 0) must be positive", () => BlackFormulaFunctions.BlackFormula(
            Call, 0.03, -0.05, -0.2, 0.0));
        AssertArgument("stdDev", "stdDev (-0.2) must be non-negative", () => BlackFormulaFunctions.BlackFormula(
            Call, 0.03, 0.05, -0.2, 0.0));

        AssertArgument("stdDev", "stdDev (NaN) must be non-negative", () => BlackFormulaFunctions.BachelierBlackFormula(
            Call, 0.03, 0.05, double.NaN, 0.0));
        AssertArgument("discount", "discount (-1) must be positive", () => BlackFormulaFunctions.BachelierBlackFormula(
            Call, 0.03, 0.05, 0.01, -1.0));

        Assert.Equal(
            "negative value (NaN) for 0.2 stdDev, Call option, Infinity strike , 0.05 forward",
            Assert.Throws<NanoQLException>(() => BlackFormulaFunctions.BlackFormula(Call, double.PositiveInfinity, 0.05, 0.2)).Message);
        Assert.Equal(
            "negative value (NaN) for 0.2 stdDev, Put option, 0.03 strike , Infinity forward",
            Assert.Throws<NanoQLException>(() => BlackFormulaFunctions.BlackFormula(
                OptionType.Put, 0.03, double.PositiveInfinity, 0.2)).Message);
        Assert.Equal(
            "negative value (NaN) for 0.01 stdDev, Call option, Infinity strike , 0.05 forward",
            Assert.Throws<NanoQLException>(() => BlackFormulaFunctions.BachelierBlackFormula(
                Call, double.PositiveInfinity, 0.05, 0.01)).Message);

        foreach (OptionType unknown in new[] { (OptionType)0, (OptionType)2, (OptionType)(-2) })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
                () => BlackFormulaFunctions.BlackFormula(unknown, 0.03, 0.05, 0.2));
            Assert.Equal("optionType", e.ParamName);
            Assert.StartsWith("unknown option type", e.Message, StringComparison.Ordinal);
            e = Assert.Throws<ArgumentOutOfRangeException>(() => BlackFormulaFunctions.BachelierBlackFormula(unknown, 0.03, 0.05, 0.01));
            Assert.Equal("optionType", e.ParamName);
        }
    }

    [Fact]
    public void TheOtherFormulasRefusals()
    {
        // QuantLib's checks of the parameters: ArgumentException on the argument each names, with QuantLib's texts.
        AssertArgument("strike", "strike + displacement (-0.03 + 0) must be non-negative", () => BlackFormulaFunctions.BlackFormulaForwardDerivative(
            Call, -0.03, 0.05, 0.2));
        AssertArgument("stdDev", "stdDev (-0.2) must be non-negative", () => BlackFormulaFunctions.BlackFormulaStdDevDerivative(
            0.03, 0.05, -0.2));
        AssertArgument("discount", "discount (0) must be positive", () => BlackFormulaFunctions.BlackFormulaStdDevSecondDerivative(
            0.03, 0.05, 0.2, 0.0, 0.0));
        AssertArgument("blackPrice", "blackPrice (-0.01) must be non-negative", () => BlackFormulaFunctions.BlackFormulaImpliedStdDevApproximation(
            Call, 0.03, 0.05, -0.01));
        AssertArgument("blackAtmPrice", "blackAtmPrice (-0.004) must be non-negative", () => BlackFormulaFunctions.BlackFormulaImpliedStdDevChambers(
            Call, 0.03, 0.05, 0.02, -0.004));
        AssertArgument("blackPrice", "blackPrice (-0.01) must be non-negative", () => BlackFormulaFunctions.BlackFormulaImpliedStdDevApproximationRS(
            Call, 0.03, 0.05, -0.01));
        AssertArgument("blackPrice", "option price (-0.01) must be non-negative", () => BlackFormulaFunctions.BlackFormulaImpliedStdDev(
            Call, 0.03, 0.05, -0.01));
        AssertArgument("blackPrice", "option price (-0.01) must be non-negative", () => BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
            Call, 0.03, 0.05, -0.01));
        AssertArgument(
            "blackPrice",
            "negative Put price (-0.010000000000000004) implied by put-call parity. No solution exists for Call strike 0.03, forward "
            + "0.05, price 0.01, deflator 1",
            () => BlackFormulaFunctions.BlackFormulaImpliedStdDev(Call, 0.03, 0.05, 0.01));
        AssertArgument("guess", "stdDev guess (-0.1) must be non-negative", () => BlackFormulaFunctions.BlackFormulaImpliedStdDev(
            Call, 0.03, 0.05, 0.03, 1.0, 0.0, -0.1));
        AssertArgument("guess", "stdDev guess (-0.1) must be non-negative", () => BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
            Call, 0.03, 0.05, 0.03, 1.0, 0.0, -0.1));
        AssertArgument("tte", "tte (0) must be positive", () => BlackFormulaFunctions.BachelierBlackFormulaImpliedVolChoi(
            Call, 0.03, 0.05, 0.0, 0.021));
        AssertArgument("stdDev", "stdDev (-0.01) must be non-negative", () => BlackFormulaFunctions.BachelierBlackFormulaAssetItmProbability(
            Call, 0.03, 0.05, -0.01));
        AssertArgument("discount", "discount (0) must be positive", () => BlackFormulaFunctions.BachelierBlackFormulaStdDevDerivative(
            0.03, 0.05, 0.01, 0.0));

        // A refusal inside a callee reaches the caller as the callee raises it: the solver's, of the guess and the
        // accuracy; the guess's approximation's, of the strike and forward it was given, displaced; and on a payoff,
        // the type overload's, of the payoff's strike.
        AssertArgument("guess", "guess (30) > xMax_ (24)", () => BlackFormulaFunctions.BlackFormulaImpliedStdDev(
            Call, 0.03, 0.05, 0.03, 0.9, 0.0, 30.0));
        AssertArgument("accuracy", "accuracy (0) must be positive", () => BlackFormulaFunctions.BlackFormulaImpliedStdDev(
            Call, 0.03, 0.05, 0.03, 0.9, 0.0, 0.2, 0.0));
        AssertArgument("strike", "strike + displacement (-0.01 + 0) must be non-negative", () => BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
            Call, -0.01, 0.03, 0.04, 1.0));
        AssertArgument("strike", "strike + displacement (-0.03 + 0) must be non-negative", () => BlackFormulaFunctions.BlackFormula(
            new PlainVanillaPayoff(Call, -0.03), 0.05, 0.2));

        // QuantLib's other checks, on computed values, and its QL_ENSUREs: NanoQLException.
        Assert.Equal(
            "maximum number of function evaluations (0) exceeded",
            Assert.Throws<NanoQLException>(() => BlackFormulaFunctions.BlackFormulaImpliedStdDev(
                Call, 0.03, 0.05, 0.03, 0.9, 0.0, 0.2, 1e-6, 0)).Message);
        Assert.Equal(
            "max iterations exceeded",
            Assert.Throws<NanoQLException>(() => BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
                Call, 0.03, 0.05, 0.03, 0.9, 0.0, 0.2, 1.0, 1e-6, 1)).Message);
        Assert.Equal(
            "negative option price from in-out duality",
            Assert.Throws<NanoQLException>(() => BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
                Call, 0.0, 0.03, 0.04, 1.0)).Message);
        Assert.StartsWith(
            "nu (",
            Assert.Throws<NanoQLException>(() => BlackFormulaFunctions.BachelierBlackFormulaImpliedVolChoi(
                Call, 0.03, 0.05, 1.0, 0.01)).Message,
            StringComparison.Ordinal);
        Assert.Equal(
            "bachelierBlackFormulaImpliedVolExact(theta=1,strike=0.03,forward=0.05,tte=1,price=0.01): option price implies "
            + "negative time value (-0.010000000000000004)",
            Assert.Throws<NanoQLException>(() => BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(
                Call, 0.03, 0.05, 1.0, 0.01)).Message);
        Assert.Equal(
            "stdDev (NaN) must be non-negative",
            Assert.Throws<NanoQLException>(() => BlackFormulaFunctions.BlackFormulaImpliedStdDevApproximation(
                OptionType.Put, 0.03, double.PositiveInfinity, 0.03, 0.9)).Message);

        // Boost's domain error in Li's inverse normal, on a probability the iteration computed from a negative strike:
        // ArgumentOutOfRangeException, Boost's text.
        Assert.StartsWith(
            "Error in function boost::math::quantile(const normal_distribution<double>&, double): Probability argument is NaN",
            Assert.Throws<ArgumentOutOfRangeException>(() => BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
                Call, -0.01, 0.03, 0.04, 1.0, 0.0, 0.1)).Message,
            StringComparison.Ordinal);

        // nanoql's checks on entry: an option type the formula computes with, and a negative number of iterations.
        foreach (OptionType unknown in new[] { (OptionType)0, (OptionType)2 })
        {
            Func<object>[] refusing =
            [
                () => BlackFormulaFunctions.BlackFormulaForwardDerivative(unknown, 0.03, 0.05, 0.2),
                () => BlackFormulaFunctions.BlackFormulaImpliedStdDevApproximation(unknown, 0.03, 0.05, 0.01),
                () => BlackFormulaFunctions.BlackFormulaImpliedStdDev(unknown, 0.03, 0.05, 0.03),
                () => BlackFormulaFunctions.BlackFormulaCashItmProbability(unknown, 0.03, 0.05, 0.2),
                () => BlackFormulaFunctions.BlackFormulaAssetItmProbability(unknown, 0.03, 0.05, 0.2),
                () => BlackFormulaFunctions.BachelierBlackFormulaForwardDerivative(unknown, 0.03, 0.05, 0.01),
                () => BlackFormulaFunctions.BachelierBlackFormulaAssetItmProbability(unknown, 0.03, 0.05, 0.01),
                () => BlackFormulaFunctions.BlackFormulaImpliedStdDevChambers(unknown, 0.03, 0.05, 0.02, 0.004),
            ];
            foreach (Func<object> f in refusing)
            {
                ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(f);
                Assert.Equal("optionType", e.ParamName);
                Assert.StartsWith("unknown option type", e.Message, StringComparison.Ordinal);
            }
        }

        Assert.Equal("maxIterations", Assert.Throws<ArgumentOutOfRangeException>(() => BlackFormulaFunctions.BlackFormulaImpliedStdDev(
            Call, 0.03, 0.05, 0.03, 1.0, 0.0, null, 1e-6, -1)).ParamName);
        Assert.Equal("maxIterations", Assert.Throws<ArgumentOutOfRangeException>(() => BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
            Call, 0.03, 0.05, 0.03, 1.0, 0.0, null, 1.0, 1e-6, -1)).ParamName);

        // A null payoff: ArgumentNullException naming it, with .NET's text.
        ArgumentNullException n = Assert.Throws<ArgumentNullException>(() => BlackFormulaFunctions.BlackFormulaImpliedStdDev(
            (PlainVanillaPayoff)null!, 0.05, 0.03));
        Assert.Equal("payoff", n.ParamName);
        Assert.StartsWith("Value cannot be null.", n.Message, StringComparison.Ordinal);
    }

    private static void AssertArgument(string paramName, string message, Func<object> action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
