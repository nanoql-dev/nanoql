// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>
/// Hagan's G(R): the payment-date discount over the annuity as a function of the swap rate, under a model of the yield
/// curve, which the CMS replication integrates against; <see cref="GFunctionFactory"/> builds them.
/// </summary>
/// <remarks>
/// A g-function with shifts memoizes its last swap rate's shift solve, a function of the rate alone, so it computes the
/// same values whatever was asked before; the memo is replaced atomically, so one function serves any thread.
/// </remarks>
public abstract class GFunction
{
    private protected GFunction()
    {
    }

    /// <summary>G at a swap rate: QuantLib's <c>operator()</c>.</summary>
    /// <param name="x">The swap rate.</param>
    /// <returns>The value.</returns>
    public abstract double Evaluate(double x);

    /// <summary>G's first derivative at a swap rate.</summary>
    /// <param name="x">The swap rate.</param>
    /// <returns>The derivative.</returns>
    public abstract double FirstDerivative(double x);

    /// <summary>G's second derivative at a swap rate.</summary>
    /// <param name="x">The swap rate.</param>
    /// <returns>The derivative.</returns>
    public abstract double SecondDerivative(double x);
}
