// SPDX-FileCopyrightText: Copyright (C) 2018 Sebastian Schlenkrich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/basismodels/tenoroptionletvts.hpp ql/experimental/basismodels/tenoroptionletvts.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A normal optionlet volatility on a longer target tenor transformed from one on a shorter base tenor: QuantLib's
/// <c>TenorOptionletVTS</c>.
/// </summary>
/// <remarks>
/// <para>
/// The target forward over the target tenor is a weighted sum of the base forwards over the base periods it covers; a
/// target optionlet's normal variance is the weights' quadratic form in the base volatilities, at the base strikes the
/// target strike maps to, under the correlation of the base forwards by their start times. The target tenor must be a
/// whole number of base tenors.
/// </para>
/// <para>
/// A smile section is built per query, as QuantLib's, from the indexes' fixings and forecasts at the option date: its
/// fixings are read in the context the structure was built with, which it keeps (S7R2's form, master Revision 43),
/// since the smile-section and volatility queries take no context. The structure takes the base structure's reference
/// date, calendar, convention and day counter.
/// </para>
/// <para>
/// A volatility is the smile section's at the strike, the range checked once, by the query with its own extrapolation
/// flag (the S-xrt run's R2-4; <c>Divergences/TenorOptionletExtrapolationTests.cs</c>). QuantLib's builds the section
/// through <c>smileSection(optionTime)</c>, which checks the range again without extrapolation, so a query allowed to
/// extrapolate past the base structure's last date refuses: QuantLib's own <c>testTenoroptionletvts</c> asks for its
/// 30-year caplets from the base structure's reference date, two business days after the evaluation date, and on QuantLib
/// 1.44 refuses ("time (30.0219) is past max curve time (30.0137)") on every evaluation date of 29 October to 11 November
/// 2026 but the two Wednesdays. On its test runner's date, 16 September 2015, a Wednesday, the case passes, so
/// QuantLib's own run doesn't refuse. QuantLib's swaption structure builds its section unchecked, as here; where
/// QuantLib's doesn't refuse, the value is its.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class TenorOptionletVTS : OptionletVolatilityStructure
{
    /// <summary>Initializes a new instance of the <see cref="TenorOptionletVTS"/> class.</summary>
    /// <param name="baseVTS">The base tenor's optionlet volatility.</param>
    /// <param name="baseIndex">The base tenor's index.</param>
    /// <param name="targIndex">The target tenor's index.</param>
    /// <param name="correlation">The base forwards' correlation by start time.</param>
    /// <param name="context">The evaluation context the smile sections read the indexes' fixings in.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">The base index's frequency isn't a multiple of the target's, with QuantLib's
    /// text.</exception>
    public TenorOptionletVTS(
        OptionletVolatilityStructure baseVTS,
        IborIndex baseIndex,
        IborIndex targIndex,
        TenorOptionletVTSCorrelationStructure correlation,
        EvaluationContext context)
        : base(
            (baseVTS ?? throw new ArgumentNullException(nameof(baseVTS))).ReferenceDate,
            baseVTS.Calendar,
            baseVTS.BusinessDayConvention,
            baseVTS.DayCounter)
    {
        BaseVTS = baseVTS;
        BaseIndex = baseIndex ?? throw new ArgumentNullException(nameof(baseIndex));
        TargIndex = targIndex ?? throw new ArgumentNullException(nameof(targIndex));
        Correlation = correlation ?? throw new ArgumentNullException(nameof(correlation));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        if ((int)BaseIndex.Tenor.Frequency() % (int)TargIndex.Tenor.Frequency() != 0)
        {
            throw new NanoQLException("Base index frequency must be a multiple of target tenor frequency");
        }
    }

    /// <inheritdoc/>
    /// <remarks>The base structure's.</remarks>
    public override Date MaxDate => BaseVTS.MaxDate;

    /// <inheritdoc/>
    /// <remarks>Normal.</remarks>
    public override VolatilityType VolatilityType => VolatilityType.Normal;

    /// <summary>Gets the base tenor's optionlet volatility.</summary>
    public OptionletVolatilityStructure BaseVTS { get; }

    /// <summary>Gets the base tenor's index.</summary>
    public IborIndex BaseIndex { get; }

    /// <summary>Gets the target tenor's index.</summary>
    public IborIndex TargIndex { get; }

    /// <summary>Gets the base forwards' correlation.</summary>
    public TenorOptionletVTSCorrelationStructure Correlation { get; }

    /// <summary>Gets the evaluation context the smile sections read the indexes' fixings in.</summary>
    public EvaluationContext Context { get; }

    /// <inheritdoc/>
    /// <remarks>The base structure's.</remarks>
    public override double MinStrike() => BaseVTS.MinStrike();

    /// <inheritdoc/>
    /// <remarks>The base structure's.</remarks>
    public override double MaxStrike() => BaseVTS.MaxStrike();

    /// <inheritdoc/>
    protected override SmileSection SmileSectionImpl(double optionTime) => new TenorOptionletSmileSection(this, optionTime);

    /// <inheritdoc/>
    /// <remarks>The smile section's volatility, the range checked once, by the caller's query (the S-xrt run's
    /// R2-4).</remarks>
    protected override double VolatilityImpl(double optionTime, double strike) =>
        SmileSectionImpl(optionTime).Volatility(strike);
}
