// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/cmsmarketcalibration.hpp ql/termstructures/volatility/swaption/cmsmarketcalibration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The calibration of a SABR swaption cube's βs, and of the CMS pricers' mean reversion, to a CMS market: QuantLib's
/// <c>CmsMarketCalibration</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each cost evaluation recalibrates the cube's sections at the candidate βs, one swap tenor after another, reprices
/// the market on it at the candidate reversion, and reads the market's weighted errors on the calibration's type. The
/// βs enter the optimizer as y with β = exp(−y²), clamped to [1e-6, 0.999999], and the reversion as y with reversion =
/// √|y|. The βs reach the cube as its sections' β guesses: they are the cube's βs on a cube that fixes β, and the
/// refit's starting point on one that doesn't, whose results' cube holds the βs fitted.
/// </para>
/// <para>
/// QuantLib's calibration recalibrates its cube and reprices its market in place; here each
/// <see cref="Compute(EndCriteria, OptimizationMethod, Vector, bool, EvaluationContext)"/> starts from the cube and
/// market it was given, each cost evaluation building a new cube from the last one and a new market, as QuantLib's
/// evaluations change the one cube in turn, and it returns the recalibrated cube and repriced market in its results,
/// the calibration's own left unchanged. Three of QuantLib's defects are fixed (R8): the fixed-reversion path's β guess
/// enters the optimizer transformed, as its other paths' do (the S-cms plan's CMR5 (x)); the reversion is √|y|, where
/// QuantLib's √y returns NaN for an optimizer step to y below 0 (CMR5 (xi)); and a fixed reversion is passed to the
/// pricers as given, where QuantLib passes it through the transform's round trip, √(m²) = |m| (CMR5 (xxvi)).
/// </para>
/// </remarks>
public sealed class CmsMarketCalibration
{
    // Parametric beta term structure: betaInf, beta0, and decay.
    private const int NParametricParameters = 3;
    private const int BetaInfIndex = 0;
    private const int Beta0Index = 1;
    private const int DecayIndex = 2;

    private readonly XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> volCube;
    private readonly CmsMarket cmsMarket;
    private readonly Matrix weights;
    private readonly CmsMarketCalibrationCalibrationType calibrationType;

    /// <summary>Initializes a new instance of the <see cref="CmsMarketCalibration"/> class.</summary>
    /// <param name="volCube">The SABR swaption cube whose βs are calibrated.</param>
    /// <param name="cmsMarket">The CMS market.</param>
    /// <param name="weights">The errors' weights: a row per swap length and a column per swap index.</param>
    /// <param name="calibrationType">What the calibration fits.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The weights aren't a row per swap length and a column per swap
    /// index.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="calibrationType"/> isn't a member of its
    /// type.</exception>
    public CmsMarketCalibration(
        XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> volCube,
        CmsMarket cmsMarket,
        Matrix weights,
        CmsMarketCalibrationCalibrationType calibrationType)
    {
        ArgumentNullException.ThrowIfNull(volCube);
        ArgumentNullException.ThrowIfNull(cmsMarket);
        ArgumentNullException.ThrowIfNull(weights);
        EnumArguments.Check(calibrationType, nameof(calibrationType));
        this.volCube = volCube;
        this.cmsMarket = cmsMarket;
        this.weights = weights.Clone();
        this.calibrationType = calibrationType;

        if (!(weights.Rows == cmsMarket.SwapLengths.Length))
        {
            throw new ArgumentException(
                $"weights number of rows ({weights.Rows}) must be equal to number of swap lengths "
                + $"({cmsMarket.SwapLengths.Length})",
                nameof(weights));
        }

        if (!(weights.Columns == cmsMarket.SwapTenors.Length))
        {
            throw new ArgumentException(
                $"weights number of columns ({weights.Columns}) must be equal to number of swap indexes "
                + $"({cmsMarket.SwapTenors.Length}",
                nameof(weights));
        }
    }

    // The cost function's paths: QuantLib's ObjectiveFunction to ObjectiveFunction6.
    private enum CostPath
    {
        ConstantBetaFreeReversion,
        ConstantBetaFixedReversion,
        BetaTermStructureFreeReversion,
        BetaTermStructureFixedReversion,
        ParametricBetaFixedReversion,
        ParametricBetaFreeReversion,
    }

    /// <summary>The y of a β: √(−ln β).</summary>
    /// <param name="beta">The β.</param>
    /// <returns>The y.</returns>
    public static double BetaTransformInverse(double beta) => Math.Sqrt(-Math.Log(beta));

    /// <summary>The β of a y: exp(−y²) for |y| below 10, else 0, clamped to [1e-6, 0.999999].</summary>
    /// <param name="y">The y.</param>
    /// <returns>The β.</returns>
    public static double BetaTransformDirect(double y)
    {
        double beta = Math.Abs(y) < 10.0 ? Math.Exp(-(y * y)) : 0.0;

        // std::max(std::min(beta, 0.999999), 0.000001)
        double capped = beta < 0.999999 ? beta : 0.999999;
        return capped < 0.000001 ? 0.000001 : capped;
    }

    /// <summary>The y of a reversion: its square.</summary>
    /// <param name="reversion">The reversion.</param>
    /// <returns>The y.</returns>
    public static double ReversionTransformInverse(double reversion) => reversion * reversion;

    /// <summary>The reversion of a y: √|y|, which QuantLib's √y is for every y at or above 0 (R8, the S-cms plan's CMR5
    /// (xi)).</summary>
    /// <param name="y">The y.</param>
    /// <returns>The reversion.</returns>
    public static double ReversionTransformDirect(double y) => Math.Sqrt(Math.Abs(y));

    /// <summary>
    /// Calibrates a β per swap index and, unless it is fixed, the mean reversion: QuantLib's <c>compute</c> with a
    /// vector guess.
    /// </summary>
    /// <param name="endCriteria">The optimizer's end criteria.</param>
    /// <param name="method">The optimizer.</param>
    /// <param name="guess">The βs' guesses, one per swap index, then the reversion's guess, or with the reversion fixed
    /// the reversion itself, which may be left out to keep the pricers'.</param>
    /// <param name="isMeanReversionFixed">Whether the reversion is fixed.</param>
    /// <param name="context">The evaluation context the cube and the market are calibrated under.</param>
    /// <returns>The results, whose result holds the βs and, when given or calibrated, the reversion.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reversion is free and no guess is given for it, or the guess's size is
    /// neither the number of swap indexes nor one more.</exception>
    /// <exception cref="NanoQLException">A section's fit or the market's pricing fails.</exception>
    public CmsMarketCalibrationResults<Vector> Compute(
        EndCriteria endCriteria, OptimizationMethod method, Vector guess, bool isMeanReversionFixed, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(endCriteria);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(guess);
        ArgumentNullException.ThrowIfNull(context);
        int nSwapTenors = cmsMarket.SwapTenors.Length;
        if (!(isMeanReversionFixed || guess.Size == nSwapTenors + 1))
        {
            throw new ArgumentException("if mean reversion is not fixed, a guess must be provided", nameof(guess));
        }

        if (!(nSwapTenors == guess.Size || nSwapTenors == guess.Size - 1))
        {
            throw new ArgumentException(
                $"guess size ({guess.Size}) must be equal to swap tenors size ({nSwapTenors}) or greater by one if "
                + "mean reversion is given as last element",
                nameof(guess));
        }

        bool isMeanReversionGiven = nSwapTenors == guess.Size - 1;
        int nBeta = guess.Size - (isMeanReversionGiven ? 1 : 0);
        Vector result;
        ObjectiveFunction costFunction;
        double error;
        EndCriteriaType endCriteriaType;
        if (isMeanReversionFixed)
        {
            double? fixedMeanReversion = isMeanReversionGiven ? guess[nBeta] : null;
            var betasGuess = new double[nBeta];
            for (int i = 0; i < nBeta; ++i)
            {
                // transformed, as the other paths' guesses are, where QuantLib's passes guess[i] (CMR5 (x))
                betasGuess[i] = BetaTransformInverse(guess[i]);
            }

            costFunction = new ObjectiveFunction(this, CostPath.ConstantBetaFixedReversion, fixedMeanReversion, context);
            (Vector tmp, endCriteriaType) = Minimize(method, endCriteria, costFunction, betasGuess);
            error = costFunction.Value(tmp);
            result = new Vector(nBeta + (isMeanReversionGiven ? 1 : 0));
            for (int i = 0; i < nBeta; ++i)
            {
                result[i] = BetaTransformDirect(tmp[i]);
            }

            if (isMeanReversionGiven)
            {
                result[nBeta] = fixedMeanReversion!.Value;
            }
        }
        else
        {
            costFunction = new ObjectiveFunction(this, CostPath.ConstantBetaFreeReversion, null, context);
            var betaReversionGuess = new double[nBeta + 1];
            for (int i = 0; i < nBeta; ++i)
            {
                betaReversionGuess[i] = BetaTransformInverse(guess[i]);
            }

            betaReversionGuess[nBeta] = ReversionTransformInverse(guess[nBeta]);
            (result, endCriteriaType) = Minimize(method, endCriteria, costFunction, betaReversionGuess);
            error = costFunction.Value(result);
            for (int i = 0; i < nBeta; ++i)
            {
                result[i] = BetaTransformDirect(result[i]);
            }

            result[nBeta] = ReversionTransformDirect(result[nBeta]);
        }

        return Results(result, costFunction, error, endCriteriaType, context);
    }

    /// <summary>
    /// Calibrates a β term structure per swap index, a β per swap length, and, unless it is fixed, the mean reversion:
    /// QuantLib's <c>compute</c> with a matrix guess.
    /// </summary>
    /// <param name="endCriteria">The optimizer's end criteria.</param>
    /// <param name="method">The optimizer.</param>
    /// <param name="guess">The βs' guesses: a row per swap length and a column per swap index.</param>
    /// <param name="isMeanReversionFixed">Whether the reversion is fixed.</param>
    /// <param name="context">The evaluation context the cube and the market are calibrated under.</param>
    /// <param name="meanReversionGuess">The reversion's guess, or with the reversion fixed the reversion itself;
    /// <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, to keep the pricers' with the reversion
    /// fixed.</param>
    /// <returns>The results, whose result holds a row per swap length, the βs per swap index and, when given or
    /// calibrated, the reversion in a last column.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reversion is free and no guess is given for it, or the guess isn't a
    /// row per swap length and a column per swap index.</exception>
    /// <exception cref="NanoQLException">A section's fit or the market's pricing fails.</exception>
    public CmsMarketCalibrationResults<Matrix> Compute(
        EndCriteria endCriteria,
        OptimizationMethod method,
        Matrix guess,
        bool isMeanReversionFixed,
        EvaluationContext context,
        double? meanReversionGuess = null)
    {
        ArgumentNullException.ThrowIfNull(endCriteria);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(guess);
        ArgumentNullException.ThrowIfNull(context);
        int nSwapTenors = cmsMarket.SwapTenors.Length;
        int nSwapLengths = cmsMarket.SwapLengths.Length;
        if (!(isMeanReversionFixed || meanReversionGuess is not null))
        {
            throw new ArgumentException("if mean reversion is not fixed, a guess must be provided", nameof(meanReversionGuess));
        }

        if (!(nSwapTenors == guess.Columns))
        {
            throw new ArgumentException(
                $"number of swap tenors ({nSwapTenors}) must be equal to number of guess columns ({guess.Columns})",
                nameof(guess));
        }

        if (!(nSwapLengths == guess.Rows))
        {
            throw new ArgumentException(
                $"number of swap lengths ({nSwapLengths}) must be equal to number of guess rows ({guess.Rows})",
                nameof(guess));
        }

        Matrix result;
        int nBeta = nSwapTenors * nSwapLengths;
        ObjectiveFunction costFunction;
        double error;
        EndCriteriaType endCriteriaType;
        if (isMeanReversionFixed)
        {
            var betasGuess = new double[nBeta];
            for (int i = 0; i < nSwapTenors; ++i)
            {
                for (int j = 0; j < nSwapLengths; ++j)
                {
                    betasGuess[(i * nSwapLengths) + j] = BetaTransformInverse(guess[j, i]);
                }
            }

            costFunction = new ObjectiveFunction(this, CostPath.BetaTermStructureFixedReversion, meanReversionGuess, context);
            (Vector tmp, endCriteriaType) = Minimize(method, endCriteria, costFunction, betasGuess);
            error = costFunction.Value(tmp);
            result = new Matrix(nSwapLengths, nSwapTenors + (meanReversionGuess is not null ? 1 : 0));
            for (int i = 0; i < nSwapTenors; ++i)
            {
                for (int j = 0; j < nSwapLengths; ++j)
                {
                    result[j, i] = BetaTransformDirect(tmp[(i * nSwapLengths) + j]);
                }
            }

            if (meanReversionGuess is double m)
            {
                for (int j = 0; j < nSwapLengths; ++j)
                {
                    result[j, nSwapTenors] = m;
                }
            }
        }
        else
        {
            var betasReversionGuess = new double[nBeta + 1];
            for (int i = 0; i < nSwapTenors; ++i)
            {
                for (int j = 0; j < nSwapLengths; ++j)
                {
                    betasReversionGuess[(i * nSwapLengths) + j] = BetaTransformInverse(guess[j, i]);
                }
            }

            betasReversionGuess[nBeta] = ReversionTransformInverse(meanReversionGuess!.Value);
            costFunction = new ObjectiveFunction(this, CostPath.BetaTermStructureFreeReversion, null, context);
            (Vector tmp, endCriteriaType) = Minimize(method, endCriteria, costFunction, betasReversionGuess);
            error = costFunction.Value(tmp);
            result = new Matrix(nSwapLengths, nSwapTenors + 1);
            for (int i = 0; i < nSwapTenors; ++i)
            {
                for (int j = 0; j < nSwapLengths; ++j)
                {
                    result[j, i] = BetaTransformDirect(tmp[(i * nSwapLengths) + j]);
                }
            }

            for (int j = 0; j < nSwapLengths; ++j)
            {
                result[j, nSwapTenors] = ReversionTransformDirect(tmp[nBeta]);
            }
        }

        return Results(result, costFunction, error, endCriteriaType, context);
    }

    /// <summary>
    /// Calibrates a parametric β term structure per swap index, β(t) = β∞ + (β₀ − β∞)·exp(−decay·t) at each swap
    /// length's option time, and, unless it is fixed, the mean reversion: QuantLib's <c>computeParametric</c>.
    /// </summary>
    /// <param name="endCriteria">The optimizer's end criteria.</param>
    /// <param name="method">The optimizer.</param>
    /// <param name="guess">The parameters' guesses: the rows β∞, β₀ and the decay, a column per swap index.</param>
    /// <param name="isMeanReversionFixed">Whether the reversion is fixed.</param>
    /// <param name="context">The evaluation context the cube and the market are calibrated under.</param>
    /// <param name="meanReversionGuess">The reversion's guess, or with the reversion fixed the reversion itself;
    /// <see langword="null"/> to keep the pricers' with the reversion fixed.</param>
    /// <returns>The results, whose result holds the three parameters' rows, a column per swap index and, when given or
    /// calibrated, the reversion in a last column.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reversion is free and no guess is given for it, or the guess isn't three
    /// rows and a column per swap index.</exception>
    /// <exception cref="NanoQLException">A section's fit or the market's pricing fails.</exception>
    public CmsMarketCalibrationResults<Matrix> ComputeParametric(
        EndCriteria endCriteria,
        OptimizationMethod method,
        Matrix guess,
        bool isMeanReversionFixed,
        EvaluationContext context,
        double? meanReversionGuess = null)
    {
        ArgumentNullException.ThrowIfNull(endCriteria);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(guess);
        ArgumentNullException.ThrowIfNull(context);
        int nSwapTenors = cmsMarket.SwapTenors.Length;
        if (!(isMeanReversionFixed || meanReversionGuess is not null))
        {
            throw new ArgumentException("if mean reversion is not fixed, a guess must be provided", nameof(meanReversionGuess));
        }

        if (!(nSwapTenors == guess.Columns))
        {
            throw new ArgumentException(
                $"number of swap tenors ({nSwapTenors}) must be equal to number of guess columns ({guess.Columns})",
                nameof(guess));
        }

        if (!(NParametricParameters == guess.Rows))
        {
            throw new ArgumentException(
                $"number of parameters ({NParametricParameters}) must be equal to number of guess rows ({guess.Rows})",
                nameof(guess));
        }

        Matrix result;
        int nParams = nSwapTenors * NParametricParameters;
        ObjectiveFunction costFunction;
        double error;
        EndCriteriaType endCriteriaType;
        var betasGuess = new double[nParams + (isMeanReversionFixed ? 0 : 1)];
        for (int i = 0; i < nSwapTenors; ++i)
        {
            for (int j = 0; j < NParametricParameters; ++j)
            {
                betasGuess[(i * NParametricParameters) + j] = j is BetaInfIndex or Beta0Index
                    ? BetaTransformInverse(guess[j, i])
                    : Math.Sqrt(guess[j, i]);
            }
        }

        if (isMeanReversionFixed)
        {
            costFunction = new ObjectiveFunction(this, CostPath.ParametricBetaFixedReversion, meanReversionGuess, context);
            (Vector tmp, endCriteriaType) = Minimize(method, endCriteria, costFunction, betasGuess);
            error = costFunction.Value(tmp);
            result = new Matrix(NParametricParameters, nSwapTenors + (meanReversionGuess is not null ? 1 : 0));
            FillParametric(result, tmp, nSwapTenors);
            if (meanReversionGuess is double m)
            {
                for (int j = 0; j < NParametricParameters; ++j)
                {
                    result[j, nSwapTenors] = m;
                }
            }
        }
        else
        {
            betasGuess[nParams] = ReversionTransformInverse(meanReversionGuess!.Value);
            costFunction = new ObjectiveFunction(this, CostPath.ParametricBetaFreeReversion, null, context);
            (Vector tmp, endCriteriaType) = Minimize(method, endCriteria, costFunction, betasGuess);
            error = costFunction.Value(tmp);
            result = new Matrix(NParametricParameters, nSwapTenors + 1);
            FillParametric(result, tmp, nSwapTenors);
            for (int j = 0; j < NParametricParameters; ++j)
            {
                result[j, nSwapTenors] = ReversionTransformDirect(tmp[nParams]);
            }
        }

        return Results(result, costFunction, error, endCriteriaType, context);
    }

    private static (Vector CurrentValue, EndCriteriaType EndCriteria) Minimize(
        OptimizationMethod method, EndCriteria endCriteria, CostFunction costFunction, double[] initialValue)
    {
        var problem = new Problem(costFunction, new NoConstraint(), initialValue);
        ProblemResults results = method.Minimize(problem, endCriteria);
        return (new Vector(results.CurrentValue.AsSpan()), results.EndCriteriaType);
    }

    private static void FillParametric(Matrix result, Vector tmp, int nSwapTenors)
    {
        for (int i = 0; i < nSwapTenors; ++i)
        {
            for (int j = 0; j < NParametricParameters; ++j)
            {
                double x = tmp[(i * NParametricParameters) + j];
                result[j, i] = j is BetaInfIndex or Beta0Index ? BetaTransformDirect(x) : x * x;
            }
        }
    }

    // QuantLib's ending: the cube's updateAfterRecalibration, its parameters, and the market, which QuantLib's lazy
    // market reprices on the updated cube when browse() reads it, at the reversion its pricers last took.
    private CmsMarketCalibrationResults<TResult> Results<TResult>(
        TResult result, ObjectiveFunction costFunction, double error, EndCriteriaType endCriteria, EvaluationContext context)
        where TResult : class
    {
        XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> cube = costFunction.Cube.UpdatedAfterRecalibration(context);
        CmsMarket market = costFunction.Market.Reprice(cube, null, context);
        Matrix? dense = cube.HasDenseParameters ? cube.DenseSabrParameters() : null;
        return new CmsMarketCalibrationResults<TResult>(
            result, cube, market, cube.SparseSabrParameters(), dense, error, endCriteria);
    }

    // QuantLib's ObjectiveFunction to ObjectiveFunction6: each evaluation recalibrates the cube's sections from the
    // previous evaluation's cube, as QuantLib's change the one cube in turn, and reprices the market on it.
    private sealed class ObjectiveFunction : CostFunction
    {
        private readonly CmsMarketCalibration smileAndCms;
        private readonly CostPath path;
        private readonly double? fixedMeanReversion;
        private readonly EvaluationContext context;

        public ObjectiveFunction(
            CmsMarketCalibration smileAndCms, CostPath path, double? fixedMeanReversion, EvaluationContext context)
        {
            this.smileAndCms = smileAndCms;
            this.path = path;
            this.fixedMeanReversion = fixedMeanReversion;
            this.context = context;
            Cube = smileAndCms.volCube;
            Market = smileAndCms.cmsMarket;
        }

        // The last evaluation's cube and market.
        public XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> Cube { get; private set; }

        public CmsMarket Market { get; private set; }

        public override double Value(Vector x)
        {
            UpdateVolatilityCubeAndCmsMarket(x);
            return smileAndCms.calibrationType switch
            {
                CmsMarketCalibrationCalibrationType.OnSpread => Market.WeightedSpreadError(smileAndCms.weights),
                CmsMarketCalibrationCalibrationType.OnPrice => Market.WeightedSpotNpvError(smileAndCms.weights),
                _ => Market.WeightedFwdNpvError(smileAndCms.weights),
            };
        }

        public override Vector Values(Vector x)
        {
            UpdateVolatilityCubeAndCmsMarket(x);
            return smileAndCms.calibrationType switch
            {
                CmsMarketCalibrationCalibrationType.OnSpread => Market.WeightedSpreadErrors(smileAndCms.weights),
                CmsMarketCalibrationCalibrationType.OnPrice => Market.WeightedSpotNpvErrors(smileAndCms.weights),
                _ => Market.WeightedFwdNpvErrors(smileAndCms.weights),
            };
        }

        private void UpdateVolatilityCubeAndCmsMarket(Vector x)
        {
            IReadOnlyList<Period> swapTenors = Market.SwapTenors;
            IReadOnlyList<Period> swapLengths = Market.SwapLengths;
            int nSwapTenors = swapTenors.Count;
            int nSwapLengths = swapLengths.Count;
            XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> cube = Cube;
            double? meanReversion;
            switch (path)
            {
                case CostPath.ConstantBetaFreeReversion:
                case CostPath.ConstantBetaFixedReversion:
                {
                    bool free = path == CostPath.ConstantBetaFreeReversion;
                    if (!(nSwapTenors + (free ? 1 : 0) == x.Size))
                    {
                        throw new NanoQLException(
                            free ? "bad calibration guess nSwapTenors+1 != x.size()" : "bad calibration guess nSwapTenors != x.size()");
                    }

                    for (int i = 0; i < nSwapTenors; ++i)
                    {
                        double beta = BetaTransformDirect(x[i]);
                        cube = cube.RecalibrationStep(
                            Enumerable.Repeat(beta, cube.OptionTenors.Length).ToArray(), swapTenors[i], context);
                    }

                    meanReversion = free ? ReversionTransformDirect(x[nSwapTenors]) : fixedMeanReversion;
                    break;
                }

                case CostPath.BetaTermStructureFreeReversion:
                case CostPath.BetaTermStructureFixedReversion:
                {
                    bool free = path == CostPath.BetaTermStructureFreeReversion;
                    if (!((nSwapLengths * nSwapTenors) + (free ? 1 : 0) == x.Size))
                    {
                        throw new NanoQLException(
                            free
                                ? "bad calibration guess (nSwapLengths*nSwapTenors)+1 != x.size()"
                                : "bad calibration guess (nSwapLengths*nSwapTenors) != x.size()");
                    }

                    for (int i = 0; i < nSwapTenors; ++i)
                    {
                        var beta = new double[nSwapLengths];
                        for (int j = 0; j < nSwapLengths; ++j)
                        {
                            beta[j] = BetaTransformDirect(x[(i * nSwapLengths) + j]);
                        }

                        cube = cube.RecalibrationStep(cube.CubeBeta(swapLengths, beta), swapTenors[i], context);
                    }

                    meanReversion = free ? ReversionTransformDirect(x[nSwapLengths * nSwapTenors]) : fixedMeanReversion;
                    break;
                }

                default:
                {
                    bool free = path == CostPath.ParametricBetaFreeReversion;
                    if (!((NParametricParameters * nSwapTenors) + (free ? 1 : 0) == x.Size))
                    {
                        throw new NanoQLException(
                            free
                                ? "bad calibration guess (nParametricParameters*nSwapTenors)+1 != x.size()"
                                : "bad calibration guess (nParametricParameters*nSwapTenors) != x.size()");
                    }

                    for (int i = 0; i < nSwapTenors; ++i)
                    {
                        double betaInf = BetaTransformDirect(x[BetaInfIndex + (NParametricParameters * i)]);
                        double beta0 = BetaTransformDirect(x[Beta0Index + (NParametricParameters * i)]);
                        double d = x[DecayIndex + (NParametricParameters * i)];
                        double decay = d * d;
                        var beta = new double[nSwapLengths];
                        for (int j = 0; j < beta.Length; ++j)
                        {
                            double t = smileAndCms.volCube.TimeFromReference(
                                smileAndCms.volCube.OptionDateFromTenor(swapLengths[j]));
                            beta[j] = betaInf + ((beta0 - betaInf) * Math.Exp(-decay * t));
                        }

                        cube = cube.RecalibrationStep(cube.CubeBeta(swapLengths, beta), swapTenors[i], context);
                    }

                    meanReversion = free ? ReversionTransformDirect(x[NParametricParameters * nSwapTenors]) : fixedMeanReversion;
                    break;
                }
            }

            // a fixed reversion as given, where QuantLib's passes it through the transform's round trip (CMR5 (xxvi))
            Cube = cube;
            Market = Market.Reprice(cube, meanReversion, context);
        }
    }
}
