// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Boost.Math's <c>sign</c>, which the forward derivatives take of the signed moneyness at a zero standard deviation
/// (<c>blackformula.cpp:125, 755</c>), is 0 for a zero and otherwise −1 or 1 by the value's sign bit
/// (<c>boost/math/special_functions/sign.hpp</c>), a NaN's included: it never throws, where <see cref="Math.Sign(double)"/>
/// throws <see cref="ArithmeticException"/> for a NaN. <see cref="double.NaN"/> has its sign bit set, as has the NaN
/// an x64 processor's invalid operation produces (∞ − ∞); the NaN a C++ program or Python spells out
/// (<c>std::numeric_limits&lt;double&gt;::quiet_NaN()</c>, <c>float("nan")</c>) has it clear. So a vector, whose
/// "NaN" carries no sign bit, can't give the derivative a NaN; this fact does.
/// </summary>
public class BoostSignTests
{
    [Fact]
    public void TheSignOfANaNIsItsSignBit()
    {
        double negativeNaN = double.NaN, positiveNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000000);
        Assert.True(double.IsNegative(negativeNaN));
        Assert.False(double.IsNegative(positiveNaN));

        // sign(NaN · 1) is −1 or 1: the call's derivative is max(sign, 0) · discount.
        Assert.Equal(0.0, BlackFormulaFunctions.BachelierBlackFormulaForwardDerivative(OptionType.Call, 0.03, negativeNaN, 0.0, 0.9));
        Assert.Equal(0.9, BlackFormulaFunctions.BachelierBlackFormulaForwardDerivative(OptionType.Call, 0.03, positiveNaN, 0.0, 0.9));

        // A NaN times −1 keeps its sign bit on x64: the put's derivative is −max(sign, 0) · discount.
        Assert.Equal(-0.9, BlackFormulaFunctions.BachelierBlackFormulaForwardDerivative(OptionType.Put, 0.03, positiveNaN, 0.0, 0.9));

        // ∞ − ∞: the processor's NaN, whose sign is −1, the harness's vectors' value.
        Assert.Equal(0.0, BlackFormulaFunctions.BlackFormulaForwardDerivative(
            OptionType.Call, double.PositiveInfinity, double.PositiveInfinity, 0.0, 0.9));
        Assert.Equal(0.0, BlackFormulaFunctions.BachelierBlackFormulaForwardDerivative(
            OptionType.Call, double.PositiveInfinity, double.PositiveInfinity, 0.0, 0.9));

        // A zero's sign is 0, of either sign.
        Assert.Equal(0.0, BlackFormulaFunctions.BachelierBlackFormulaForwardDerivative(OptionType.Call, 0.05, 0.05, 0.0, 0.9));
        Assert.Equal(0.0, BlackFormulaFunctions.BachelierBlackFormulaForwardDerivative(OptionType.Put, -0.0, 0.0, 0.0, 0.9));
    }
}
