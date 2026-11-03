// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/mcamericanbasketengine.hpp ql/pricingengines/basket/mcamericanbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The early-exercise path pricer of a basket option for the Longstaff-Schwartz engine: the assets' prices at a step,
/// scaled by the strike, as the regression's state, and its basis, the polynomials of several variables with the
/// payoff appended.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. The state is a new vector per call, the Longstaff-Schwartz pricer keeping each as one call's working
/// state.
/// </para>
/// <para>
/// The state is scaled by 1/|K|, and by 1 at a zero strike, where QuantLib scales by 1/K: at a zero strike (an exchange
/// option) its scale is infinite and the price NaN, and at a negative one the scale's sign swaps the minimum and the
/// maximum (<c>Divergences/AmericanBasketStrikeScalingTests.cs</c>); a positive scale leaves the regression's
/// polynomial span unchanged, so a positive strike keeps QuantLib's arithmetic.
/// </para>
/// </remarks>
public sealed class AmericanBasketPathPricer : IEarlyExercisePathPricer<MultiPath, Vector>
{
    private readonly int assetNumber;
    private readonly BasketPayoff payoff;
    private readonly double scalingValue = 1.0;
    private readonly ImmutableArray<Func<Vector, double>> v;

    /// <summary>Initializes a new instance of the <see cref="AmericanBasketPathPricer"/> class.</summary>
    /// <param name="assetNumber">The number of assets.</param>
    /// <param name="payoff">The basket payoff.</param>
    /// <param name="polynomialOrder">The basis' highest order.</param>
    /// <param name="polynomialType">
    /// The basis' family: monomial, Laguerre, Hermite, hyperbolic or second-kind Chebyshev.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The family is another ("insufficient polynomial type"), or the payoff isn't a basket payoff.
    /// </exception>
    public AmericanBasketPathPricer(
        int assetNumber,
        Payoff payoff,
        int polynomialOrder = 2,
        LsmBasisSystemPolynomialType polynomialType = LsmBasisSystemPolynomialType.Monomial)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        this.assetNumber = assetNumber;
        ImmutableArray<Func<Vector, double>> basis =
            LsmBasisSystem.MultiPathBasisSystem(assetNumber, polynomialOrder, polynomialType);
        if (!(polynomialType == LsmBasisSystemPolynomialType.Monomial
            || polynomialType == LsmBasisSystemPolynomialType.Laguerre
            || polynomialType == LsmBasisSystemPolynomialType.Hermite
            || polynomialType == LsmBasisSystemPolynomialType.Hyperbolic
            || polynomialType == LsmBasisSystemPolynomialType.Chebyshev2nd))
        {
            throw new ArgumentException("insufficient polynomial type", nameof(polynomialType));
        }

        if (payoff is not BasketPayoff basketPayoff)
        {
            throw new ArgumentException("payoff not a basket payoff", nameof(payoff));
        }

        this.payoff = basketPayoff;

        // the scale's magnitude alone, 1 at a zero strike
        if (basketPayoff.BasePayoff is StrikedTypePayoff strikePayoff && strikePayoff.Strike != 0.0)
        {
            scalingValue /= Math.Abs(strikePayoff.Strike);
        }

        v = basis.Add(Payoff);
    }

    /// <summary>The assets' scaled prices at a time step.</summary>
    /// <param name="path">The assets' paths.</param>
    /// <param name="t">The time step's index.</param>
    /// <returns>The state, a new vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The paths' number isn't the pricer's ("invalid multipath").</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="t"/> isn't a point of the path, where QuantLib reads past its end.
    /// </exception>
    public Vector State(MultiPath path, int t)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!(path.AssetNumber == assetNumber))
        {
            throw new NanoQLException("invalid multipath");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(t);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(t, path.PathSize());
        var tmp = new Vector(assetNumber);
        for (int i = 0; i < assetNumber; ++i)
        {
            tmp[i] = path[i][t] * scalingValue;
        }

        return tmp;
    }

    /// <summary>The payoff of the assets at a time step.</summary>
    /// <param name="path">The assets' paths.</param>
    /// <param name="t">The time step's index.</param>
    /// <returns>The exercise value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The paths' number isn't the pricer's.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="t"/> isn't a point of the path, where QuantLib reads past its end.
    /// </exception>
    public double Evaluate(MultiPath path, int t) => Payoff(State(path, t));

    /// <summary>The basis: the polynomials of the state, then the payoff of the state.</summary>
    /// <returns>The basis functions.</returns>
    public ImmutableArray<Func<Vector, double>> BasisSystem() => v;

    private double Payoff(Vector state)
    {
        double value = payoff.Accumulate(state.AsSpan());
        return payoff.Evaluate(value / scalingValue);
    }
}
