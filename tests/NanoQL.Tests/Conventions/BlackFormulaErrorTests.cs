// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the Black formulas' refusals, which the vectors record only as throws: QuantLib's
/// <c>QL_REQUIRE</c>s on the parameters (<c>checkParameters</c>, then the standard deviation and the discount;
/// <c>blackformula.cpp:40-54, 67-70, 719-722</c>) throw <see cref="ArgumentException"/> on the argument each names,
/// with QuantLib's texts in C#'s number formatting and in QuantLib's order; the <c>QL_ENSURE</c> on the result
/// throws <see cref="NanoQLException"/>; and an option type outside <see cref="OptionType"/>, whose sign QuantLib
/// would compute with, <see cref="ArgumentOutOfRangeException"/> with the text of QuantLib's stream operator.
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

    private static void AssertArgument(string paramName, string message, Func<object> action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
