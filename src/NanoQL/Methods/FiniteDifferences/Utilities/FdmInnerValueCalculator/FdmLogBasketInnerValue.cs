// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp ql/methods/finitedifferences/utilities/fdminnervaluecalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;

namespace NanoQL.Methods;

/// <summary>
/// The inner value of a basket payoff on a mesher of the assets' log prices: the payoff at each point's prices.
/// </summary>
/// <remarks>
/// A working type: it holds the buffer of a point's prices, which QuantLib builds per call, so a call allocates
/// nothing.
/// </remarks>
public sealed class FdmLogBasketInnerValue : FdmInnerValueCalculator
{
    private readonly BasketPayoff payoff;
    private readonly FdmMesher mesher;
    private readonly double[] x;

    /// <summary>Initializes a new instance of the <see cref="FdmLogBasketInnerValue"/> class.</summary>
    /// <param name="payoff">The basket payoff.</param>
    /// <param name="mesher">The mesher, one direction per asset's log price.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="mesher"/> is
    /// <see langword="null"/>.</exception>
    public FdmLogBasketInnerValue(BasketPayoff payoff, FdmMesher mesher)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(mesher);
        this.payoff = payoff;
        this.mesher = mesher;
        x = new double[mesher.Layout.Dim.Length];
    }

    /// <summary>The payoff at the point's prices, the exponentials of its locations.</summary>
    /// <param name="iter">The point.</param>
    /// <param name="t">The time, unused.</param>
    /// <returns>The inner value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="iter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The payoff refuses the prices.</exception>
    public override double InnerValue(FdmLinearOpIterator iter, double t)
    {
        ArgumentNullException.ThrowIfNull(iter);
        for (int i = 0; i < x.Length; ++i)
        {
            x[i] = Math.Exp(mesher.Location(iter, i));
        }

        return payoff.Evaluate(x);
    }

    /// <summary>The inner value, the payoff at the point's prices: QuantLib takes no cell average here.</summary>
    /// <param name="iter">The point.</param>
    /// <param name="t">The time, unused.</param>
    /// <returns>The inner value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="iter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The payoff refuses the prices.</exception>
    public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
}
