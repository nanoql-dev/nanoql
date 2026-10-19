// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/swaptionvolcube.hpp ql/termstructures/volatility/swaption/swaptionvolcube.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// The base class of swaption volatility cubes: an at-the-money swaption volatility structure, and the smile around
/// it given by volatility spreads at strike spreads from the at-the-money strike, on a grid of option and swap
/// tenors.
/// </summary>
/// <remarks>
/// <para>
/// The volatility spreads are a row per option and swap tenor pair, the pair (j, k) of the j-th option tenor and the
/// k-th swap tenor in row j·(swap tenors) + k, with a column per strike spread. A cube's grid, reference date and
/// conventions are its at-the-money structure's: the constructor passes that structure's reference date, calendar,
/// business-day convention, day counter and settlement days to <see cref="SwaptionVolatilityDiscrete"/>, whose
/// constructor computes the grid from them. Its maximum date and time, maximum swap tenor, volatility type and shift
/// are the at-the-money structure's, read when asked; its strikes are unbounded.
/// </para>
/// <para>
/// The at-the-money strike of an option date and a swap tenor is the fixing of a swap index of that tenor, with the
/// conventions of the long swap index for a tenor past the short index's, else of the short one: it reads the
/// context's evaluation date, its fixings and its at-par switch. The at-the-money volatility is read from a copy of the
/// structure with extrapolation enabled, which the cube holds: QuantLib enables extrapolation on the caller's structure
/// itself, for every holder of it, where the caller's structure here is left unchanged.
/// </para>
/// <para>
/// An extension point, as in QuantLib: the interpolated cube and the XABR cubes derive from it. QuantLib's
/// <c>requiredNumberOfStrikes()</c> virtual, which its calculation reads, is a constructor argument here, checked when
/// the cube is built; QuantLib's unused <c>localStrikes_</c> and <c>localSmile_</c> aren't ported.
/// </para>
/// </remarks>
public abstract class SwaptionVolatilityCube : SwaptionVolatilityDiscrete
{
    private readonly SwaptionVolatilityStructure atmVol;
    private readonly ImmutableArray<double> strikeSpreads;
    private readonly ImmutableArray<ImmutableArray<double>> volSpreads;
    private readonly SwapIndex swapIndexBase;
    private readonly SwapIndex shortSwapIndexBase;

    /// <summary>Initializes a new instance of the <see cref="SwaptionVolatilityCube"/> class.</summary>
    /// <param name="atmVolStructure">The at-the-money volatility structure.</param>
    /// <param name="optionTenors">The option tenors, positive and increasing.</param>
    /// <param name="swapTenors">The swap tenors, positive, increasing, and in months or years.</param>
    /// <param name="strikeSpreads">The strike spreads from the at-the-money strike, increasing.</param>
    /// <param name="volSpreads">The volatility spreads: a row per option and swap tenor pair, with a spread per strike
    /// spread.</param>
    /// <param name="swapIndexBase">The swap index whose family and conventions give the at-the-money strikes of the swap
    /// tenors past the short index's.</param>
    /// <param name="shortSwapIndexBase">The swap index whose family and conventions give the at-the-money strikes of the
    /// other swap tenors.</param>
    /// <param name="vegaWeightedSmileFit">Whether a cube that fits its smiles weights them by vega.</param>
    /// <param name="requiredNumberOfStrikes">The fewest strike spreads the cube takes: QuantLib's
    /// <c>requiredNumberOfStrikes()</c>.</param>
    /// <exception cref="ArgumentNullException">The structure, a collection or an index is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The tenors are empty, not positive or not increasing, a swap tenor isn't in
    /// months or years, the strike spreads aren't increasing, the volatility spreads' rows aren't one per option and swap
    /// tenor pair or their columns one per strike spread, the short index's tenor is past the long index's, or there
    /// are fewer strike spreads than required.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="requiredNumberOfStrikes"/> is negative.</exception>
    /// <exception cref="NanoQLException">Two tenors can't be compared, or the structure has no calendar.</exception>
    protected SwaptionVolatilityCube(
        SwaptionVolatilityStructure atmVolStructure,
        IEnumerable<Period> optionTenors,
        IEnumerable<Period> swapTenors,
        IEnumerable<double> strikeSpreads,
        IEnumerable<IEnumerable<double>> volSpreads,
        SwapIndex swapIndexBase,
        SwapIndex shortSwapIndexBase,
        bool vegaWeightedSmileFit,
        int requiredNumberOfStrikes)
        : base(
            optionTenors,
            swapTenors,
            Checked(atmVolStructure).ReferenceDate,
            atmVolStructure.Calendar,
            atmVolStructure.BusinessDayConvention,
            atmVolStructure.DayCounter,
            atmVolStructure.SettlementDays)
    {
        ArgumentNullException.ThrowIfNull(strikeSpreads);
        ArgumentNullException.ThrowIfNull(volSpreads);
        ArgumentNullException.ThrowIfNull(swapIndexBase);
        ArgumentNullException.ThrowIfNull(shortSwapIndexBase);
        ArgumentOutOfRangeException.ThrowIfNegative(requiredNumberOfStrikes);

        this.strikeSpreads = [.. strikeSpreads];
        this.volSpreads = [.. volSpreads.Select(row => (row ?? throw new ArgumentNullException(nameof(volSpreads))).ToImmutableArray())];
        this.swapIndexBase = swapIndexBase;
        this.shortSwapIndexBase = shortSwapIndexBase;
        VegaWeightedSmileFit = vegaWeightedSmileFit;
        RequiredNumberOfStrikes = requiredNumberOfStrikes;
        NStrikes = this.strikeSpreads.Length;

        for (int i = 1; i < NStrikes; ++i)
        {
            if (!(this.strikeSpreads[i - 1] < this.strikeSpreads[i]))
            {
                throw new ArgumentException(
                    $"non increasing strike spreads: {DataFormattersFunctions.Ordinal(i)} is "
                    + $"{DataFormattersFunctions.General(this.strikeSpreads[i - 1])}, {DataFormattersFunctions.Ordinal(i + 1)} is "
                    + $"{DataFormattersFunctions.General(this.strikeSpreads[i])}",
                    nameof(strikeSpreads));
            }
        }

        if (this.volSpreads.IsEmpty)
        {
            throw new ArgumentException("empty vol spreads matrix", nameof(volSpreads));
        }

        if (!(NOptionTenors * NSwapTenors == this.volSpreads.Length))
        {
            throw new ArgumentException(
                $"mismatch between number of option tenors * swap tenors ({NOptionTenors * NSwapTenors}) and number of "
                + $"rows ({this.volSpreads.Length})",
                nameof(volSpreads));
        }

        for (int i = 0; i < this.volSpreads.Length; i++)
        {
            if (!(NStrikes == this.volSpreads[i].Length))
            {
                throw new ArgumentException(
                    $"mismatch between number of strikes ({NStrikes}) and number of columns ({this.volSpreads[i].Length}) "
                    + $"in the {DataFormattersFunctions.Ordinal(i + 1)} row",
                    nameof(volSpreads));
            }
        }

        // QuantLib enables extrapolation on the caller's structure; the cube holds a copy that extrapolates.
        atmVol = atmVolStructure.EnableExtrapolation();

        if (!(shortSwapIndexBase.Tenor <= swapIndexBase.Tenor))
        {
            throw new ArgumentException(
                $"short index tenor ({shortSwapIndexBase.Tenor}) is not less or equal than index tenor "
                + $"({swapIndexBase.Tenor})",
                nameof(shortSwapIndexBase));
        }

        // QuantLib's calculation checks the strikes' count first; the cube computes at construction.
        if (!(NStrikes >= RequiredNumberOfStrikes))
        {
            throw new ArgumentException(
                $"too few strikes ({NStrikes}) required are at least {RequiredNumberOfStrikes}", nameof(strikeSpreads));
        }
    }

    /// <summary>
    /// Gets the at-the-money volatility structure the cube reads: a copy of the one it was built on, with extrapolation
    /// enabled.
    /// </summary>
    public SwaptionVolatilityStructure AtmVol => atmVol;

    /// <summary>Gets the strike spreads from the at-the-money strike.</summary>
    public ImmutableArray<double> StrikeSpreads => strikeSpreads;

    /// <summary>Gets the swap index whose conventions give the at-the-money strikes past the short index's tenor.</summary>
    public SwapIndex SwapIndexBase => swapIndexBase;

    /// <summary>Gets the swap index whose conventions give the other at-the-money strikes.</summary>
    public SwapIndex ShortSwapIndexBase => shortSwapIndexBase;

    /// <summary>Gets a value indicating whether a cube that fits its smiles weights them by vega.</summary>
    public bool VegaWeightedSmileFit { get; }

    /// <summary>Gets the latest date for which the cube returns volatilities: the at-the-money structure's.</summary>
    public override Date MaxDate => atmVol.MaxDate;

    /// <summary>Gets the latest time for which the cube returns volatilities: the at-the-money structure's.</summary>
    public override double MaxTime => atmVol.MaxTime;

    /// <summary>Gets the largest swap tenor for which the cube returns volatilities: the at-the-money structure's.</summary>
    public override Period MaxSwapTenor => atmVol.MaxSwapTenor;

    /// <summary>Gets the type of the cube's volatilities: the at-the-money structure's.</summary>
    public override VolatilityType VolatilityType => atmVol.VolatilityType;

    /// <summary>Gets the number of strike spreads: QuantLib's <c>nStrikes_</c>.</summary>
    protected int NStrikes { get; }

    /// <summary>Gets the fewest strike spreads the cube takes: QuantLib's <c>requiredNumberOfStrikes()</c>.</summary>
    protected int RequiredNumberOfStrikes { get; }

    /// <summary>The volatility spreads, as given.</summary>
    /// <returns>A row per option and swap tenor pair, the pair (j, k) in row j·(swap tenors) + k, with a spread per
    /// strike spread.</returns>
    public ImmutableArray<ImmutableArray<double>> VolSpreads() => volSpreads;

    /// <summary>The at-the-money strike of an option date and a swap tenor: the forward swap rate.</summary>
    /// <param name="optionDate">The option date, the swap index's fixing date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="context">The evaluation context: the evaluation date, the fixings and the at-par switch the swap
    /// index's fixing reads.</param>
    /// <returns>The fixing, of the long index's family and conventions for a tenor past the short index's, else of the
    /// short one's, on the swap tenor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="optionDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The tenor can't be compared with the short index's, the date isn't a fixing
    /// date, a past fixing is missing, or the swap can't be built.</exception>
    public double AtmStrike(Date optionDate, Period swapTenor, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(optionDate, nameof(optionDate));

        // QuantLib builds a plain SwapIndex of the family on the tenor, as clone(tenor) does.
        SwapIndex index = swapTenor > shortSwapIndexBase.Tenor ? swapIndexBase : shortSwapIndexBase;
        return index.Clone(swapTenor).Fixing(optionDate, context);
    }

    /// <summary>The at-the-money strike of an option tenor and a swap tenor: the forward swap rate.</summary>
    /// <param name="optionTenor">The option tenor, from the reference date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="context">The evaluation context the swap index's fixing reads.</param>
    /// <returns>The fixing at the tenor's option date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">The tenor can't be compared with the short index's, the option date isn't a
    /// fixing date, a past fixing is missing, or the swap can't be built.</exception>
    public double AtmStrike(Period optionTenor, Period swapTenor, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date optionDate = OptionDateFromTenor(optionTenor);
        return AtmStrike(optionDate, swapTenor, context);
    }

    /// <summary>The minimum strike: the most negative real.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => -NumericLimits.QL_MAX_REAL;

    /// <summary>The maximum strike: the largest real.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <summary>The volatility at a strike: the smile section's for the option time and the swap length.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected override double VolatilityImpl(double optionTime, double swapLength, double strike) =>
        SmileSectionImpl(optionTime, swapLength).Volatility(strike);

    /// <summary>The volatility at a strike: the smile section's for the option date and the swap tenor.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected override double VolatilityImpl(Date optionDate, Period swapTenor, double strike) =>
        SmileSectionImpl(optionDate, swapTenor).Volatility(strike);

    /// <summary>The shift: the at-the-money structure's.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <returns>The shift.</returns>
    protected override double ShiftImpl(double optionTime, double swapLength) => atmVol.Shift(optionTime, swapLength);

    private static SwaptionVolatilityStructure Checked(SwaptionVolatilityStructure atmVolStructure) =>
        atmVolStructure ?? throw new ArgumentNullException(nameof(atmVolStructure));
}
