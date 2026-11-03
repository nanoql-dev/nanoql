// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/convexmonotoneinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// The convex-monotone interpolation of instantaneous forward rates from their section averages: QuantLib's
/// <c>ConvexMonotoneInterpolation</c>, Hagan and West's method ("Interpolation Methods for Curve Construction", Applied
/// Mathematical Finance 13(2), 2006) with QuantLib's enhancements.
/// </summary>
/// <remarks>
/// <para>
/// The y values are the sections' averages, the first ignored: the section from x<sub>i−1</sub> to x<sub>i</sub>
/// averages y<sub>i</sub>, and the primitive at x<sub>i</sub> is the sum of the sections' averages times their
/// lengths. A monotonicity of 1 and a quadraticity of 0 is Hagan and West's basic method; a monotonicity below 1 or a
/// quadraticity above 0 gives smoother curves. Forcing positivity keeps the forwards non-negative where the averages
/// are. Past the last node the interpolation is flat at its last value; with a flat final period, the last section is
/// flat at its average, as the local bootstrap builds the curve a section at a time.
/// </para>
/// <para>
/// Immutable: the section helpers are computed at construction. The derivatives aren't implemented, as in QuantLib.
/// </para>
/// <para>
/// Three fixes of QuantLib's sections (documented divergences in <c>Divergences/ConvexMonotoneTests.cs</c>): a
/// section's quadratic is split to stay non-negative only where it dips below 0 inside the section, and a discriminant
/// that rounds a hair below 0 is taken as 0, so every section keeps its average and the forwards their continuity; and
/// positivity isn't forced on a section whose average is negative, nor is an end forward beside one floored at 0, so a
/// negative-rate curve keeps its averages. Everywhere else QuantLib's values, bit for bit.
/// </para>
/// </remarks>
public sealed class ConvexMonotoneInterpolation : Interpolation
{
    // QuantLib's map from each section's right end x[i] to its helper, i from 1: the helper of the section ending at
    // x[i] at index i - 1.
    private readonly SectionHelper[] sectionHelpers;
    private readonly SectionHelper extrapolationHelper;
    private readonly bool constantLastPeriod;

    /// <summary>Initializes a new instance of the <see cref="ConvexMonotoneInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The sections' averages, one per x value, the first ignored.</param>
    /// <param name="quadraticity">The weight of the quadratic form, in [0, 1].</param>
    /// <param name="monotonicity">The monotonicity, in [0, 1].</param>
    /// <param name="forcePositive">Whether to keep the forwards non-negative.</param>
    /// <param name="flatFinalPeriod">Whether the last section is flat at its average.</param>
    /// <exception cref="ArgumentException">The sizes differ; there are fewer than two points; or the monotonicity or the
    /// quadraticity lies outside [0, 1].</exception>
    public ConvexMonotoneInterpolation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double quadraticity,
        double monotonicity,
        bool forcePositive,
        bool flatFinalPeriod = false)
        : this(x, y, quadraticity, monotonicity, forcePositive, flatFinalPeriod, [])
    {
    }

    // QuantLib's constructor with pre-existing helpers: those of a previous interpolation over the same first nodes,
    // for its first sections, as the local bootstrap extends the curve.
    internal ConvexMonotoneInterpolation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double quadraticity,
        double monotonicity,
        bool forcePositive,
        bool flatFinalPeriod,
        ImmutableArray<SectionHelper> preExistingHelpers)
        : base(x, y, ConvexMonotone.RequiredPoints)
    {
        CheckConfiguration(quadraticity, monotonicity);
        if (!(x.Length - preExistingHelpers.Length > 1))
        {
            throw new ArgumentException("Too many existing helpers have been supplied", nameof(preExistingHelpers));
        }

        constantLastPeriod = flatFinalPeriod;
        sectionHelpers = new SectionHelper[x.Length - 1];
        extrapolationHelper = Update(
            X, Y, quadraticity, monotonicity, forcePositive, flatFinalPeriod, preExistingHelpers, sectionHelpers);
    }

    // QuantLib's ConvexMonotoneImpl checks, which read the parameters only. Its check of at least two points is the
    // base's, which runs first.
    internal static void CheckConfiguration(double quadraticity, double monotonicity)
    {
        if (!(monotonicity >= 0 && monotonicity <= 1))
        {
            throw new ArgumentException("Monotonicity must lie between 0 and 1", nameof(monotonicity));
        }

        if (!(quadraticity >= 0 && quadraticity <= 1))
        {
            throw new ArgumentException("Quadraticity must lie between 0 and 1", nameof(quadraticity));
        }
    }

    // QuantLib's getExistingHelpers(): the helpers, less the flat final period's.
    internal ImmutableArray<SectionHelper> ExistingHelpers() =>
        constantLastPeriod ? [.. sectionHelpers.AsSpan(0, sectionHelpers.Length - 1)] : [.. sectionHelpers];

    /// <inheritdoc/>
    protected override double ValueCore(double x) =>
        x >= X[^1] ? extrapolationHelper.Value(x) : sectionHelpers[UpperBound(X[1..], x)].Value(x);

    /// <inheritdoc/>
    protected override double PrimitiveCore(double x) =>
        x >= X[^1] ? extrapolationHelper.Primitive(x) : sectionHelpers[UpperBound(X[1..], x)].Primitive(x);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: the derivative isn't implemented.</exception>
    protected override double DerivativeCore(double x) =>
        throw new NanoQLException("Convex-monotone spline derivative not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: the second derivative isn't implemented.</exception>
    protected override double SecondDerivativeCore(double x) =>
        throw new NanoQLException("Convex-monotone spline second derivative not implemented");

    // QuantLib's update(): fills the section helpers and returns the extrapolation helper.
    private static SectionHelper Update(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double quadraticity,
        double monotonicity,
        bool forcePositive,
        bool constantLastPeriod,
        ImmutableArray<SectionHelper> preSectionHelpers,
        SectionHelper[] sectionHelpers)
    {
        int length = x.Length;
        if (length == 2)
        {
            // single period
            var singleHelper = new EverywhereConstantHelper(y[1], 0.0, x[0]);
            sectionHelpers[0] = singleHelper;
            return singleHelper;
        }

        var f = new double[length];
        preSectionHelpers.CopyTo(sectionHelpers);
        int startPoint = preSectionHelpers.Length + 1;

        // first derive the boundary forwards.
        for (int i = startPoint; i < length - 1; ++i)
        {
            double dxPrev = x[i] - x[i - 1];
            double dx = x[i + 1] - x[i];
            f[i] = (dx / (dx + dxPrev) * y[i]) + (dxPrev / (dx + dxPrev) * y[i + 1]);
        }

        if (startPoint > 1)
        {
            f[startPoint - 1] = preSectionHelpers[^1].FNext();
        }

        if (startPoint == 1)
        {
            f[0] = (1.5 * y[1]) - (0.5 * f[1]);
        }

        f[length - 1] = (1.5 * y[length - 1]) - (0.5 * f[length - 2]);

        // positivity isn't forced on an end forward beside a section of negative average
        if (forcePositive)
        {
            if (f[0] < 0 && !(y[1] < 0.0))
            {
                f[0] = 0.0;
            }

            if (f[length - 1] < 0.0 && !(y[length - 1] < 0.0))
            {
                f[length - 1] = 0.0;
            }
        }

        double primitive = 0.0;
        for (int i = 0; i < startPoint - 1; ++i)
        {
            primitive += y[i + 1] * (x[i + 1] - x[i]);
        }

        int endPoint = length;
        if (constantLastPeriod)
        {
            endPoint = endPoint - 1;
        }

        for (int i = startPoint; i < endPoint; ++i)
        {
            double gPrev = f[i - 1] - y[i];
            double gNext = f[i] - y[i];

            // nor on a section of negative average
            bool sectionForcePositive = forcePositive && !(y[i] < 0.0);

            // first deal with the zero gradient case
            if (Math.Abs(gPrev) < 1.0E-14 && Math.Abs(gNext) < 1.0E-14)
            {
                sectionHelpers[i - 1] = new ConstantGradHelper(f[i - 1], primitive, x[i - 1], x[i], f[i]);
            }
            else
            {
                sectionHelpers[i - 1] = Section(
                    x[i - 1], x[i], f[i - 1], f[i], y[i], gPrev, gNext, quadraticity, monotonicity, sectionForcePositive,
                    primitive);
            }

            primitive += y[i] * (x[i] - x[i - 1]);
        }

        if (constantLastPeriod)
        {
            sectionHelpers[length - 2] = new EverywhereConstantHelper(y[length - 1], primitive, x[length - 2]);
            return sectionHelpers[length - 2];
        }
        else
        {
            return new EverywhereConstantHelper(sectionHelpers[^1].Value(x[length - 1]), primitive, x[length - 1]);
        }
    }

    // A section's helper away from the zero-gradient case: its quadratic form, its convex-monotone form by Hagan and
    // West's regions, or their combination by the quadraticity.
    private static SectionHelper Section(
        double xPrev,
        double xNext,
        double fPrev,
        double fNext,
        double fAverage,
        double gPrev,
        double gNext,
        double quadraticityParameter,
        double monotonicity,
        bool forcePositive,
        double primitive)
    {
        double quadraticity = quadraticityParameter;
        SectionHelper? quadraticHelper = null;
        SectionHelper? convMonotoneHelper = null;
        if (quadraticityParameter > 0.0)
        {
            if (gPrev >= -2.0 * gNext && gPrev > -0.5 * gNext && forcePositive)
            {
                quadraticHelper = new QuadraticMinHelper(xPrev, xNext, fPrev, fNext, fAverage, primitive);
            }
            else
            {
                quadraticHelper = new QuadraticHelper(xPrev, xNext, fPrev, fNext, fAverage, primitive);
            }
        }

        if (quadraticityParameter < 1.0)
        {
            if ((gPrev > 0.0 && -0.5 * gPrev >= gNext && gNext >= -2.0 * gPrev)
                || (gPrev < 0.0 && -0.5 * gPrev <= gNext && gNext <= -2.0 * gPrev))
            {
                quadraticity = 1.0;
                if (quadraticityParameter == 0)
                {
                    if (forcePositive)
                    {
                        quadraticHelper = new QuadraticMinHelper(xPrev, xNext, fPrev, fNext, fAverage, primitive);
                    }
                    else
                    {
                        quadraticHelper = new QuadraticHelper(xPrev, xNext, fPrev, fNext, fAverage, primitive);
                    }
                }
            }
            else if ((gPrev < 0.0 && gNext > -2.0 * gPrev) || (gPrev > 0.0 && gNext < -2.0 * gPrev))
            {
                double eta = (gNext + (2.0 * gPrev)) / (gNext - gPrev);
                double b2 = (1.0 + monotonicity) / 2.0;
                if (eta < b2)
                {
                    convMonotoneHelper = new ConvexMonotone2Helper(xPrev, xNext, gPrev, gNext, fAverage, eta, primitive);
                }
                else
                {
                    convMonotoneHelper = forcePositive
                        ? new ConvexMonotone4MinHelper(xPrev, xNext, gPrev, gNext, fAverage, b2, primitive)
                        : new ConvexMonotone4Helper(xPrev, xNext, gPrev, gNext, fAverage, b2, primitive);
                }
            }
            else if ((gPrev > 0.0 && gNext < 0.0 && gNext > -0.5 * gPrev)
                || (gPrev < 0.0 && gNext > 0.0 && gNext < -0.5 * gPrev))
            {
                double eta = gNext / (gNext - gPrev) * 3.0;
                double b3 = (1.0 - monotonicity) / 2.0;
                if (eta > b3)
                {
                    convMonotoneHelper = new ConvexMonotone3Helper(xPrev, xNext, gPrev, gNext, fAverage, eta, primitive);
                }
                else
                {
                    convMonotoneHelper = forcePositive
                        ? new ConvexMonotone4MinHelper(xPrev, xNext, gPrev, gNext, fAverage, b3, primitive)
                        : new ConvexMonotone4Helper(xPrev, xNext, gPrev, gNext, fAverage, b3, primitive);
                }
            }
            else
            {
                double eta = gNext / (gPrev + gNext);
                double b2 = (1.0 + monotonicity) / 2.0;
                double b3 = (1.0 - monotonicity) / 2.0;
                if (eta > b2)
                {
                    eta = b2;
                }

                if (eta < b3)
                {
                    eta = b3;
                }

                convMonotoneHelper = forcePositive
                    ? new ConvexMonotone4MinHelper(xPrev, xNext, gPrev, gNext, fAverage, eta, primitive)
                    : new ConvexMonotone4Helper(xPrev, xNext, gPrev, gNext, fAverage, eta, primitive);
            }
        }

        if (quadraticity == 1.0)
        {
            return quadraticHelper!;
        }
        else if (quadraticity == 0.0)
        {
            return convMonotoneHelper!;
        }
        else
        {
            return new ComboHelper(quadraticHelper!, convMonotoneHelper!, quadraticity);
        }
    }
}
