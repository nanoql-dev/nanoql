// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2014, 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2023 Ignacio Anguita
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/sabrswaptionvolatilitycube.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// The swaption volatility cube that fits a smile model per option and swap tenor: the fit-early, interpolate-later
/// cube, generic over its model.
/// </summary>
/// <typeparam name="TModel">The smile model: its parameter count, its fit and its sections.</typeparam>
/// <remarks>
/// <para>
/// The cube fits the model's smile to the market volatilities, the at-the-money volatility plus each spread, at each
/// node of its grid (the sparse parameters). An at-the-money-calibrated cube then fills a dense grid, the union of its
/// grid and its at-the-money structure's: off its own nodes, a strike's volatility is the at-the-money volatility plus
/// the spread interpolated between the four surrounding nodes' fitted smiles at the strike of the same strike spread
/// (a normal cube) or moneyness (a lognormal one), and it fits the smile there again (the dense parameters). In a single
/// pass, the dense grid's spreads are the quoted ones interpolated the same way and the dense fit is the only one; with
/// at-the-money calibration each node's α is then solved so that the section returns the at-the-money volatility, and
/// the node's errors are recomputed at that α. Each fit starts from the parameter guesses interpolated at its node,
/// and restarts as its model's fit does. A node whose fit ends on its iteration limit, or whose error reaches the
/// tolerance, is refused with QuantLib's text.
/// </para>
/// <para>
/// A query's smile section is the model's section at the parameters and forward interpolated at its option time and
/// swap length, bilinearly between the nodes or, with backward-flat interpolation, backward flat in option time and
/// linear in swap length, both flat outside the grid. It reads only what the cube stores, not the context.
/// </para>
/// <para>
/// QuantLib's cube calibrates on its first query, and recalibrates when its quotes, its structures or the evaluation
/// date change; this one calibrates when it is built, under the context it is given, so a refusal of the calibration
/// comes at construction, and a new market is a new cube. The parameter guesses are read on the cube's grid, its
/// at-the-money structure's, where QuantLib reads them on the grid of its construction-time reference date, the
/// evaluation date: the two differ where the structure's reference date isn't that date. Immutable and thread-safe
/// once built.
/// </para>
/// <para>
/// QuantLib's <c>recalibration</c> members change a calibrated cube: they set a swap tenor's β guesses and refit that
/// tenor's sections, and <c>updateAfterRecalibration</c> then refits the dense grid. Here each
/// <see cref="Recalibration(IEnumerable{double}, Period, EvaluationContext)"/> overload returns a new cube, a copy of
/// this one's calibrated state with both steps applied, and leaves this cube unchanged. The β values are the fits'
/// guesses, so they are the sections' βs where the cube fixes β and the refit's starting point where it doesn't.
/// </para>
/// <para>
/// An extension point, as in QuantLib: a model of one's own is a <typeparamref name="TModel"/>, and QuantLib-Python's
/// named cubes derive from it, as <see cref="SabrSwaptionVolatilityCube"/> does.
/// </para>
/// </remarks>
public class XabrSwaptionVolatilityCube<TModel> : SwaptionVolatilityCube
    where TModel : struct, IXabrCubeModel
{
    // QuantLib's SWAPTIONVOLCUBE_VEGAWEIGHTED_TOL and SWAPTIONVOLCUBE_TOL.
    private const double VegaWeightedTolerance = 15.0e-4;
    private const double DefaultTolerance = 100.0e-4;

    private readonly ImmutableArray<bool> isParameterFixed;
    private readonly bool isAtmCalibrated;
    private readonly bool singlePassCalibration;
    private readonly EndCriteria? endCriteria;
    private readonly double maxErrorTolerance;
    private readonly OptimizationMethod? optMethod;
    private readonly double errorAccept;
    private readonly bool useMaxError;
    private readonly int maxGuesses;
    private readonly bool backwardFlat;
    private readonly double cutoffStrike;
    private readonly VolatilityType volatilityType;

    // The calibrated state: written by the constructor, and on a fresh copy by the recalibrations.
    private Cube parametersGuess;
    private readonly Cube marketVolCube;
    private Cube volCubeAtmCalibrated;
    private Cube sparseParameters;
    private Cube? denseParameters;
    private ImmutableArray<ImmutableArray<SmileSection>> sparseSmiles;

    /// <summary>
    /// Initializes a new instance of the <see cref="XabrSwaptionVolatilityCube{TModel}"/> class, and calibrates it.
    /// </summary>
    /// <param name="atmVolStructure">The at-the-money volatility structure; a <see cref="SwaptionVolatilityDiscrete"/>
    /// for an at-the-money-calibrated or single-pass cube.</param>
    /// <param name="optionTenors">The option tenors, positive and increasing; at least 2.</param>
    /// <param name="swapTenors">The swap tenors, positive, increasing, and in months or years; at least 2.</param>
    /// <param name="strikeSpreads">The strike spreads from the at-the-money strike, increasing; at least 1.</param>
    /// <param name="volSpreads">The volatility spreads: a row per option and swap tenor pair, the pair (j, k) in row
    /// j·(swap tenors) + k, with a spread per strike spread.</param>
    /// <param name="swapIndexBase">The swap index whose family and conventions give the at-the-money strikes of the swap
    /// tenors past the short index's.</param>
    /// <param name="shortSwapIndexBase">The swap index whose family and conventions give the at-the-money strikes of the
    /// other swap tenors.</param>
    /// <param name="vegaWeightedSmileFit">Whether the fits weight their errors by the strikes' vegas.</param>
    /// <param name="parametersGuess">The parameter guesses: a row per option and swap tenor pair, laid out as the
    /// volatility spreads, with a guess per model parameter; rows and guesses past those are ignored.</param>
    /// <param name="isParameterFixed">Whether each model parameter is fixed at its guess; flags past the model's
    /// parameters are ignored.</param>
    /// <param name="isAtmCalibrated">Whether the cube fills and fits its dense grid.</param>
    /// <param name="context">The evaluation context, which the at-the-money strikes read: the evaluation date, the
    /// fixings and the at-par switch.</param>
    /// <param name="endCriteria">The fits' end criteria, or <see langword="null"/> for the model fit's default.</param>
    /// <param name="maxErrorTolerance">The error a fit must stay below, or <see langword="null"/> for QuantLib's 0.01,
    /// 0.0015 with vega weights.</param>
    /// <param name="optMethod">The fits' optimizer, or <see langword="null"/> for the model fit's default.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted and its restarts stop, or
    /// <see langword="null"/> for a fifth of the tolerance.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error, not its root-mean-square error.</param>
    /// <param name="maxGuesses">The most minimizations of a fit.</param>
    /// <param name="backwardFlat">Whether the parameters are interpolated backward flat in option time.</param>
    /// <param name="cutoffStrike">The lowest strike plus shift a fit takes.</param>
    /// <param name="singlePassCalibration">Whether the cube fits its dense grid alone, from the quoted spreads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, the structure, a collection or an index is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The cube's base refuses its arguments; there are fewer than 2 option or swap
    /// tenors; the guesses' rows aren't one per option and swap tenor pair, or a row has fewer guesses than the model's
    /// parameters; there are fewer fixed flags than the model's parameters; the at-the-money structure isn't a
    /// <see cref="SwaptionVolatilityDiscrete"/> for a cube that fills its dense grid; or a fit refuses its
    /// points.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGuesses"/> is negative.</exception>
    /// <exception cref="NanoQLException">A fit fails or reaches the tolerance, an at-the-money strike can't be computed,
    /// or the dense grid reaches past the cube's.</exception>
    public XabrSwaptionVolatilityCube(
        SwaptionVolatilityStructure atmVolStructure,
        IEnumerable<Period> optionTenors,
        IEnumerable<Period> swapTenors,
        IEnumerable<double> strikeSpreads,
        IEnumerable<IEnumerable<double>> volSpreads,
        SwapIndex swapIndexBase,
        SwapIndex shortSwapIndexBase,
        bool vegaWeightedSmileFit,
        IEnumerable<IEnumerable<double>> parametersGuess,
        IEnumerable<bool> isParameterFixed,
        bool isAtmCalibrated,
        EvaluationContext context,
        EndCriteria? endCriteria = null,
        double? maxErrorTolerance = null,
        OptimizationMethod? optMethod = null,
        double? errorAccept = null,
        bool useMaxError = false,
        int maxGuesses = 50,
        bool backwardFlat = false,
        double cutoffStrike = 0.0001,
        bool singlePassCalibration = false)
        : base(
            EntryChecked(atmVolStructure, context),
            optionTenors,
            swapTenors,
            strikeSpreads,
            volSpreads,
            swapIndexBase,
            shortSwapIndexBase,
            vegaWeightedSmileFit,
            1)
    {
        ArgumentNullException.ThrowIfNull(parametersGuess);
        ArgumentNullException.ThrowIfNull(isParameterFixed);
        ArgumentOutOfRangeException.ThrowIfNegative(maxGuesses);
        ImmutableArray<ImmutableArray<double>> guesses =
            [.. parametersGuess.Select(row => (row ?? throw new ArgumentNullException(nameof(parametersGuess))).ToImmutableArray())];

        this.isParameterFixed = [.. isParameterFixed];
        this.isAtmCalibrated = isAtmCalibrated;
        this.singlePassCalibration = singlePassCalibration;
        this.endCriteria = endCriteria;
        this.optMethod = optMethod;
        this.useMaxError = useMaxError;
        this.maxGuesses = maxGuesses;
        this.backwardFlat = backwardFlat;
        this.cutoffStrike = cutoffStrike;
        volatilityType = atmVolStructure.VolatilityType;
        this.maxErrorTolerance = maxErrorTolerance ?? (VegaWeightedSmileFit ? VegaWeightedTolerance : DefaultTolerance);
        this.errorAccept = errorAccept ?? this.maxErrorTolerance / 5.0;

        CheckGuesses(guesses);
        this.parametersGuess = ParameterGuess(guesses);

        // QuantLib's performCalculations: the market volatilities, then the fits.
        marketVolCube = new Cube(OptionDates, SwapTenors, OptionTimes, SwapLengths, NStrikes);
        ImmutableArray<ImmutableArray<double>> spreads = VolSpreads();
        for (int j = 0; j < NOptionTenors; ++j)
        {
            for (int k = 0; k < NSwapTenors; ++k)
            {
                double atmForward = AtmStrike(OptionDates[j], SwapTenors[k], context);
                double atmVol = AtmVol.Volatility(OptionDates[j], SwapTenors[k], atmForward);
                for (int i = 0; i < NStrikes; ++i)
                {
                    double vol = atmVol + spreads[(j * NSwapTenors) + k][i];
                    marketVolCube.SetElement(i, j, k, vol);
                }
            }
        }

        marketVolCube.UpdateInterpolators();

        volCubeAtmCalibrated = new Cube(marketVolCube);

        if (singlePassCalibration)
        {
            sparseSmiles = FillVolatilityCube(true, null, context);
            denseParameters = SabrCalibration(volCubeAtmCalibrated, context);
            denseParameters.UpdateInterpolators();
            sparseParameters = new Cube(denseParameters);
        }
        else
        {
            sparseParameters = SabrCalibration(marketVolCube, context);
            sparseParameters.UpdateInterpolators();
            sparseSmiles = [];
            if (isAtmCalibrated)
            {
                sparseSmiles = FillVolatilityCube(false, sparseParameters, context);
                denseParameters = SabrCalibration(volCubeAtmCalibrated, context);
                denseParameters.UpdateInterpolators();
            }
        }
    }

    /// <summary>The sparse parameters: the fits at the cube's own nodes, or in a single pass the dense ones.</summary>
    /// <returns>
    /// A new matrix, a row per node, swap length by swap length and option time by option time within each: the swap
    /// length, the option time, the model's parameters, the forward, the root-mean-square error, the maximum error and
    /// the fit's end criteria, as their <see cref="EndCriteriaType"/> value.
    /// </returns>
    public Matrix SparseSabrParameters() => sparseParameters.Browse();

    /// <summary>The dense parameters: the fits at the dense grid's nodes.</summary>
    /// <returns>A new matrix, laid out as <see cref="SparseSabrParameters"/>'s.</returns>
    /// <exception cref="NanoQLException">The cube is neither at-the-money-calibrated nor calibrated in a single pass, so
    /// has no dense parameters.</exception>
    public Matrix DenseSabrParameters()
    {
        // QuantLib browses a default-constructed cube, whose layer count is uninitialized.
        if (denseParameters is null)
        {
            throw new NanoQLException(
                "no dense parameters: the cube is neither ATM-calibrated nor calibrated in a single pass");
        }

        return denseParameters.Browse();
    }

    /// <summary>The market volatilities at the cube's nodes: the at-the-money volatility plus each spread.</summary>
    /// <returns>A new matrix, a row per node laid out as <see cref="SparseSabrParameters"/>'s, with the swap length, the
    /// option time and a volatility per strike spread.</returns>
    public Matrix MarketVolCube() => marketVolCube.Browse();

    /// <summary>
    /// The market volatilities the fits took: the market volatilities at the cube's nodes and, for a cube that fills
    /// its dense grid, the filled volatilities at the dense nodes.
    /// </summary>
    /// <returns>A new matrix, laid out as <see cref="MarketVolCube()"/>'s.</returns>
    public Matrix VolCubeAtmCalibrated() => volCubeAtmCalibrated.Browse();

    // Whether the cube has dense parameters: at-the-money-calibrated or calibrated in a single pass.
    internal bool HasDenseParameters => denseParameters is not null;

    /// <summary>A strike spread's market volatilities at the cube's nodes.</summary>
    /// <param name="i">The strike spread's index.</param>
    /// <returns>A copy of the volatilities: a row per option tenor and a column per swap tenor.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative, or not less than the number of
    /// strike spreads.</exception>
    public Matrix MarketVolCube(int i)
    {
        // QuantLib reads past its matrices for an index past them.
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, NStrikes);
        return marketVolCube.Points[i].Clone();
    }

    /// <summary>
    /// A cube recalibrated at one β for a swap tenor: QuantLib's <c>recalibration(beta, swapTenor)</c>, each option
    /// tenor's β guess the one given, followed by <c>updateAfterRecalibration</c>.
    /// </summary>
    /// <param name="beta">The β guess for every option tenor.</param>
    /// <param name="swapTenor">The swap tenor, one of the cube's.</param>
    /// <param name="context">The evaluation context, which the at-the-money strikes read.</param>
    /// <returns>The new cube; this one is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The swap tenor isn't one of the cube's.</exception>
    /// <exception cref="NanoQLException">A fit fails or reaches the tolerance.</exception>
    public XabrSwaptionVolatilityCube<TModel> Recalibration(double beta, Period swapTenor, EvaluationContext context) =>
        Recalibration(Enumerable.Repeat(beta, NOptionTenors), swapTenor, context);

    /// <summary>
    /// A cube recalibrated at a β per option tenor for a swap tenor: QuantLib's <c>recalibration(beta, swapTenor)</c>,
    /// which sets the tenor's β guesses and refits its sections, followed by <c>updateAfterRecalibration</c>, which
    /// refits the dense grid.
    /// </summary>
    /// <param name="beta">The β guesses, one per option tenor.</param>
    /// <param name="swapTenor">The swap tenor, one of the cube's.</param>
    /// <param name="context">The evaluation context, which the at-the-money strikes read.</param>
    /// <returns>The new cube; this one is unchanged.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There isn't a β per option tenor, or the swap tenor isn't one of the
    /// cube's.</exception>
    /// <exception cref="NanoQLException">A fit fails or reaches the tolerance.</exception>
    public XabrSwaptionVolatilityCube<TModel> Recalibration(
        IEnumerable<double> beta, Period swapTenor, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(beta);
        ArgumentNullException.ThrowIfNull(context);
        return RecalibrationStep([.. beta], swapTenor, context).UpdatedAfterRecalibration(context);
    }

    /// <summary>
    /// A cube recalibrated on a β term structure for a swap tenor: QuantLib's
    /// <c>recalibration(swapLengths, beta, swapTenor)</c>, the βs given at the option dates of the periods given,
    /// interpolated linearly at the cube's option times and flat outside them, followed by
    /// <c>updateAfterRecalibration</c>.
    /// </summary>
    /// <param name="swapLengths">The periods whose option dates the βs are given at, increasing; at least 2.</param>
    /// <param name="beta">The βs, one per period.</param>
    /// <param name="swapTenor">The swap tenor, one of the cube's.</param>
    /// <param name="context">The evaluation context, which the at-the-money strikes read.</param>
    /// <returns>The new cube; this one is unchanged.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The βs aren't one per period, there are fewer than 2, or the swap tenor
    /// isn't one of the cube's.</exception>
    /// <exception cref="NanoQLException">A fit fails or reaches the tolerance.</exception>
    public XabrSwaptionVolatilityCube<TModel> Recalibration(
        IEnumerable<Period> swapLengths, IEnumerable<double> beta, Period swapTenor, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return RecalibrationStep(CubeBeta(swapLengths, beta), swapTenor, context).UpdatedAfterRecalibration(context);
    }

    /// <summary>
    /// QuantLib's <c>recalibration(beta, swapTenor)</c> alone, on a copy: the tenor's β guesses set and its sections
    /// refit, the dense grid refilled and its sections for the tenor refit; the step a market calibration's cost
    /// evaluation takes, before <see cref="UpdatedAfterRecalibration"/> at its end.
    /// </summary>
    /// <param name="beta">The β guesses, one per option tenor.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The copy.</returns>
    internal XabrSwaptionVolatilityCube<TModel> RecalibrationStep(
        IReadOnlyList<double> beta, Period swapTenor, EvaluationContext context)
    {
        if (!(beta.Count == NOptionTenors))
        {
            throw new ArgumentException(
                $"beta size ({beta.Count}) must be equal to number of option tenors ({NOptionTenors})", nameof(beta));
        }

        int k = IndexOf(marketVolCube.SwapTenors, swapTenor);
        if (!(k != marketVolCube.SwapTenors.Count))
        {
            throw new ArgumentException($"swap tenor ({swapTenor}) not found", nameof(swapTenor));
        }

        var copy = (XabrSwaptionVolatilityCube<TModel>)MemberwiseClone();
        copy.parametersGuess = new Cube(parametersGuess);
        for (int i = 0; i < NOptionTenors; ++i)
        {
            copy.parametersGuess.SetElement(1, i, k, beta[i]);
        }

        copy.parametersGuess.UpdateInterpolators();
        copy.volCubeAtmCalibrated = new Cube(marketVolCube);
        if (singlePassCalibration)
        {
            copy.sparseSmiles = copy.FillVolatilityCube(true, null, context);
            copy.denseParameters = new Cube(denseParameters!);
            copy.SabrCalibrationSection(copy.volCubeAtmCalibrated, copy.denseParameters, swapTenor, context);
            copy.sparseParameters = new Cube(copy.denseParameters);
        }
        else
        {
            copy.sparseParameters = new Cube(sparseParameters);
            copy.SabrCalibrationSection(marketVolCube, copy.sparseParameters, swapTenor, context);
        }

        if (isAtmCalibrated && !singlePassCalibration)
        {
            copy.sparseSmiles = copy.FillVolatilityCube(false, copy.sparseParameters, context);
            copy.denseParameters = new Cube(denseParameters!);
            copy.SabrCalibrationSection(copy.volCubeAtmCalibrated, copy.denseParameters, swapTenor, context);
        }

        return copy;
    }

    /// <summary>
    /// QuantLib's <c>updateAfterRecalibration</c>, on a copy: the dense grid refilled from the at-the-money
    /// volatilities and refit whole, for a cube that fills it.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The copy.</returns>
    internal XabrSwaptionVolatilityCube<TModel> UpdatedAfterRecalibration(EvaluationContext context)
    {
        var copy = (XabrSwaptionVolatilityCube<TModel>)MemberwiseClone();
        copy.volCubeAtmCalibrated = new Cube(marketVolCube);
        if (singlePassCalibration)
        {
            copy.sparseSmiles = copy.FillVolatilityCube(true, null, context);
            copy.denseParameters = copy.SabrCalibration(copy.volCubeAtmCalibrated, context);
            copy.denseParameters.UpdateInterpolators();
            copy.sparseParameters = new Cube(copy.denseParameters);
        }
        else if (isAtmCalibrated)
        {
            copy.sparseSmiles = copy.FillVolatilityCube(false, copy.sparseParameters, context);
            copy.denseParameters = copy.SabrCalibration(copy.volCubeAtmCalibrated, context);
            copy.denseParameters.UpdateInterpolators();
        }

        return copy;
    }

    /// <summary>
    /// The β per option tenor of QuantLib's term-structure recalibration: the βs given at the option dates of the
    /// periods, interpolated linearly at the cube's option times and flat outside them.
    /// </summary>
    /// <param name="swapLengths">The periods.</param>
    /// <param name="beta">The βs, one per period.</param>
    /// <returns>The βs, one per option tenor.</returns>
    internal double[] CubeBeta(IEnumerable<Period> swapLengths, IEnumerable<double> beta)
    {
        ArgumentNullException.ThrowIfNull(swapLengths);
        ArgumentNullException.ThrowIfNull(beta);
        Period[] lengths = [.. swapLengths];
        double[] betas = [.. beta];
        if (!(betas.Length == lengths.Length))
        {
            throw new ArgumentException(
                $"beta size ({betas.Length}) must be equal to number of swap lengths ({lengths.Length})", nameof(beta));
        }

        double[] betaTimes = [.. lengths.Select(l => TimeFromReference(OptionDateFromTenor(l)))];
        var betaInterpolation = new LinearInterpolation(betaTimes, betas);
        var cubeBeta = new double[OptionTimes.Length];
        for (int i = 0; i < OptionTimes.Length; i++)
        {
            double t = OptionTimes[i];

            // flat extrapolation ensures admissable values
            if (t < betaTimes[0])
            {
                t = betaTimes[0];
            }

            if (t > betaTimes[^1])
            {
                t = betaTimes[^1];
            }

            cubeBeta[i] = betaInterpolation.Evaluate(t);
        }

        return cubeBeta;
    }

    /// <summary>
    /// The smile section of an option time and a swap length: the model's section at the dense parameters, or the
    /// sparse ones for a cube that calibrates its own nodes only.
    /// </summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <returns>The section.</returns>
    protected override SmileSection SmileSectionImpl(double optionTime, double swapLength) =>
        isAtmCalibrated || singlePassCalibration
            ? SmileSection(optionTime, swapLength, denseParameters!)
            : SmileSection(optionTime, swapLength, sparseParameters);

    /// <summary>The α at which the model's section returns the at-the-money volatility at the forward.</summary>
    /// <param name="optionTime">The exercise time.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="atmVol">The at-the-money volatility.</param>
    /// <param name="parameters">The model's parameters, α the solve's guess.</param>
    /// <param name="shift">The shift.</param>
    /// <returns>The α, solved by <see cref="Brent"/> to 1e-12, at least machine epsilon.</returns>
    /// <exception cref="NanoQLException">The solve fails.</exception>
    protected double CalibratedAtmAlpha(
        double optionTime, double forward, double atmVol, ReadOnlySpan<double> parameters, double shift)
    {
        double[] p = [.. parameters];
        double AtmError(double alpha)
        {
            p[0] = alpha;
            return TModel.CreateSmileSection(optionTime, forward, p, shift, volatilityType).Volatility(forward) - atmVol;
        }

        Brent solver = new Brent().SetLowerBound(NumericLimits.QL_EPSILON);
        double guess = p[0];
        double scaled = 0.1 * guess;
        double step = scaled < 1.0e-6 ? 1.0e-6 : scaled;
        return solver.Solve(AtmError, 1.0e-12, guess, step);
    }

    /// <summary>The model's section's errors against volatilities at strikes, with QuantLib's fit's formulas.</summary>
    /// <param name="optionTime">The exercise time.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">The model's parameters.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="strikes">The strikes.</param>
    /// <param name="volatilities">The volatilities, one per strike.</param>
    /// <param name="weights">The weights, one per strike.</param>
    /// <returns>The weighted root-mean-square error and the largest absolute error.</returns>
    protected (double RmsError, double MaxError) SmileErrors(
        double optionTime,
        double forward,
        ReadOnlySpan<double> parameters,
        double shift,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> volatilities,
        ReadOnlySpan<double> weights)
    {
        SmileSection smile = TModel.CreateSmileSection(optionTime, forward, parameters, shift, volatilityType);
        double squaredError = 0.0;
        double maxError = NumericLimits.QL_MIN_REAL;
        for (int i = 0; i < strikes.Length; ++i)
        {
            double error = smile.Volatility(strikes[i]) - volatilities[i];
            squaredError += error * error * weights[i];
            double absolute = Math.Abs(error);
            maxError = maxError < absolute ? absolute : maxError;
        }

        int n = strikes.Length;
        return (Math.Sqrt(n * squaredError / (n == 1 ? 1 : n - 1)), maxError);
    }

    /// <summary>The model's section at the parameters and forward a parameter cube interpolates.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <param name="sabrParametersCube">The parameter cube.</param>
    /// <returns>The section.</returns>
    private protected SmileSection SmileSection(double optionTime, double swapLength, Cube sabrParametersCube)
    {
        double[] allParameters = sabrParametersCube.Evaluate(optionTime, swapLength);

        // the forward is stored after the model's parameters
        double forward = allParameters[TModel.NParams];
        double shiftTmp = AtmVol.Shift(optionTime, swapLength);
        return TModel.CreateSmileSection(
            optionTime, forward, allParameters.AsSpan(0, TModel.NParams), shiftTmp, volatilityType);
    }

    /// <summary>The fits at a market volatility cube's nodes: QuantLib's <c>sabrCalibration</c>.</summary>
    /// <param name="marketVolCube">The market volatilities.</param>
    /// <param name="context">The evaluation context the at-the-money strikes read.</param>
    /// <returns>The parameter cube: the model's parameters, the forward, the two errors and the end criteria.</returns>
    private protected Cube SabrCalibration(Cube marketVolCube, EvaluationContext context)
    {
        IReadOnlyList<double> optionTimes = marketVolCube.OptionTimes;
        IReadOnlyList<double> swapLengths = marketVolCube.SwapLengths;
        IReadOnlyList<Date> optionDates = marketVolCube.OptionDates;
        IReadOnlyList<Period> swapTenors = marketVolCube.SwapTenors;
        int nParams = TModel.NParams;
        var alphas = new Matrix(optionTimes.Count, swapLengths.Count, 0.0);
        Matrix betas = alphas.Clone();
        Matrix nus = alphas.Clone();
        Matrix rhos = alphas.Clone();

        // populated and used only for models of five or more parameters
        Matrix gammas = alphas.Clone();
        Matrix forwards = alphas.Clone();
        Matrix errors = alphas.Clone();
        Matrix maxErrors = alphas.Clone();
        Matrix endCriteriaLayer = alphas.Clone();

        IReadOnlyList<Matrix> tmpMarketVolCube = marketVolCube.Points;
        ImmutableArray<double> strikeSpreads = StrikeSpreads;
        var strikes = new List<double>(strikeSpreads.Length);
        var volatilities = new List<double>(strikeSpreads.Length);

        for (int j = 0; j < optionTimes.Count; j++)
        {
            for (int k = 0; k < swapLengths.Count; k++)
            {
                double atmForward = AtmStrike(optionDates[j], swapTenors[k], context);
                double shiftTmp = AtmVol.Shift(optionTimes[j], swapLengths[k]);
                strikes.Clear();
                volatilities.Clear();
                for (int i = 0; i < NStrikes; i++)
                {
                    double strike = atmForward + strikeSpreads[i];
                    if (strike + shiftTmp >= cutoffStrike)
                    {
                        strikes.Add(strike);
                        volatilities.Add(tmpMarketVolCube[i][j, k]);
                    }
                }

                double[] guess = parametersGuess.Evaluate(optionTimes[j], swapLengths[k]);

                IXabrInterpolation sabrInterpolation = TModel.CreateInterpolation(
                    strikes.ToArray(),
                    volatilities.ToArray(),
                    optionTimes[j],
                    atmForward,
                    guess,
                    isParameterFixed.AsSpan(),
                    VegaWeightedSmileFit,
                    endCriteria,
                    optMethod,
                    errorAccept,
                    useMaxError,
                    maxGuesses,
                    shiftTmp,
                    volatilityType);

                double rmsError = sabrInterpolation.RmsError;
                double maxError = sabrInterpolation.MaxError;
                double calibrationRmsError = rmsError;
                double calibrationMaxError = maxError;
                alphas[j, k] = sabrInterpolation.Alpha;
                betas[j, k] = sabrInterpolation.Beta;
                nus[j, k] = sabrInterpolation.Nu;
                rhos[j, k] = sabrInterpolation.Rho;
                if (nParams >= 5)
                {
                    gammas[j, k] = TModel.ExtractGamma(sabrInterpolation);
                }

                if (singlePassCalibration && isAtmCalibrated)
                {
                    double[] parameters = nParams >= 5
                        ? [alphas[j, k], betas[j, k], nus[j, k], rhos[j, k], gammas[j, k]]
                        : [alphas[j, k], betas[j, k], nus[j, k], rhos[j, k]];
                    double atmVol = AtmVol.Volatility(optionDates[j], swapTenors[k], atmForward, true);
                    alphas[j, k] = CalibratedAtmAlpha(optionTimes[j], atmForward, atmVol, parameters, shiftTmp);
                    parameters[0] = alphas[j, k];
                    (rmsError, maxError) = SmileErrors(
                        optionTimes[j],
                        atmForward,
                        parameters,
                        shiftTmp,
                        strikes.ToArray(),
                        volatilities.ToArray(),
                        sabrInterpolation.InterpolationWeights().AsSpan());
                }

                forwards[j, k] = atmForward;
                errors[j, k] = rmsError;
                maxErrors[j, k] = maxError;
                endCriteriaLayer[j, k] = (int)sabrInterpolation.EndCriteria;

                // the fifth parameter's text, for models that have one
                string gammaInfo = nParams >= 5
                    ? "\n   gamma = " + gammas[j, k].ToString("F6", CultureInfo.InvariantCulture)
                    : string.Empty;

                if (!(endCriteriaLayer[j, k] != (int)EndCriteriaType.MaxIterations))
                {
                    throw new NanoQLException(
                        "global swaptions calibration failed: MaxIterations reached: \n"
                        + $"option maturity = {optionDates[j]}, \n"
                        + $"swap tenor = {swapTenors[k]}, \n"
                        + $"rms error = {DataFormattersFunctions.Rate(calibrationRmsError)}, \n"
                        + $"max error = {DataFormattersFunctions.Rate(calibrationMaxError)}, \n"
                        + ParametersText(alphas[j, k], betas[j, k], nus[j, k], rhos[j, k], gammaInfo));
                }

                if (!((useMaxError ? calibrationMaxError : calibrationRmsError) < maxErrorTolerance))
                {
                    throw new NanoQLException(
                        "global swaptions calibration failed: error tolerance exceeded: \n"
                        + $"using {(useMaxError ? "maxError" : "rmsError")} tolerance "
                        + $"{Show(maxErrorTolerance)}, \n"
                        + $"option maturity = {optionDates[j]}, \n"
                        + $"swap tenor = {swapTenors[k]}, \n"
                        + $"rms error = {DataFormattersFunctions.Rate(calibrationRmsError)}, \n"
                        + $"max error = {DataFormattersFunctions.Rate(calibrationMaxError)}, \n"
                        + ParametersText(alphas[j, k], betas[j, k], nus[j, k], rhos[j, k], gammaInfo));
                }
            }
        }

        // the model's parameter layers, then the forwards, the errors, the maximum errors and the end criteria
        var sabrParametersCube = new Cube(optionDates, swapTenors, optionTimes, swapLengths, nParams + 4, backwardFlat);
        sabrParametersCube.SetLayer(0, alphas);
        sabrParametersCube.SetLayer(1, betas);
        sabrParametersCube.SetLayer(2, nus);
        sabrParametersCube.SetLayer(3, rhos);
        if (nParams >= 5)
        {
            sabrParametersCube.SetLayer(4, gammas);
        }

        sabrParametersCube.SetLayer(nParams, forwards);
        sabrParametersCube.SetLayer(nParams + 1, errors);
        sabrParametersCube.SetLayer(nParams + 2, maxErrors);
        sabrParametersCube.SetLayer(nParams + 3, endCriteriaLayer);

        return sabrParametersCube;
    }

    /// <summary>
    /// The fits of one swap tenor's nodes of a market volatility cube, into a parameter cube: QuantLib's
    /// <c>sabrCalibrationSection</c>.
    /// </summary>
    /// <param name="marketVolCube">The market volatilities.</param>
    /// <param name="parametersCube">The parameter cube, whose tenor's nodes are set.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <param name="context">The evaluation context the at-the-money strikes read.</param>
    private protected void SabrCalibrationSection(
        Cube marketVolCube, Cube parametersCube, Period swapTenor, EvaluationContext context)
    {
        IReadOnlyList<double> optionTimes = marketVolCube.OptionTimes;
        IReadOnlyList<double> swapLengths = marketVolCube.SwapLengths;
        IReadOnlyList<Date> optionDates = marketVolCube.OptionDates;
        IReadOnlyList<Period> swapTenors = marketVolCube.SwapTenors;

        int k = IndexOf(swapTenors, swapTenor);
        if (!(k != swapTenors.Count))
        {
            throw new NanoQLException("swap tenor not found");
        }

        int nParams = TModel.NParams;
        var calibrationResult = new double[nParams + 4];
        IReadOnlyList<Matrix> tmpMarketVolCube = marketVolCube.Points;
        ImmutableArray<double> strikeSpreads = StrikeSpreads;
        var strikes = new List<double>(strikeSpreads.Length);
        var volatilities = new List<double>(strikeSpreads.Length);

        for (int j = 0; j < optionTimes.Count; j++)
        {
            double atmForward = AtmStrike(optionDates[j], swapTenors[k], context);
            double shiftTmp = AtmVol.Shift(optionTimes[j], swapLengths[k]);
            strikes.Clear();
            volatilities.Clear();
            for (int i = 0; i < NStrikes; i++)
            {
                double strike = atmForward + strikeSpreads[i];
                if (strike + shiftTmp >= cutoffStrike)
                {
                    strikes.Add(strike);
                    volatilities.Add(tmpMarketVolCube[i][j, k]);
                }
            }

            double[] guess = parametersGuess.Evaluate(optionTimes[j], swapLengths[k]);

            IXabrInterpolation sabrInterpolation = TModel.CreateInterpolation(
                strikes.ToArray(),
                volatilities.ToArray(),
                optionTimes[j],
                atmForward,
                guess,
                isParameterFixed.AsSpan(),
                VegaWeightedSmileFit,
                endCriteria,
                optMethod,
                errorAccept,
                useMaxError,
                maxGuesses,
                shiftTmp,
                volatilityType);

            double interpolationError = sabrInterpolation.RmsError;
            double maxError = sabrInterpolation.MaxError;
            double calibrationError = interpolationError;
            double calibrationMaxError = maxError;
            calibrationResult[0] = sabrInterpolation.Alpha;
            calibrationResult[1] = sabrInterpolation.Beta;
            calibrationResult[2] = sabrInterpolation.Nu;
            calibrationResult[3] = sabrInterpolation.Rho;
            if (nParams >= 5)
            {
                calibrationResult[4] = TModel.ExtractGamma(sabrInterpolation);
            }

            if (singlePassCalibration && isAtmCalibrated)
            {
                double[] parameters = calibrationResult[..nParams];
                double atmVol = AtmVol.Volatility(optionDates[j], swapTenors[k], atmForward, true);
                calibrationResult[0] = CalibratedAtmAlpha(optionTimes[j], atmForward, atmVol, parameters, shiftTmp);
                parameters[0] = calibrationResult[0];
                (interpolationError, maxError) = SmileErrors(
                    optionTimes[j],
                    atmForward,
                    parameters,
                    shiftTmp,
                    strikes.ToArray(),
                    volatilities.ToArray(),
                    sabrInterpolation.InterpolationWeights().AsSpan());
            }

            // Metadata stored after model parameters
            calibrationResult[nParams] = atmForward;
            calibrationResult[nParams + 1] = interpolationError;
            calibrationResult[nParams + 2] = maxError;
            calibrationResult[nParams + 3] = (int)sabrInterpolation.EndCriteria;

            // the fifth parameter's text, for models that have one: QuantLib's std::to_string, six decimals
            string gammaInfo = nParams >= 5
                ? ", gamma " + calibrationResult[4].ToString("F6", CultureInfo.InvariantCulture)
                : string.Empty;
            string parametersText =
                $", alpha {DataFormattersFunctions.General(calibrationResult[0])}"
                + $", beta {DataFormattersFunctions.General(calibrationResult[1])}"
                + $", nu {DataFormattersFunctions.General(calibrationResult[2])}"
                + $", rho {DataFormattersFunctions.General(calibrationResult[3])}"
                + gammaInfo;

            if (!(calibrationResult[nParams + 3] != (int)EndCriteriaType.MaxIterations))
            {
                // QuantLib reads the cube's end criteria, null by default, where the fit runs on its own default's
                // 60000 iterations: the text gives the iterations the fit ran on.
                throw new NanoQLException(
                    $"section calibration failed: option tenor {optionDates[j]}, swap tenor {swapTenors[k]}: max iteration "
                    + $"({endCriteria?.MaxIterations ?? 60000}){parametersText}"
                    + $", max error {DataFormattersFunctions.General(calibrationMaxError)}"
                    + $", error {DataFormattersFunctions.General(calibrationError)}");
            }

            if (!((useMaxError ? calibrationMaxError : calibrationError) < maxErrorTolerance))
            {
                throw new NanoQLException(
                    $"section calibration failed: option tenor {optionDates[j]}, swap tenor {swapTenors[k]}"
                    + (useMaxError ? ": max error " : ": error ")
                    + DataFormattersFunctions.General(useMaxError ? calibrationMaxError : calibrationError)
                    + parametersText
                    + (useMaxError ? ", error " : ", max error ")
                    + DataFormattersFunctions.General(useMaxError ? calibrationError : calibrationMaxError));
            }

            parametersCube.SetPoint(optionDates[j], swapTenors[k], optionTimes[j], swapLengths[k], calibrationResult);
            parametersCube.UpdateInterpolators();
        }
    }

    // std::find over the swap tenors: the first equal one's index, or the count.
    private static int IndexOf(IReadOnlyList<Period> swapTenors, Period swapTenor)
    {
        for (int i = 0; i < swapTenors.Count; i++)
        {
            if (swapTenors[i] == swapTenor)
            {
                return i;
            }
        }

        return swapTenors.Count;
    }

    // The at-the-money structure checked first, as the context: before the base's.
    private static SwaptionVolatilityStructure EntryChecked(SwaptionVolatilityStructure atmVolStructure, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return atmVolStructure;
    }

    private static string ParametersText(double alpha, double beta, double nu, double rho, string gammaInfo) =>
        $"   alpha = {Show(alpha)}\n"
        + $"   beta = {Show(beta)}\n"
        + $"   nu = {Show(nu)}\n"
        + $"   rho = {Show(rho)}{gammaInfo}\n";

    // An insertion position, the first element not less than x: std::lower_bound.
    private static int LowerBound(IReadOnlyList<double> range, double x)
    {
        int first = 0, count = range.Count;
        while (count > 0)
        {
            int step = count / 2;
            int mid = first + step;
            if (range[mid] < x)
            {
                first = mid + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return first;
    }

    // std::binary_search: whether an element is equivalent to x.
    private static bool BinarySearch(IReadOnlyList<double> range, double x)
    {
        int i = LowerBound(range, x);
        return i != range.Count && !(x < range[i]);
    }

    // std::sort, then std::unique by ==: a stable sort, as MSVC's insertion sort is for the few elements here.
    private static List<T> SortedUnique<T>(IEnumerable<T> values, Func<T, T, bool> equal)
    {
        List<T> sorted = [.. values.OrderBy(x => x)];
        var unique = new List<T>(sorted.Count);
        foreach (T x in sorted)
        {
            if (unique.Count == 0 || !equal(unique[^1], x))
            {
                unique.Add(x);
            }
        }

        return unique;
    }

    // QuantLib's registerWithParametersGuess reads parametersGuess[j*nSwapTenors+k][i] for each pair and parameter,
    // unchecked.
    private void CheckGuesses(ImmutableArray<ImmutableArray<double>> guesses)
    {
        int rows = NOptionTenors * NSwapTenors;
        if (!(guesses.Length >= rows))
        {
            throw new ArgumentException(
                $"mismatch between number of option tenors * swap tenors ({rows}) and number of rows ({guesses.Length})",
                "parametersGuess");
        }

        for (int i = 0; i < rows; i++)
        {
            if (!(guesses[i].Length >= TModel.NParams))
            {
                throw new ArgumentException(
                    $"mismatch between number of parameters ({TModel.NParams}) and number of columns "
                    + $"({guesses[i].Length}) in the {DataFormattersFunctions.Ordinal(i + 1)} row",
                    "parametersGuess");
            }
        }
    }

    // QuantLib's setParameterGuess, on the cube's grid.
    private Cube ParameterGuess(ImmutableArray<ImmutableArray<double>> guesses)
    {
        var cube = new Cube(OptionDates, SwapTenors, OptionTimes, SwapLengths, TModel.NParams, backwardFlat);
        for (int i = 0; i < TModel.NParams; i++)
        {
            for (int j = 0; j < NOptionTenors; j++)
            {
                for (int k = 0; k < NSwapTenors; k++)
                {
                    cube.SetElement(i, j, k, guesses[(j * NSwapTenors) + k][i]);
                }
            }
        }

        cube.UpdateInterpolators();
        return cube;
    }

    // QuantLib's createSparseSmiles: the sections at the sparse nodes.
    private ImmutableArray<ImmutableArray<SmileSection>> CreateSparseSmiles(Cube sparse)
    {
        List<double> optionTimes = [.. sparse.OptionTimes];
        List<double> swapLengths = [.. sparse.SwapLengths];
        var smiles = ImmutableArray.CreateBuilder<ImmutableArray<SmileSection>>(optionTimes.Count);
        foreach (double optionTime in optionTimes)
        {
            var tmp = ImmutableArray.CreateBuilder<SmileSection>(swapLengths.Count);
            for (int k = 0; k < swapLengths.Count; ++k)
            {
                tmp.Add(SmileSection(optionTime, swapLengths[k], sparse));
            }

            smiles.Add(tmp.MoveToImmutable());
        }

        return smiles.MoveToImmutable();
    }

    // QuantLib's fillVolatilityCube: the dense grid's volatilities, into the at-the-money-calibrated cube; it returns
    // the sparse sections it builds, of the sparse parameters given, which QuantLib keeps.
    private ImmutableArray<ImmutableArray<SmileSection>> FillVolatilityCube(
        bool marketSpreads, Cube? sparse, EvaluationContext context)
    {
        if (AtmVol is not SwaptionVolatilityDiscrete atmVolStructure)
        {
            throw new ArgumentException(
                "isAtmCalibrated requires an ATM volatility structure derived from SwaptionVolatilityDiscrete (e.g. "
                + "SwaptionVolatilityMatrix), but the provided atmVolStructure is not",
                "atmVolStructure");
        }

        List<double> optionTimes = [.. volCubeAtmCalibrated.OptionTimes];
        List<double> atmOptionTimes = SortedUnique(atmVolStructure.OptionTimes.Concat(optionTimes), (a, b) => a == b);

        List<double> swapLengths = [.. volCubeAtmCalibrated.SwapLengths];
        List<double> atmSwapLengths = SortedUnique(atmVolStructure.SwapLengths.Concat(swapLengths), (a, b) => a == b);

        List<Date> atmOptionDates =
            SortedUnique(atmVolStructure.OptionDates.Concat(volCubeAtmCalibrated.OptionDates), (a, b) => a == b);

        List<Period> atmSwapTenors =
            SortedUnique(atmVolStructure.SwapTenors.Concat(volCubeAtmCalibrated.SwapTenors), (a, b) => a == b);

        ImmutableArray<ImmutableArray<SmileSection>> smiles = marketSpreads ? [] : CreateSparseSmiles(sparse!);

        for (int j = 0; j < atmOptionTimes.Count; j++)
        {
            for (int k = 0; k < atmSwapLengths.Count; k++)
            {
                bool expandOptionTimes = !BinarySearch(optionTimes, atmOptionTimes[j]);
                bool expandSwapLengths = !BinarySearch(swapLengths, atmSwapLengths[k]);
                if (expandOptionTimes || expandSwapLengths)
                {
                    double atmForward = AtmStrike(atmOptionDates[j], atmSwapTenors[k], context);
                    double atmVol = AtmVol.Volatility(atmOptionDates[j], atmSwapTenors[k], atmForward);
                    double[] spreadVols = SpreadVolInterpolation(
                        atmOptionDates[j], atmSwapTenors[k], marketSpreads, sparse, smiles, context);
                    var volAtmCalibrated = new double[NStrikes];
                    for (int i = 0; i < NStrikes; i++)
                    {
                        volAtmCalibrated[i] = atmVol + spreadVols[i];
                    }

                    volCubeAtmCalibrated.SetPoint(
                        atmOptionDates[j], atmSwapTenors[k], atmOptionTimes[j], atmSwapLengths[k], volAtmCalibrated);
                }
            }
        }

        volCubeAtmCalibrated.UpdateInterpolators();
        return smiles;
    }

    // QuantLib's spreadVolInterpolation: a dense point's spreads, interpolated between the four surrounding nodes.
    private double[] SpreadVolInterpolation(
        Date atmOptionDate,
        Period atmSwapTenor,
        bool marketSpreads,
        Cube? sparse,
        ImmutableArray<ImmutableArray<SmileSection>> sparseSmiles,
        EvaluationContext context)
    {
        double atmOptionTime = TimeFromReference(atmOptionDate);
        double atmTimeLength = SwapLength(atmSwapTenor);

        var result = new double[NStrikes];
        Cube sourceCube = marketSpreads ? marketVolCube : sparse!;
        IReadOnlyList<double> optionTimes = sourceCube.OptionTimes;
        IReadOnlyList<double> swapLengths = sourceCube.SwapLengths;
        IReadOnlyList<Date> optionDates = sourceCube.OptionDates;
        IReadOnlyList<Period> swapTenors = sourceCube.SwapTenors;

        int optionTimesPreviousIndex = LowerBound(optionTimes, atmOptionTime);
        if (optionTimesPreviousIndex > 0)
        {
            optionTimesPreviousIndex--;
        }

        int swapLengthsPreviousIndex = LowerBound(swapLengths, atmTimeLength);
        if (swapLengthsPreviousIndex > 0)
        {
            swapLengthsPreviousIndex--;
        }

        SmileSection[,] smiles = new SmileSection[2, 2];
        if (marketSpreads)
        {
            if (!(optionTimesPreviousIndex + 1 < optionTimes.Count))
            {
                throw new NanoQLException(
                    $"cannot interpolate market spreads at option time {Show(atmOptionTime)}");
            }

            if (!(swapLengthsPreviousIndex + 1 < swapLengths.Count))
            {
                throw new NanoQLException(
                    $"cannot interpolate market spreads at swap length {Show(atmTimeLength)}");
            }
        }
        else
        {
            if (!(optionTimesPreviousIndex + 1 < sparseSmiles.Length))
            {
                throw new NanoQLException("optionTimesPreviousIndex+1 >= sparseSmiles_.size()");
            }

            if (!(swapLengthsPreviousIndex + 1 < sparseSmiles[0].Length))
            {
                throw new NanoQLException("swapLengthsPreviousIndex+1 >= sparseSmiles_[0].size()");
            }

            smiles[0, 0] = sparseSmiles[optionTimesPreviousIndex][swapLengthsPreviousIndex];
            smiles[0, 1] = sparseSmiles[optionTimesPreviousIndex][swapLengthsPreviousIndex + 1];
            smiles[1, 0] = sparseSmiles[optionTimesPreviousIndex + 1][swapLengthsPreviousIndex];
            smiles[1, 1] = sparseSmiles[optionTimesPreviousIndex + 1][swapLengthsPreviousIndex + 1];
        }

        double[] optionsNodes = [optionTimes[optionTimesPreviousIndex], optionTimes[optionTimesPreviousIndex + 1]];
        Date[] optionsDateNodes = [optionDates[optionTimesPreviousIndex], optionDates[optionTimesPreviousIndex + 1]];
        double[] swapLengthsNodes = [swapLengths[swapLengthsPreviousIndex], swapLengths[swapLengthsPreviousIndex + 1]];
        Period[] swapTenorNodes = [swapTenors[swapLengthsPreviousIndex], swapTenors[swapLengthsPreviousIndex + 1]];

        double atmForward = AtmStrike(atmOptionDate, atmSwapTenor, context);
        double shift = AtmVol.Shift(atmOptionTime, atmTimeLength);

        var atmForwards = new Matrix(2, 2, 0.0);
        var atmShifts = new Matrix(2, 2, 0.0);
        var atmVols = new Matrix(2, 2, 0.0);
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                atmForwards[i, j] = AtmStrike(optionsDateNodes[i], swapTenorNodes[j], context);
                atmShifts[i, j] = AtmVol.Shift(optionsNodes[i], swapLengthsNodes[j]);
                if (!marketSpreads)
                {
                    // QuantLib's note: the dense at-the-money volatilities carry the interpolated fit errors on the
                    // sparse ones, negligible for good fits.
                    atmVols[i, j] = AtmVol.Volatility(optionsDateNodes[i], swapTenorNodes[j], atmForwards[i, j]);
                }
            }
        }

        ImmutableArray<double> strikeSpreads = StrikeSpreads;
        ImmutableArray<ImmutableArray<double>> volSpreads = VolSpreads();
        for (int k = 0; k < NStrikes; k++)
        {
            double spreadStrike = atmForward + strikeSpreads[k];
            double floor = cutoffStrike - shift;
            double strike = spreadStrike < floor ? floor : spreadStrike;
            double moneyness = (atmForward + shift) / (strike + shift);

            var strikes = new Matrix(2, 2, 0.0);
            var spreadVols = new Matrix(2, 2, 0.0);
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    if (volatilityType == VolatilityType.Normal)
                    {
                        strikes[i, j] = atmForwards[i, j] + strike - atmForward;
                    }
                    else
                    {
                        strikes[i, j] = ((atmForwards[i, j] + atmShifts[i, j]) / moneyness) - atmShifts[i, j];
                    }

                    if (marketSpreads)
                    {
                        int section = ((optionTimesPreviousIndex + i) * NSwapTenors) + swapLengthsPreviousIndex + j;
                        if (NStrikes == 1)
                        {
                            spreadVols[i, j] = volSpreads[section][0];
                        }
                        else
                        {
                            var quotedSpreads = new double[NStrikes];
                            for (int n = 0; n < NStrikes; ++n)
                            {
                                quotedSpreads[n] = volSpreads[section][n];
                            }

                            LinearInterpolation strikeInterpolation =
                                new LinearInterpolation(strikeSpreads.AsSpan(), quotedSpreads).EnableExtrapolation();
                            spreadVols[i, j] = strikeInterpolation.Evaluate(strikes[i, j] - atmForwards[i, j]);
                        }
                    }
                    else
                    {
                        spreadVols[i, j] = smiles[i, j].Volatility(strikes[i, j]) - atmVols[i, j];
                    }
                }
            }

            var localInterpolator = new Cube(optionsDateNodes, swapTenorNodes, optionsNodes, swapLengthsNodes, 1);
            localInterpolator.SetLayer(0, spreadVols);
            localInterpolator.UpdateInterpolators();

            result[k] = localInterpolator.Evaluate(atmOptionTime, atmTimeLength)[0];
        }

        return result;
    }

    /// <summary>
    /// QuantLib's nested <c>Cube</c>: layers of values on a grid of option times and swap lengths, each layer
    /// interpolated bilinearly or, for the first five with backward-flat interpolation, backward flat in option time,
    /// both flat outside the grid. A working type the cube's constructor fills and then freezes.
    /// </summary>
    [WorkingType]
    private protected sealed class Cube
    {
        private readonly int nLayers;
        private readonly bool backwardFlat;
        private readonly List<double> optionTimes;
        private readonly List<double> swapLengths;
        private readonly List<Date> optionDates;
        private readonly List<Period> swapTenors;
        private Matrix[] points;
        private Interpolation2D[] interpolators;

        /// <summary>Initializes a new instance of the <see cref="Cube"/> class, its layers zero.</summary>
        /// <param name="optionDates">The option dates.</param>
        /// <param name="swapTenors">The swap tenors.</param>
        /// <param name="optionTimes">The option times, one per date; at least 2.</param>
        /// <param name="swapLengths">The swap lengths, one per tenor; at least 2.</param>
        /// <param name="nLayers">The number of layers.</param>
        /// <param name="backwardFlat">Whether the first five layers are interpolated backward flat in option time.</param>
        /// <remarks>QuantLib's extrapolation flag, stored and never read, isn't ported.</remarks>
        /// <exception cref="NanoQLException">There are fewer than 2 times or lengths, or their dates or tenors are as
        /// many.</exception>
        public Cube(
            IReadOnlyList<Date> optionDates,
            IReadOnlyList<Period> swapTenors,
            IReadOnlyList<double> optionTimes,
            IReadOnlyList<double> swapLengths,
            int nLayers,
            bool backwardFlat = false)
        {
            if (!(optionTimes.Count > 1))
            {
                throw new NanoQLException("Cube::Cube(...): optionTimes.size()<2");
            }

            if (!(swapLengths.Count > 1))
            {
                throw new NanoQLException("Cube::Cube(...): swapLengths.size()<2");
            }

            if (!(optionTimes.Count == optionDates.Count))
            {
                throw new NanoQLException("Cube::Cube(...): optionTimes/optionDates mismatch");
            }

            if (!(swapTenors.Count == swapLengths.Count))
            {
                throw new NanoQLException("Cube::Cube(...): swapTenors/swapLengths mismatch");
            }

            this.optionTimes = [.. optionTimes];
            this.swapLengths = [.. swapLengths];
            this.optionDates = [.. optionDates];
            this.swapTenors = [.. swapTenors];
            this.nLayers = nLayers;
            this.backwardFlat = backwardFlat;
            points = new Matrix[nLayers];
            for (int k = 0; k < nLayers; k++)
            {
                points[k] = new Matrix(optionTimes.Count, swapLengths.Count, 0.0);
            }

            interpolators = new Interpolation2D[nLayers];
            UpdateInterpolators();
        }

        /// <summary>Initializes a new instance of the <see cref="Cube"/> class, a copy of another.</summary>
        /// <param name="o">The cube copied.</param>
        public Cube(Cube o)
        {
            optionTimes = [.. o.optionTimes];
            swapLengths = [.. o.swapLengths];
            optionDates = [.. o.optionDates];
            swapTenors = [.. o.swapTenors];
            nLayers = o.nLayers;
            backwardFlat = o.backwardFlat;
            points = [.. o.points.Select(p => p.Clone())];
            interpolators = new Interpolation2D[nLayers];
            UpdateInterpolators();
        }

        /// <summary>Gets the option dates.</summary>
        public IReadOnlyList<Date> OptionDates => optionDates;

        /// <summary>Gets the swap tenors.</summary>
        public IReadOnlyList<Period> SwapTenors => swapTenors;

        /// <summary>Gets the option times.</summary>
        public IReadOnlyList<double> OptionTimes => optionTimes;

        /// <summary>Gets the swap lengths.</summary>
        public IReadOnlyList<double> SwapLengths => swapLengths;

        /// <summary>Gets the layers: a row per option time and a column per swap length.</summary>
        public IReadOnlyList<Matrix> Points => points;

        /// <summary>Sets an element of a layer.</summary>
        /// <param name="indexOfLayer">The layer.</param>
        /// <param name="indexOfRow">The option time's index.</param>
        /// <param name="indexOfColumn">The swap length's index.</param>
        /// <param name="x">The value.</param>
        /// <exception cref="NanoQLException">An index is out of range.</exception>
        public void SetElement(int indexOfLayer, int indexOfRow, int indexOfColumn, double x)
        {
            if (!(indexOfLayer < nLayers))
            {
                throw new NanoQLException("Cube::setElement: incompatible IndexOfLayer ");
            }

            if (!(indexOfRow < optionTimes.Count))
            {
                throw new NanoQLException("Cube::setElement: incompatible IndexOfRow");
            }

            if (!(indexOfColumn < swapLengths.Count))
            {
                throw new NanoQLException("Cube::setElement: incompatible IndexOfColumn");
            }

            points[indexOfLayer][indexOfRow, indexOfColumn] = x;
        }

        /// <summary>Sets every layer, from copies.</summary>
        /// <param name="x">The layers.</param>
        /// <exception cref="NanoQLException">Their number or size isn't the cube's.</exception>
        public void SetPoints(IReadOnlyList<Matrix> x)
        {
            if (!(x.Count == nLayers))
            {
                throw new NanoQLException("Cube::setPoints: incompatible number of layers ");
            }

            if (!(x[0].Rows == optionTimes.Count))
            {
                throw new NanoQLException("Cube::setPoints: incompatible size 1");
            }

            if (!(x[0].Columns == swapLengths.Count))
            {
                throw new NanoQLException("Cube::setPoints: incompatible size 2");
            }

            points = [.. x.Select(m => m.Clone())];
        }

        /// <summary>Sets a layer, from a copy.</summary>
        /// <param name="i">The layer.</param>
        /// <param name="x">The values.</param>
        /// <exception cref="NanoQLException">The layer is out of range, or the size isn't the cube's.</exception>
        public void SetLayer(int i, Matrix x)
        {
            if (!(i < nLayers))
            {
                throw new NanoQLException("Cube::setLayer: incompatible number of layer ");
            }

            if (!(x.Rows == optionTimes.Count))
            {
                throw new NanoQLException("Cube::setLayer: incompatible size 1");
            }

            if (!(x.Columns == swapLengths.Count))
            {
                throw new NanoQLException("Cube::setLayer: incompatible size 2");
            }

            points[i] = x.Clone();
        }

        /// <summary>
        /// Sets every layer's value at an option time and a swap length, inserting the time or the length into the grid
        /// where it isn't a node.
        /// </summary>
        /// <param name="optionDate">The option date.</param>
        /// <param name="swapTenor">The swap tenor.</param>
        /// <param name="optionTime">The option time.</param>
        /// <param name="swapLength">The swap length.</param>
        /// <param name="point">A value per layer.</param>
        public void SetPoint(Date optionDate, Period swapTenor, double optionTime, double swapLength, IReadOnlyList<double> point)
        {
            bool expandOptionTimes = !BinarySearch(optionTimes, optionTime);
            bool expandSwapLengths = !BinarySearch(swapLengths, swapLength);

            int optionTimesIndex = LowerBound(optionTimes, optionTime);
            int swapLengthsIndex = LowerBound(swapLengths, swapLength);

            if (expandOptionTimes || expandSwapLengths)
            {
                ExpandLayers(optionTimesIndex, expandOptionTimes, swapLengthsIndex, expandSwapLengths);
            }

            for (int k = 0; k < nLayers; ++k)
            {
                points[k][optionTimesIndex, swapLengthsIndex] = point[k];
            }

            optionTimes[optionTimesIndex] = optionTime;
            swapLengths[swapLengthsIndex] = swapLength;
            optionDates[optionTimesIndex] = optionDate;
            swapTenors[swapLengthsIndex] = swapTenor;
        }

        /// <summary>Inserts a row, a column, or both, into every layer, with zero values and placeholder nodes.</summary>
        /// <param name="i">The row's position.</param>
        /// <param name="expandOptionTimes">Whether to insert the row.</param>
        /// <param name="j">The column's position.</param>
        /// <param name="expandSwapLengths">Whether to insert the column.</param>
        /// <exception cref="NanoQLException">A position is past the grid's end.</exception>
        public void ExpandLayers(int i, bool expandOptionTimes, int j, bool expandSwapLengths)
        {
            if (!(i <= optionTimes.Count))
            {
                throw new NanoQLException("Cube::expandLayers: incompatible size 1");
            }

            if (!(j <= swapLengths.Count))
            {
                throw new NanoQLException("Cube::expandLayers: incompatible size 2");
            }

            // QuantLib's placeholders, Date() and Period(), which SetPoint overwrites.
            if (expandOptionTimes)
            {
                optionTimes.Insert(i, 0.0);
                optionDates.Insert(i, default);
            }

            if (expandSwapLengths)
            {
                swapLengths.Insert(j, 0.0);
                swapTenors.Insert(j, default);
            }

            var newPoints = new Matrix[nLayers];
            for (int k = 0; k < nLayers; ++k)
            {
                newPoints[k] = new Matrix(optionTimes.Count, swapLengths.Count, 0.0);
                for (int u = 0; u < points[k].Rows; ++u)
                {
                    int indexOfRow = u;
                    if (u >= i && expandOptionTimes)
                    {
                        indexOfRow = u + 1;
                    }

                    for (int v = 0; v < points[k].Columns; ++v)
                    {
                        int indexOfCol = v;
                        if (v >= j && expandSwapLengths)
                        {
                            indexOfCol = v + 1;
                        }

                        newPoints[k][indexOfRow, indexOfCol] = points[k][u, v];
                    }
                }
            }

            SetPoints(newPoints);
        }

        /// <summary>Every layer's value at an option time and a swap length: QuantLib's <c>operator()</c>.</summary>
        /// <param name="optionTime">The option time.</param>
        /// <param name="swapLength">The swap length.</param>
        /// <returns>A value per layer.</returns>
        public double[] Evaluate(double optionTime, double swapLength)
        {
            var result = new double[nLayers];
            for (int k = 0; k < nLayers; ++k)
            {
                result[k] = interpolators[k].Evaluate(optionTime, swapLength);
            }

            return result;
        }

        /// <summary>Rebuilds every layer's interpolation from its values.</summary>
        public void UpdateInterpolators()
        {
            for (int k = 0; k < nLayers; ++k)
            {
                Matrix transposedPoints = MatrixFunctions.Transpose(points[k]);

                // QuantLib's k <= 4 takes the first five layers backward flat: a four-parameter model's forward too.
                Interpolation2D interpolation = k <= 4 && backwardFlat
                    ? new BackwardflatLinearInterpolation(
                        AsSpan(optionTimes), AsSpan(swapLengths), transposedPoints)
                    : new BilinearInterpolation(
                        AsSpan(optionTimes), AsSpan(swapLengths), transposedPoints);
                interpolators[k] = new FlatExtrapolator2D(interpolation).EnableExtrapolation();
            }
        }

        /// <summary>The layers as a table: a row per node, swap length by swap length.</summary>
        /// <returns>A new matrix: for each node the swap length, the option time and a value per layer.</returns>
        public Matrix Browse()
        {
            var result = new Matrix(swapLengths.Count * optionTimes.Count, nLayers + 2, 0.0);
            for (int i = 0; i < swapLengths.Count; ++i)
            {
                for (int j = 0; j < optionTimes.Count; ++j)
                {
                    result[(i * optionTimes.Count) + j, 0] = swapLengths[i];
                    result[(i * optionTimes.Count) + j, 1] = optionTimes[j];
                    for (int k = 0; k < nLayers; ++k)
                    {
                        result[(i * optionTimes.Count) + j, 2 + k] = points[k][j, i];
                    }
                }
            }

            return result;
        }

        private static ReadOnlySpan<double> AsSpan(List<double> list) =>
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list);
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
