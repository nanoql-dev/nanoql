// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A null payoff in a Black formula's payoff overload. QuantLib's fifteen payoff overloads read the payoff's option
/// type or strike through the pointer before anything else (<c>blackformula.cpp:105, 144, 195, 257, 330, 456, 564,
/// 597, 628, 675, 709, 742, 767, 958, 981</c>), a null dereference, undefined under <c>std::shared_ptr</c> and a Boost
/// assertion by default. nanoql refuses it first, with <see cref="ArgumentNullException"/> naming <c>payoff</c>.
/// </summary>
public class BlackFormulaNullPayoffTests
{
    [Fact]
    public void ANullPayoffIsRefusedBeforeItIsRead()
    {
        PlainVanillaPayoff payoff = null!;
        Func<double>[] overloads =
        [
            () => BlackFormula(payoff, 0.05, 0.2),
            () => BlackFormulaForwardDerivative(payoff, 0.05, 0.2),
            () => BlackFormulaImpliedStdDevApproximation(payoff, 0.05, 0.02),
            () => BlackFormulaImpliedStdDevChambers(payoff, 0.05, 0.02, 0.004),
            () => BlackFormulaImpliedStdDevApproximationRS(payoff, 0.05, 0.02),
            () => BlackFormulaImpliedStdDev(payoff, 0.05, 0.02),
            () => BlackFormulaImpliedStdDevLiRS(payoff, 0.05, 0.02),
            () => BlackFormulaCashItmProbability(payoff, 0.05, 0.2),
            () => BlackFormulaAssetItmProbability(payoff, 0.05, 0.2),
            () => BlackFormulaStdDevDerivative(payoff, 0.05, 0.2),
            () => BlackFormulaStdDevSecondDerivative(payoff, 0.05, 0.2),
            () => BachelierBlackFormula(payoff, 0.05, 0.01),
            () => BachelierBlackFormulaForwardDerivative(payoff, 0.05, 0.01),
            () => BachelierBlackFormulaStdDevDerivative(payoff, 0.05, 0.01),
            () => BachelierBlackFormulaAssetItmProbability(payoff, 0.05, 0.01),
        ];

        foreach (Func<double> overload in overloads)
        {
            Assert.Equal("payoff", Assert.Throws<ArgumentNullException>(() => overload()).ParamName);
        }
    }
}
