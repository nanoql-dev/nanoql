// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>
/// A user's calibrated model that is a curve: with it bound beside nanoql, a calibration helper's trial model could go
/// into an engine's curve parameter by type, so the helpers place it by the roles table's names every time, and
/// nanoql's helper pairs stay as they are.
/// </summary>
public sealed class UserCalibratedCurve : YieldTermStructure, ICalibratedModel
{
    private readonly double rate;

    /// <summary>Initializes a new instance of the <see cref="UserCalibratedCurve"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="rate">The continuous rate.</param>
    public UserCalibratedCurve(Date referenceDate, double rate)
        : base(referenceDate, null, new Actual365Fixed())
    {
        this.rate = rate;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <inheritdoc/>
    public ImmutableArray<Parameter> Arguments => [new ConstantParameter(rate, new NoConstraint())];

    /// <inheritdoc/>
    public Constraint Constraint { get; } = new NoConstraint();

    /// <inheritdoc/>
    public Vector Params() => new([rate]);

    /// <inheritdoc/>
    public ICalibratedModel WithParams(Vector parameters) => new UserCalibratedCurve(ReferenceDate, parameters[0]);

    /// <inheritdoc/>
    protected override double DiscountImpl(double t) => Math.Exp(-rate * t);
}
