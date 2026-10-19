// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodel.hpp ql/models/shortrate/onefactormodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The single-factor affine base class: a model whose discount bond is P(t, T, r) = A(t, T)·exp(−B(t, T)·r), for
/// derived classes implementing <see cref="A(double, double)"/> and <see cref="B(double, double)"/>.
/// </summary>
/// <remarks>An extension point, as in QuantLib: Vasicek derives from it, as later ports' Cox-Ingersoll-Ross will.</remarks>
public abstract class OneFactorAffineModel : OneFactorModel, IAffineModel
{
    /// <summary>Initializes a new instance of the <see cref="OneFactorAffineModel"/> class.</summary>
    /// <param name="nArguments">The number of arguments.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nArguments"/> is negative.</exception>
    protected OneFactorAffineModel(int nArguments)
        : base(nArguments)
    {
    }

    /// <summary>The price at a time of a unit discount bond maturing later, given the factor then.</summary>
    /// <param name="now">The time.</param>
    /// <param name="maturity">The bond's maturity.</param>
    /// <param name="factors">The factor's value at <paramref name="now"/>, the vector's first element.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factors"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="factors"/> is empty, where QuantLib reads past it.</exception>
    public virtual double DiscountBond(double now, double maturity, Vector factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        if (factors.Size == 0)
        {
            throw new ArgumentException("no factors given", nameof(factors));
        }

        return DiscountBond(now, maturity, factors[0]);
    }

    /// <summary>The price at a time of a unit discount bond maturing later, given the short rate then: A·exp(−B·r).</summary>
    /// <param name="now">The time.</param>
    /// <param name="maturity">The bond's maturity.</param>
    /// <param name="rate">The short rate at <paramref name="now"/>.</param>
    /// <returns>The price.</returns>
    public double DiscountBond(double now, double maturity, double rate) =>
        A(now, maturity) * Math.Exp(-B(now, maturity) * rate);

    /// <summary>The implied discount curve: the discount bond from 0 at the short rate of the state variable's start.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    public virtual double Discount(double t)
    {
        double x0 = Dynamics().Process.X0;
        double r0 = Dynamics().ShortRate(0.0, x0);
        return DiscountBond(0.0, t, r0);
    }

    /// <summary>The price of an option on a discount bond.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    public abstract double DiscountBondOption(OptionType type, double strike, double maturity, double bondMaturity);

    /// <summary>
    /// The price of an option on a discount bond that starts later than the option's maturity: by default, as QuantLib's,
    /// the option on the bond from the option's maturity, <paramref name="bondStart"/> ignored.
    /// </summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondStart">The bond's start.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    public virtual double DiscountBondOption(
        OptionType type, double strike, double maturity, double bondStart, double bondMaturity) =>
        DiscountBondOption(type, strike, maturity, bondMaturity);

    /// <summary>The affine discount bond's A(t, T).</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>A(t, T).</returns>
    protected abstract double A(double t, double T);

    /// <summary>The affine discount bond's B(t, T).</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The bond's maturity.</param>
    /// <returns>B(t, T).</returns>
    protected abstract double B(double t, double T);
}
