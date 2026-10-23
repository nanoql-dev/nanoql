// SPDX-FileCopyrightText: Copyright (C) 2007 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2010 Alessandro Roveda
// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/nonlinearfittingmethods.hpp ql/termstructures/yield/nonlinearfittingmethods.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// The spread fitting method: a spread curve fitted on top of a discounting curve, by an underlying parametric method,
/// so that the fitted discount is the method's times the discounting curve's.
/// </summary>
/// <remarks>
/// <para>
/// The method takes the underlying method's constraint at zero, weights, optimization method and L2 penalties, and its
/// own cut-off times; the underlying method's own cut-offs apply to its part. When the discounting curve has another
/// reference date than the fitted curve, its discount is rebased to the fitted curve's reference date. A discounting
/// curve that changes is a new curve: QuantLib's method follows its handle.
/// </para>
/// <para>
/// QuantLib reads the discounting curve at the fitted curve's time (<c>nonlinearfittingmethods.cpp:414-416</c>), which
/// the discounting curve counts from its own reference date on its own day counter: dated 30 days earlier, the fitted
/// discount misses the rebased discounting curve by up to 3.1e-3, and by up to 1.05e-2 on Act/360. Here the
/// discounting curve is read at the instant the fitted curve's time denotes; where the two curves share their
/// reference date and day counter it is QuantLib's read and bits. A documented divergence.
/// </para>
/// </remarks>
public sealed class SpreadFittingMethod : FittedBondDiscountCurveFittingMethod
{
    // underlying parametric method
    private readonly FittedBondDiscountCurveFittingMethod method;

    // discount curve from on top of which the spread will be calculated
    private readonly YieldTermStructure discountingCurve;

    // adjustment in case underlying discount curve has different reference date, set by Init on the curve's copy
    private double rebase;

    /// <summary>Initializes a new instance of the <see cref="SpreadFittingMethod"/> class.</summary>
    /// <param name="method">The underlying parametric method.</param>
    /// <param name="discountCurve">The discounting curve the spread is fitted on top of.</param>
    /// <param name="minCutoffTime">The time before which the discount extrapolates with a flat forward rate.</param>
    /// <param name="maxCutoffTime">The time after which the discount extrapolates with a flat forward rate.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="method"/> or <paramref name="discountCurve"/> is <see langword="null"/>, QuantLib's "Fitting
    /// method is empty" and "Discounting curve cannot be empty".
    /// </exception>
    public SpreadFittingMethod(
        FittedBondDiscountCurveFittingMethod method,
        YieldTermStructure discountCurve,
        double minCutoffTime = 0.0,
        double maxCutoffTime = double.MaxValue)
        : base(
            method?.ConstrainAtZero ?? true,
            method?.Weights,
            method?.OptimizationMethod,
            method?.L2,
            minCutoffTime,
            maxCutoffTime)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(discountCurve);
        this.method = method;
        discountingCurve = discountCurve;
    }

    /// <inheritdoc/>
    /// <remarks>The underlying method's.</remarks>
    public override int Size => method.Size;

    /// <inheritdoc/>
    protected override double DiscountFunction(ReadOnlySpan<double> x, double t) =>
        method.Discount(x, t) * discountingCurve.DiscountAt(Curve!, t, true) / rebase;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The discounting curve can't give its discount at the fitted curve's reference date.</exception>
    protected override void Init()
    {
        // In case discount curve has a different reference date, discount to this curve's reference date
        FittedBondDiscountCurve curve = Curve!;
        if (curve.ReferenceDate != discountingCurve.ReferenceDate)
        {
            rebase = discountingCurve.Discount(curve.ReferenceDate);
        }
        else
        {
            rebase = 1.0;
        }

        // Call regular init
        base.Init();
    }
}
