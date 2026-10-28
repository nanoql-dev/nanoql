// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/factorspreadedhazardratecurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A default-probability structure with a multiplicative spread on the hazard rates of an original curve,
/// whose reference date, calendar, day counter and range it takes.</summary>
/// <remarks>
/// Immutable: QuantLib's stays linked to its original and its spread quote, which change through it; here both are
/// values, a new curve for a new original or spread. The survival probability is the exact integral of the hazard rate
/// the curve reports, the original's times 1 + s: QuantLib's integrates it numerically with its base's 48-point
/// rule, which misses the exact survival by 5.4e-6 to 5.4e-5 relative on flat originals at 1 to 10 years and by
/// 1.05e-4 on a stepped one at four years, and which QuantLib warns derived classes to override (the S-xcr plan's XR3
/// (b), XR8 (xiv); <c>Divergences/SpreadedHazardRateCurveTests.cs</c>). The original's rate is its own kind's: on the
/// hazard kinds its survival's, jumps apart; on the survival and density kinds the density over the survival with its
/// jumps; on the affine structures the deterministic part alone, the model's left out, as QuantLib's integral leaves
/// it; so each kind integrates its rate in closed form (the S-xcr code review's C4 O1-2), and an original that gives
/// none, a user's, is integrated numerically, as QuantLib does.
/// </remarks>
/// <param name="originalCurve">The original curve.</param>
/// <param name="spread">The spread, the hazard rate multiplied by 1 plus it.</param>
[Experimental("NQL0001")]
public sealed class FactorSpreadedHazardRateCurve(DefaultProbabilityTermStructure originalCurve, double spread)
    : HazardRateStructure(
        (originalCurve ?? throw new ArgumentNullException(nameof(originalCurve))).ReferenceDate,
        originalCurve.Calendar,
        originalCurve.DayCounter)
{
    /// <inheritdoc/>
    public override Date MaxDate => originalCurve.MaxDate;

    /// <inheritdoc/>
    public override double MaxTime => originalCurve.MaxTime;

    /// <inheritdoc/>
    protected override double HazardRateImpl(double t) => originalCurve.HazardRate(t, true) * (1.0 + spread);

    /// <inheritdoc/>
    protected override double SurvivalProbabilityImpl(double t) =>
        originalCurve.HazardRateSurvival(t) is double s ? Math.Pow(s, 1.0 + spread) : base.SurvivalProbabilityImpl(t);

    /// <inheritdoc/>
    internal override double? HazardRateSurvival(double t) =>
        originalCurve.HazardRateSurvival(t) is double s ? Math.Pow(s, 1.0 + spread) : null;
}
