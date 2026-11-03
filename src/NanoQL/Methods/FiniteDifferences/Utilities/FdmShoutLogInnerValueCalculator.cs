// SPDX-FileCopyrightText: Copyright (C) 2021 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmshoutloginnervaluecalculator.hpp ql/methods/finitedifferences/utilities/fdmshoutloginnervaluecalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The exercise value of a shout option on a grid in the logarithm of the escrowed spot: the intrinsic value at the
/// spot locked in and paid at maturity, discounted, plus the residual option on the rest of the move, Black's value
/// struck at that spot, floored at 0.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. The curves and the volatility are read on the escrowed dividend adjustment's clock
/// (<see cref="FdmBlackScholesOp.Clock"/> in the shout engine), each on its own, QuantLib's reads where they share its
/// times.
/// </para>
/// <para>
/// The residual option is struck at the spot, the escrowed level s_t less the dividend adjustment, and its volatility
/// read there, where QuantLib strikes it and reads its volatility at s_t (<c>fdmshoutloginnervaluecalculator.cpp:47-73</c>):
/// a shout locks in S_τ − K and leaves (S_T − S_τ)⁺, so struck at the escrowed level QuantLib's residual hands the
/// dividends' present value back (24.90999 where the shout is worth 20.116 on QuantLib's
/// <c>testLargeDividendShoutNPV</c> option). Without dividends the two levels are one, and QuantLib's bits stand.
/// </para>
/// </remarks>
public sealed class FdmShoutLogInnerValueCalculator : FdmInnerValueCalculator
{
    private readonly BlackVolTermStructure blackVolatility;
    private readonly EscrowedDividendAdjustment escrowedDividendAdj;
    private readonly double maturity;
    private readonly PlainVanillaPayoff payoff;
    private readonly FdmMesher mesher;
    private readonly int direction;

    /// <summary>Initializes a new instance of the <see cref="FdmShoutLogInnerValueCalculator"/> class.</summary>
    /// <param name="blackVolatility">The Black volatility.</param>
    /// <param name="escrowedDividendAdj">The escrowed dividend adjustment, with the curves it reads.</param>
    /// <param name="maturity">The maturity, as a time on the adjustment's clock.</param>
    /// <param name="payoff">The payoff.</param>
    /// <param name="mesher">The mesher, in the logarithm of the escrowed spot.</param>
    /// <param name="direction">The direction of the underlying.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FdmShoutLogInnerValueCalculator(
        BlackVolTermStructure blackVolatility,
        EscrowedDividendAdjustment escrowedDividendAdj,
        double maturity,
        PlainVanillaPayoff payoff,
        FdmMesher mesher,
        int direction)
    {
        ArgumentNullException.ThrowIfNull(blackVolatility);
        ArgumentNullException.ThrowIfNull(escrowedDividendAdj);
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, mesher.Layout.Dim.Length);
        this.blackVolatility = blackVolatility;
        this.escrowedDividendAdj = escrowedDividendAdj;
        this.maturity = maturity;
        this.payoff = payoff;
        this.mesher = mesher;
        this.direction = direction;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">A curve or the volatility can't be read at the time.</exception>
    /// <exception cref="NanoQLException">The volatility can't be read, or Black's formula refuses its arguments.</exception>
    public override double InnerValue(FdmLinearOpIterator iter, double t)
    {
        ArgumentNullException.ThrowIfNull(iter);
        TermStructure clock = escrowedDividendAdj.Clock;
        double s_t = Math.Exp(mesher.Location(iter, direction));

        double qf = escrowedDividendAdj.DividendYield.DiscountAt(clock, maturity)
            / escrowedDividendAdj.DividendYield.DiscountAt(clock, t);

        double df = escrowedDividendAdj.RiskFreeRate.DiscountAt(clock, maturity)
            / escrowedDividendAdj.RiskFreeRate.DiscountAt(clock, t);

        double fwd = s_t * qf / df;

        // the residual struck at the spot, its volatility read there
        double spot = s_t - escrowedDividendAdj.DividendAdjustment(t);

        double stdDev = blackVolatility.SharesTimes(clock)
            ? blackVolatility.BlackForwardVol(t, maturity, spot) * Math.Sqrt(maturity - t)
            : Math.Sqrt(blackVolatility.BlackForwardVarianceAt(clock, t, maturity, spot));

        double npv = BlackFormulaFunctions.BlackFormula(payoff.OptionType, spot, fwd, stdDev, df);

        double intrinsic = (payoff.OptionType == OptionType.Call) ? spot - payoff.Strike : payoff.Strike - spot;

        // std::max(0.0, npv + intrinsic*df)
        double value = npv + (intrinsic * df);
        return 0.0 < value ? value : 0.0;
    }

    /// <inheritdoc/>
    public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
}
