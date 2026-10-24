// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2022 Jonghee Lee
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mcamericanengine.hpp ql/pricingengines/vanilla/mcamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.PricingEngines;

/// <summary>
/// The American early-exercise path pricer: the payoff of the underlying at a time step, the state the underlying
/// scaled by the strike, and a polynomial basis of the state with the payoff as one more function.
/// </summary>
/// <remarks>
/// Immutable. The state is the underlying divided by the strike where the payoff is striked, for the regression's
/// numerical stability. The Legendre and first-kind Chebyshev bases are refused, as QuantLib refuses them; the
/// second-kind Chebyshev basis is NaN above 1, so a call's regression, on its in-the-money paths, is refused by the
/// singular value decomposition (the S-van plan's VR7).
/// </remarks>
public sealed class AmericanPathPricer : IEarlyExercisePathPricer<MonteCarloPath, double>
{
    private readonly double scalingValue = 1.0;
    private readonly Payoff payoff;
    private readonly ImmutableArray<Func<double, double>> v;

    /// <summary>Initializes a new instance of the <see cref="AmericanPathPricer"/> class.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="polynomialOrder">The basis' highest order.</param>
    /// <param name="polynomialType">The basis' family: monomial, Laguerre, Hermite, hyperbolic or second-kind Chebyshev.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="polynomialOrder"/> is negative.</exception>
    /// <exception cref="ArgumentException">The family is Legendre or first-kind Chebyshev ("insufficient polynomial type").</exception>
    /// <exception cref="NanoQLException">The family is unknown.</exception>
    public AmericanPathPricer(Payoff payoff, int polynomialOrder, LsmBasisSystemPolynomialType polynomialType)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        this.payoff = payoff;
        ImmutableArray<Func<double, double>> basis = LsmBasisSystem.PathBasisSystem(polynomialOrder, polynomialType);

        if (!(polynomialType == LsmBasisSystemPolynomialType.Monomial
            || polynomialType == LsmBasisSystemPolynomialType.Laguerre
            || polynomialType == LsmBasisSystemPolynomialType.Hermite
            || polynomialType == LsmBasisSystemPolynomialType.Hyperbolic
            || polynomialType == LsmBasisSystemPolynomialType.Chebyshev2nd))
        {
            throw new ArgumentException("insufficient polynomial type", nameof(polynomialType));
        }

        // the payoff gives an additional value
        v = basis.Add(Payoff);

        if (payoff is StrikedTypePayoff strikePayoff)
        {
            scalingValue /= strikePayoff.Strike;
        }
    }

    /// <summary>The scaled underlying at a time step.</summary>
    /// <param name="path">The path.</param>
    /// <param name="t">The time step's index.</param>
    /// <returns>The state.</returns>
    public double State(MonteCarloPath path, int t)
    {
        ArgumentNullException.ThrowIfNull(path);

        // scale values of the underlying
        // to increase numerical stability
        return path[t] * scalingValue;
    }

    /// <summary>The payoff of the underlying at a time step.</summary>
    /// <param name="path">The path.</param>
    /// <param name="t">The time step's index.</param>
    /// <returns>The exercise value.</returns>
    public double Evaluate(MonteCarloPath path, int t) => Payoff(State(path, t));

    /// <summary>The basis: the polynomials of the state, then the payoff of the state.</summary>
    /// <returns>The basis functions.</returns>
    public ImmutableArray<Func<double, double>> BasisSystem() => v;

    private double Payoff(double state) => payoff.Evaluate(state / scalingValue);
}
