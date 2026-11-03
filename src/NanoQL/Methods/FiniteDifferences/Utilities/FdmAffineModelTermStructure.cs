// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmaffinemodeltermstructure.hpp ql/methods/finitedifferences/utilities/fdmaffinemodeltermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>
/// The curve an affine model implies at a state of its factors: from its reference date, the model's discount bond
/// from that date's time on the model's clock, P(t, t + T) at the factors.
/// </summary>
/// <remarks>
/// Immutable: the factors are fixed at construction, where QuantLib's <c>setVariable</c> changes them and notifies; a
/// curve at another state is a new curve. The finite-difference inner value's own per-point curve is internal to it.
/// </remarks>
public sealed class FdmAffineModelTermStructure : YieldTermStructure
{
    private readonly Vector r;
    private readonly double t;
    private readonly IAffineModel model;

    /// <summary>Initializes a new instance of the <see cref="FdmAffineModelTermStructure"/> class.</summary>
    /// <param name="r">The model's factors.</param>
    /// <param name="cal">The calendar.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="referenceDate">The curve's reference date.</param>
    /// <param name="modelReferenceDate">The model's reference date, from which the curve's reference date's time is counted.</param>
    /// <param name="model">The affine model.</param>
    /// <exception cref="ArgumentNullException">The factors, the day counter or the model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    public FdmAffineModelTermStructure(
        Vector r, Calendar? cal, DayCounter dayCounter, Date referenceDate, Date modelReferenceDate, IAffineModel model)
        : base(referenceDate, cal, dayCounter)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(model);
        Date.ThrowIfDefault(modelReferenceDate, nameof(modelReferenceDate));
        this.r = r.Clone();
        t = dayCounter.YearFraction(modelReferenceDate, referenceDate);
        this.model = model;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <inheritdoc/>
    protected override double DiscountImpl(double t) => model.DiscountBond(this.t, t + this.t, r);
}
