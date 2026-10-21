// SPDX-FileCopyrightText: Copyright (C) 2021 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmescrowedloginnervaluecalculator.hpp ql/methods/finitedifferences/utilities/fdmescrowedloginnervaluecalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The inner value under the escrowed dividend model on a grid in the logarithm of the underlying: the payoff at the
/// grid's underlying less the escrowed dividends' adjustment at the time.
/// </summary>
/// <remarks>Its average over a cell is its value at the point, as QuantLib's is.</remarks>
public sealed class FdmEscrowedLogInnerValueCalculator : FdmInnerValueCalculator
{
    private readonly EscrowedDividendAdjustment escrowedDividendAdj;
    private readonly Payoff payoff;
    private readonly FdmMesher mesher;
    private readonly int direction;

    /// <summary>Initializes a new instance of the <see cref="FdmEscrowedLogInnerValueCalculator"/> class.</summary>
    /// <param name="escrowedDividendAdj">The escrowed dividends' adjustment.</param>
    /// <param name="payoff">The payoff.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="direction">The direction of the underlying's logarithm.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FdmEscrowedLogInnerValueCalculator(
        EscrowedDividendAdjustment escrowedDividendAdj, Payoff payoff, FdmMesher mesher, int direction)
    {
        ArgumentNullException.ThrowIfNull(escrowedDividendAdj);
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, mesher.Layout.Dim.Length);
        this.escrowedDividendAdj = escrowedDividendAdj;
        this.payoff = payoff;
        this.mesher = mesher;
        this.direction = direction;
    }

    /// <inheritdoc/>
    public override double InnerValue(FdmLinearOpIterator iter, double t)
    {
        ArgumentNullException.ThrowIfNull(iter);
        double s_t = Math.Exp(mesher.Location(iter, direction));
        double spot = s_t - escrowedDividendAdj.DividendAdjustment(t);

        return payoff.Evaluate(spot);
    }

    /// <inheritdoc/>
    public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
}
