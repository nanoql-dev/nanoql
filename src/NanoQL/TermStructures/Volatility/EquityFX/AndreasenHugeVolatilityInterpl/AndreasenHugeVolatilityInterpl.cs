// SPDX-FileCopyrightText: Copyright (C) 2017, 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/andreasenhugevolatilityinterpl.hpp ql/termstructures/volatility/equityfx/andreasenhugevolatilityinterpl.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// Andreasen and Huge's calibration of a local volatility surface to a sparse grid of European options: at each expiry
/// a piecewise volatility in log strike, fitted so that one implicit step of the forward equation from the previous
/// expiry's prices reprices the expiry's options; prices and local volatilities between expiries by partial steps.
/// Andreasen J., Huge B., 2010. Volatility Interpolation, https://ssrn.com/abstract=1694972.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, calibrated at construction (the playbook's <c>LazyObject</c> row), where QuantLib calibrates on first
/// use. Its local-volatility and price slices at a time are memoised by that time, as QuantLib caches them, in
/// thread-safe maps inside the object (a named departure from the stateless rule): each slice is computed from the
/// calibration by QuantLib's arithmetic, so its bits are the same whichever thread computes it first, and every query
/// returns what a fresh object returns. Without them an engine reading the surface at every grid point and step would
/// recompute a slice per read, about 130 times QuantLib's cost.
/// </para>
/// <para>
/// The call-put calibration's error picks a put's error for a strike below the forward and a call's above it, where
/// QuantLib compares the forward with the j-th grid point rather than the j-th strike, and so always takes the puts'
/// (<c>andreasenhugevolatilityinterpl.cpp:418</c>). The dividend curve is read at the instants the risk-free curve's
/// times denote, on its own clock, where QuantLib reads it at those times (the mismatched-dates class); curves that
/// share a clock keep QuantLib's bits. Documented divergences.
/// </para>
/// </remarks>
public sealed class AndreasenHugeVolatilityInterpl
{
    private readonly ImmutableArray<(VanillaOption Option, double Volatility)> calibrationSet;
    private readonly double spot;
    private readonly YieldTermStructure rTS;
    private readonly YieldTermStructure qTS;
    private readonly AndreasenHugeVolatilityInterplInterpolationType interpolationType;
    private readonly AndreasenHugeVolatilityInterplCalibrationType calibrationType;
    private readonly int nGridPoints;
    private readonly double? minStrike;
    private readonly double? maxStrike;
    private readonly ImmutableArray<double> strikes;
    private readonly ImmutableArray<Date> expiries;
    private readonly double[] expiryTimes;
    private readonly double[] dT;
    private readonly int?[][] calibrationMatrix;
    private readonly double avgError;
    private readonly double minError;
    private readonly double maxError;
    private readonly FdmMesherComposite mesher;
    private readonly Vector gridPoints;
    private readonly Vector gridInFwd;
    private readonly ImmutableArray<SingleStepCalibrationResult> calibrationResults;

    // The slices by time, computed once from the immutable calibration
    private readonly ConcurrentDictionary<double, Slice> localVolCache = new();
    private readonly ConcurrentDictionary<double, Slice> priceCache = new();

    /// <summary>Initializes a new instance of the <see cref="AndreasenHugeVolatilityInterpl"/> class, calibrated.</summary>
    /// <param name="calibrationSet">The options and their Black volatilities: European, plain vanilla.</param>
    /// <param name="spot">The spot.</param>
    /// <param name="rTS">The risk-free curve, whose times the interpolation's are.</param>
    /// <param name="qTS">The dividend curve.</param>
    /// <param name="interpolationType">The volatilities' interpolation between strikes.</param>
    /// <param name="calibrationType">The options calibrated to.</param>
    /// <param name="nGridPoints">The number of the log-strike grid's points, above 2.</param>
    /// <param name="minStrike">The grid's lowest strike; <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>,
    /// for an eighth of the lowest calibration strike.</param>
    /// <param name="maxStrike">The grid's highest strike; <see langword="null"/> for eight times the highest.</param>
    /// <param name="optimizationMethod">The optimiser; <see langword="null"/> for QuantLib's Levenberg-Marquardt.</param>
    /// <param name="endCriteria">The optimiser's end criteria; <see langword="null"/> for QuantLib's (500, 100, 1e-12,
    /// 1e-10, 1e-10).</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// The grid has at most 2 points or the set is empty, an option isn't European or plain vanilla, or the highest
    /// strike isn't above the lowest; with QuantLib's messages.
    /// </exception>
    /// <exception cref="NanoQLException">A curve can't give its discount at an expiry, or the optimiser fails.</exception>
    public AndreasenHugeVolatilityInterpl(
        IEnumerable<(VanillaOption Option, double Volatility)> calibrationSet,
        double spot,
        YieldTermStructure rTS,
        YieldTermStructure qTS,
        AndreasenHugeVolatilityInterplInterpolationType interpolationType = AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
        AndreasenHugeVolatilityInterplCalibrationType calibrationType = AndreasenHugeVolatilityInterplCalibrationType.Call,
        int nGridPoints = 500,
        double? minStrike = null,
        double? maxStrike = null,
        OptimizationMethod? optimizationMethod = null,
        EndCriteria? endCriteria = null)
    {
        ArgumentNullException.ThrowIfNull(calibrationSet);
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentNullException.ThrowIfNull(qTS);
        if (interpolationType is < AndreasenHugeVolatilityInterplInterpolationType.PiecewiseConstant
            or > AndreasenHugeVolatilityInterplInterpolationType.CubicSpline)
        {
            throw new ArgumentOutOfRangeException(nameof(interpolationType), interpolationType, "unknown interpolation type");
        }

        if (calibrationType is < AndreasenHugeVolatilityInterplCalibrationType.Put
            or > AndreasenHugeVolatilityInterplCalibrationType.Call)
        {
            throw new ArgumentOutOfRangeException(nameof(calibrationType), calibrationType, "unknown calibration type");
        }

        (VanillaOption Option, double Volatility)[] set = [.. calibrationSet];
        if (!(nGridPoints > 2 && set.Length != 0))
        {
            throw new ArgumentException("undefined grid or calibration set", nameof(calibrationSet));
        }

        this.spot = spot;
        this.rTS = rTS;
        this.qTS = qTS;
        this.interpolationType = interpolationType;
        this.calibrationType = calibrationType;
        this.nGridPoints = nGridPoints;
        this.minStrike = minStrike;
        this.maxStrike = maxStrike;
        OptimizationMethod method = optimizationMethod ?? new LevenbergMarquardt();
        EndCriteria criteria = endCriteria ?? new EndCriteria(500, 100, 1e-12, 1e-10, 1e-10);

        var strikeSet = new SortedSet<double>();
        var expirySet = new SortedSet<Date>();
        var options = ImmutableArray.CreateBuilder<(VanillaOption Option, double Volatility)>(set.Length);
        foreach ((VanillaOption option, double volatility) in set)
        {
            ArgumentNullException.ThrowIfNull(option, nameof(calibrationSet));
            Exercise exercise = option.Exercise;
            if (!(exercise.Type == ExerciseType.European))
            {
                throw new ArgumentException("European option required", nameof(calibrationSet));
            }

            Date expiry = exercise.LastDate();
            expirySet.Add(expiry);

            if (option.Payoff is not PlainVanillaPayoff payoff)
            {
                throw new ArgumentException("plain vanilla payoff required", nameof(calibrationSet));
            }

            strikeSet.Add(payoff.Strike);
            options.Add((new VanillaOption(payoff, exercise), volatility));
        }

        this.calibrationSet = options.MoveToImmutable();
        strikes = [.. strikeSet];
        expiries = [.. expirySet];

        dT = new double[expiries.Length];
        expiryTimes = new double[expiries.Length];

        calibrationMatrix = new int?[expiries.Length][];
        for (int l = 0; l < expiries.Length; ++l)
        {
            calibrationMatrix[l] = new int?[strikes.Length];
        }

        for (int i = 0; i < set.Length; ++i)
        {
            Date expiry = set[i].Option.Exercise.LastDate();
            int l = expiries.IndexOf(expiry);
            double strike = ((PlainVanillaPayoff)set[i].Option.Payoff!).Strike;
            int k = 0;
            while (k < strikes.Length && !ComparisonFunctions.CloseEnough(strikes[k], strike))
            {
                ++k;
            }

            calibrationMatrix[l][k] = i;
        }

        // QuantLib's performCalculations, at construction
        if (!(MaxStrike() > MinStrike()))
        {
            throw new ArgumentException("max strike must be greater than min strike", nameof(maxStrike));
        }

        DayCounter dc = rTS.DayCounter;
        for (int i = 0; i < expiryTimes.Length; ++i)
        {
            expiryTimes[i] = dc.YearFraction(rTS.ReferenceDate, expiries[i]);
            dT[i] = expiryTimes[i] - ((i == 0) ? 0.0 : expiryTimes[i - 1]);
        }

        var grid = new Concentrating1dMesher(Math.Log(MinStrike() / spot), Math.Log(MaxStrike() / spot), nGridPoints, (0.0, 0.025));
        mesher = new FdmMesherComposite(grid);

        gridPoints = new Vector(grid.Locations().AsSpan());
        gridInFwd = ArrayFunctions.Exp(gridPoints) * spot;

        double avg = 0.0, min = double.MaxValue, max = 0.0;

        var results = ImmutableArray.CreateBuilder<SingleStepCalibrationResult>(expiries.Length);

        var npvPuts = new Vector(nGridPoints);
        var npvCalls = new Vector(nGridPoints);

        for (int i = 0; i < nGridPoints; ++i)
        {
            double strike = Math.Exp(gridPoints[i]);
            npvPuts[i] = new PlainVanillaPayoff(OptionType.Put, strike).Evaluate(1.0);
            npvCalls[i] = new PlainVanillaPayoff(OptionType.Call, strike).Evaluate(1.0);
        }

        for (int i = 0; i < expiries.Length; ++i)
        {
            AndreasenHugeCostFunction? putCostFct = BuildCostFunction(i, OptionType.Put, npvPuts);
            AndreasenHugeCostFunction? callCostFct = BuildCostFunction(i, OptionType.Call, npvCalls);

            var costFunction = new CombinedCostFunction(putCostFct, callCostFct);

            var positiveConstraint = new PositiveConstraint();
            var problem = new Problem(costFunction, positiveConstraint, costFunction.InitialValues().AsSpan());

            ProblemResults result = method.Minimize(problem, criteria);

            var sig = new Vector(result.CurrentValue.AsSpan());

            results.Add(new SingleStepCalibrationResult(
                npvPuts,
                npvCalls,
                sig,
                (calibrationType == AndreasenHugeVolatilityInterplCalibrationType.Call) ? callCostFct! : putCostFct!));

            Vector vegaDiffs;
            switch (calibrationType)
            {
                case AndreasenHugeVolatilityInterplCalibrationType.CallPut:
                {
                    Vector vegaPutDiffs = putCostFct!.VegaCalibrationError(sig);
                    Vector vegaCallDiffs = callCostFct!.VegaCalibrationError(sig);

                    double fwd = spot * qTS.DiscountAt(rTS, expiryTimes[i]) / rTS.Discount(expiryTimes[i]);

                    // by strike against the forward
                    ImmutableArray<double> expiryStrikes = CalibratedStrikes(i);
                    vegaDiffs = new Vector(sig.Size);
                    for (int j = 0; j < vegaDiffs.Size; ++j)
                    {
                        vegaDiffs[j] = Math.Abs((fwd > expiryStrikes[j]) ? vegaPutDiffs[j] : vegaCallDiffs[j]);
                    }
                }

                break;
                case AndreasenHugeVolatilityInterplCalibrationType.Put:
                    vegaDiffs = ArrayFunctions.Abs(putCostFct!.VegaCalibrationError(sig));
                    break;
                default:
                    vegaDiffs = ArrayFunctions.Abs(callCostFct!.VegaCalibrationError(sig));
                    break;
            }

            double sum = 0.0;
            double lowest = double.MaxValue, highest = double.MinValue;
            for (int j = 0; j < vegaDiffs.Size; ++j)
            {
                sum += vegaDiffs[j];
                lowest = Math.Min(lowest, vegaDiffs[j]);
                highest = Math.Max(highest, vegaDiffs[j]);
            }

            avg += sum;
            min = Math.Min(min, lowest);
            max = Math.Max(max, highest);

            if (putCostFct is not null)
            {
                npvPuts = putCostFct.SolveFor(dT[i], sig, npvPuts);
            }

            if (callCostFct is not null)
            {
                npvCalls = callCostFct.SolveFor(dT[i], sig, npvCalls);
            }
        }

        avg /= this.calibrationSet.Length;
        calibrationResults = results.MoveToImmutable();
        avgError = avg;
        minError = min;
        maxError = max;
    }

    /// <summary>Gets the risk-free curve, whose times the interpolation's are.</summary>
    public YieldTermStructure RiskFreeRate => rTS;

    /// <summary>Gets the last expiry.</summary>
    public Date MaxDate => expiries[^1];

    /// <summary>The grid's lowest strike.</summary>
    /// <returns>The strike given, or an eighth of the lowest calibration strike.</returns>
    public double MinStrike() => minStrike ?? (1 / 8.0 * strikes[0]);

    /// <summary>The grid's highest strike.</summary>
    /// <returns>The strike given, or eight times the highest calibration strike.</returns>
    public double MaxStrike() => maxStrike ?? (8.0 * strikes[^1]);

    /// <summary>The forward at a time on the risk-free curve's clock, the dividend curve read at its instant.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The forward.</returns>
    /// <exception cref="NanoQLException">A curve can't give its discount there.</exception>
    public double Fwd(double t) => spot * qTS.DiscountAt(rTS, t) / rTS.Discount(t);

    /// <summary>The calibration's error in volatility units: QuantLib's <c>calibrationError()</c>.</summary>
    /// <returns>The smallest, the largest and the average error.</returns>
    public AndreasenHugeCalibrationError CalibrationError() => new(minError, maxError, avgError);

    /// <summary>The option price at a time and strike, of the calibration's type (a call for the call-put calibration),
    /// the other type by put-call parity.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="optionType">The option type.</param>
    /// <returns>The price.</returns>
    /// <exception cref="NanoQLException">A curve can't give its discount there.</exception>
    public double OptionPrice(double t, double strike, OptionType optionType)
    {
        double df = rTS.Discount(t);

        Slice f = priceCache.GetOrAdd(t, PriceSlice);
        double fwd = f.Fwd;

        double price = CacheValue(strike, f);

        if (optionType == OptionType.Put
            && calibrationType is AndreasenHugeVolatilityInterplCalibrationType.Call or AndreasenHugeVolatilityInterplCalibrationType.CallPut)
        {
            price = price + (strike / fwd) - 1.0;
        }
        else if (optionType == OptionType.Call && calibrationType == AndreasenHugeVolatilityInterplCalibrationType.Put)
        {
            price = 1.0 - (strike / fwd) + price;
        }

        return price * df * fwd;
    }

    /// <summary>The local volatility at a time and strike.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The local volatility.</returns>
    /// <exception cref="NanoQLException">A curve can't give its discount there.</exception>
    public double LocalVol(double t, double strike) => CacheValue(strike, localVolCache.GetOrAdd(t, LocalVolSlice));

    // the strikes of an expiry's calibrated options, in order
    private ImmutableArray<double> CalibratedStrikes(int iExpiry)
    {
        var ks = ImmutableArray.CreateBuilder<double>();
        for (int j = 0; j < strikes.Length; ++j)
        {
            if (calibrationMatrix[iExpiry][j] is not null)
            {
                ks.Add(strikes[j]);
            }
        }

        return ks.ToImmutable();
    }

    private AndreasenHugeCostFunction? BuildCostFunction(int iExpiry, OptionType optionType, Vector previousNPVs)
    {
        if (calibrationType != AndreasenHugeVolatilityInterplCalibrationType.CallPut
            && ((calibrationType == AndreasenHugeVolatilityInterplCalibrationType.Call && optionType == OptionType.Put)
                || (calibrationType == AndreasenHugeVolatilityInterplCalibrationType.Put && optionType == OptionType.Call)))
        {
            return null;
        }

        double expiryTime = expiryTimes[iExpiry];

        double discount = rTS.Discount(expiryTime);
        double fwd = spot * qTS.DiscountAt(rTS, expiryTime) / discount;

        int nOptions = calibrationMatrix[iExpiry].Count(n => n is not null);

        var lnMarketStrikes = new double[nOptions];
        var marketNPVs = new Vector(nOptions);
        var marketVegas = new Vector(nOptions);

        // calculate undiscounted market prices
        for (int j = 0, k = 0; j < strikes.Length; ++j)
        {
            if (calibrationMatrix[iExpiry][j] is int idx)
            {
                double vol = calibrationSet[idx].Volatility;
                double stdDev = vol * Math.Sqrt(expiryTime);

                var calculator = new BlackCalculator(optionType, strikes[j], fwd, stdDev, discount);

                double npv = calculator.Value;
                double vega = calculator.Vega(expiryTime);

                marketNPVs[k] = npv / (discount * fwd);
                marketVegas[k] = vega / (discount * fwd);
                lnMarketStrikes[k++] = Math.Log(strikes[j] / fwd);
            }
        }

        return new AndreasenHugeCostFunction(
            marketNPVs, marketVegas, lnMarketStrikes, previousNPVs, mesher, gridPoints, dT[iExpiry], interpolationType);
    }

    private int ExerciseTimeIdx(double t)
    {
        // std::upper_bound
        int lo = 0, hi = expiryTimes.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (expiryTimes[mid] > t)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return Math.Min(expiryTimes.Length - 1, lo);
    }

    private double CacheValue(double strike, Slice f)
    {
        double fwd = f.Fwd;
        double k = Math.Log(strike / fwd);

        double s = Math.Max(gridPoints[1], Math.Min(gridPoints[gridPoints.Size - 2], k));

        return f.Interpolation.Evaluate(s);
    }

    private Vector PriceSliceValues(double t, OptionType optionType)
    {
        int iu = ExerciseTimeIdx(t);
        SingleStepCalibrationResult r = calibrationResults[iu];

        return r.CostFunction.SolveFor((iu == 0) ? t : t - expiryTimes[iu - 1], r.Sigmas, (optionType == OptionType.Call) ? r.CallNPVs : r.PutNPVs);
    }

    private Slice PriceSlice(double t)
    {
        Vector prices = calibrationType == AndreasenHugeVolatilityInterplCalibrationType.Put
            ? PriceSliceValues(t, OptionType.Put)
            : PriceSliceValues(t, OptionType.Call);

        double fwd = spot * qTS.DiscountAt(rTS, t) / rTS.Discount(t);

        return new Slice(fwd, new CubicNaturalSpline(gridPoints.AsSpan()[1..^1], prices.AsSpan()[1..^1]));
    }

    private Vector LocalVolSliceValues(double t, OptionType optionType)
    {
        int iu = ExerciseTimeIdx(t);
        SingleStepCalibrationResult r = calibrationResults[iu];

        Vector previousNPVs = (optionType == OptionType.Call) ? r.CallNPVs : r.PutNPVs;

        AndreasenHugeCostFunction costFunction = r.CostFunction;

        double dt = (iu == 0) ? t : t - expiryTimes[iu - 1];
        Vector sig = r.Sigmas;

        Vector cAtJ = costFunction.SolveFor(dt, sig, previousNPVs);

        Vector dCdT = costFunction.SolveFor(dt, sig, costFunction.Apply(sig, costFunction.SolveFor(dt, sig, previousNPVs)));

        Vector d2CdK2 = costFunction.D2CdK2(cAtJ);

        Vector localVol = ArrayFunctions.Sqrt(2 * dCdT / d2CdK2);

        for (int i = 1; i < localVol.Size - 1; ++i)
        {
            if (!double.IsFinite(localVol[i]) || localVol[i] < 0.0)
            {
                localVol[i] = 0.25;
            }
        }

        return localVol;
    }

    private Slice LocalVolSlice(double t)
    {
        Vector localVol;
        switch (calibrationType)
        {
            case AndreasenHugeVolatilityInterplCalibrationType.CallPut:
            {
                Vector putLocalVol = LocalVolSliceValues(t, OptionType.Put);
                Vector callLocalVol = LocalVolSliceValues(t, OptionType.Call);

                localVol = new Vector(gridPoints.Size);
                for (int i = 0, n = localVol.Size; i < n; ++i)
                {
                    localVol[i] = (gridPoints[i] > 0.0) ? callLocalVol[i] : putLocalVol[i];
                }
            }

            break;
            case AndreasenHugeVolatilityInterplCalibrationType.Put:
                localVol = LocalVolSliceValues(t, OptionType.Put);
                break;
            default:
                localVol = LocalVolSliceValues(t, OptionType.Call);
                break;
        }

        double fwd = spot * qTS.DiscountAt(rTS, t) / rTS.Discount(t);

        return new Slice(fwd, new LinearInterpolation(gridPoints.AsSpan()[1..^1], localVol.AsSpan()[1..^1]));
    }

    // a slice at a time: the forward, and the values' interpolation in log strike over the grid's inner points
    private sealed record Slice(double Fwd, Interpolation Interpolation);

    private sealed record SingleStepCalibrationResult(
        Vector PutNPVs, Vector CallNPVs, Vector Sigmas, AndreasenHugeCostFunction CostFunction);

    /// <summary>
    /// QuantLib's <c>AndreasenHugeCostFunction</c>: one implicit step of the forward equation over an expiry's interval
    /// for a volatility at each calibration strike, and its prices' differences from the market's.
    /// </summary>
    /// <remarks>
    /// Immutable: each step builds its operator, where QuantLib's mutates a member, so that the slices can be computed on
    /// any thread; the arithmetic is QuantLib's.
    /// </remarks>
    private sealed class AndreasenHugeCostFunction : CostFunction
    {
        private readonly Vector marketNPVs;
        private readonly Vector marketVegas;
        private readonly double[] lnMarketStrikes;
        private readonly Vector previousNPVs;
        private readonly FdmMesherComposite mesher;
        private readonly Vector gridPoints;
        private readonly double dT;
        private readonly AndreasenHugeVolatilityInterplInterpolationType interpolationType;
        private readonly FirstDerivativeOp dxMap;
        private readonly TripleBandLinearOp dxxMap;
        private readonly TripleBandLinearOp d2CdK2;

        public AndreasenHugeCostFunction(
            Vector marketNPVs,
            Vector marketVegas,
            double[] lnMarketStrikes,
            Vector previousNPVs,
            FdmMesherComposite mesher,
            Vector gridPoints,
            double dT,
            AndreasenHugeVolatilityInterplInterpolationType interpolationType)
        {
            this.marketNPVs = marketNPVs;
            this.marketVegas = marketVegas;
            this.lnMarketStrikes = lnMarketStrikes;
            this.previousNPVs = previousNPVs;
            this.mesher = mesher;
            this.gridPoints = gridPoints;
            this.dT = dT;
            this.interpolationType = (lnMarketStrikes.Length > 1)
                ? interpolationType
                : AndreasenHugeVolatilityInterplInterpolationType.PiecewiseConstant;
            dxMap = new FirstDerivativeOp(0, mesher);
            dxxMap = new SecondDerivativeOp(0, mesher);
            d2CdK2 = dxMap.Mult(new Vector(mesher.Layout.Size, -1.0)).Add(dxxMap);
        }

        public Vector D2CdK2(Vector c) => d2CdK2.Apply(c);

        public Vector SolveFor(double dT, Vector sig, Vector b) => MapT(sig).Mult(new Vector(mesher.Layout.Size, dT)).SolveSplitting(b, 1.0);

        // QuantLib's apply, on the operator its last solveFor set, here built for the same volatilities
        public Vector Apply(Vector sig, Vector c) => -MapT(sig).Apply(c);

        public override Vector Values(Vector x)
        {
            Vector newNPVs = SolveFor(dT, x, previousNPVs);

            var interpl = new MonotonicCubicNaturalSpline(gridPoints.AsSpan(), newNPVs.AsSpan());

            var retVal = new Vector(lnMarketStrikes.Length);
            for (int i = 0; i < retVal.Size; ++i)
            {
                double strike = lnMarketStrikes[i];
                retVal[i] = interpl.Evaluate(strike) - marketNPVs[i];
            }

            return retVal;
        }

        public Vector VegaCalibrationError(Vector sig) => Values(sig) / marketVegas;

        public Vector InitialValues() => new(lnMarketStrikes.Length, 0.25);

        // the generator ½σ²(∂ₓ − ∂ₓₓ) at the volatilities, QuantLib's mapT_ after its axpyb
        private TripleBandLinearOp MapT(Vector sig)
        {
            var x = new double[lnMarketStrikes.Length];
            Interpolation sigInterpl;

            switch (interpolationType)
            {
                case AndreasenHugeVolatilityInterplInterpolationType.CubicSpline:
                    sigInterpl = new CubicNaturalSpline(lnMarketStrikes, sig.AsSpan());
                    break;
                case AndreasenHugeVolatilityInterplInterpolationType.Linear:
                    sigInterpl = new LinearInterpolation(lnMarketStrikes, sig.AsSpan());
                    break;
                default:
                    for (int i = 0; i < x.Length - 1; ++i)
                    {
                        x[i] = 0.5 * (lnMarketStrikes[i] + lnMarketStrikes[i + 1]);
                    }

                    x[^1] = lnMarketStrikes[^1];

                    sigInterpl = new BackwardFlatInterpolation(x, sig.AsSpan());
                    break;
            }

            var z = new Vector(mesher.Layout.Size);
            for (int i = 0; i < z.Size; ++i)
            {
                double lnStrike = gridPoints[i];

                double vol = sigInterpl.Evaluate(Math.Min(Math.Max(lnStrike, lnMarketStrikes[0]), lnMarketStrikes[^1]), true);

                z[i] = 0.5 * vol * vol;
            }

            var mapT = new TripleBandLinearOp(0, mesher);
            mapT.Axpyb(z, dxMap, dxxMap.Mult(-z), new Vector());
            return mapT;
        }
    }

    /// <summary>QuantLib's <c>CombinedCostFunction</c>: the put and call cost functions' values together.</summary>
    private sealed class CombinedCostFunction : CostFunction
    {
        private readonly AndreasenHugeCostFunction? putCostFct;
        private readonly AndreasenHugeCostFunction? callCostFct;

        public CombinedCostFunction(AndreasenHugeCostFunction? putCostFct, AndreasenHugeCostFunction? callCostFct)
        {
            this.putCostFct = putCostFct;
            this.callCostFct = callCostFct;
        }

        public override Vector Values(Vector x)
        {
            if (putCostFct is not null && callCostFct is not null)
            {
                Vector pv = putCostFct.Values(x);
                Vector cv = callCostFct.Values(x);

                var retVal = new Vector(pv.Size + cv.Size);
                pv.AsSpan().CopyTo(retVal.AsSpan());
                cv.AsSpan().CopyTo(retVal.AsSpan()[cv.Size..]);

                return retVal;
            }
            else if (putCostFct is not null)
            {
                return putCostFct.Values(x);
            }
            else if (callCostFct is not null)
            {
                return callCostFct.Values(x);
            }

            throw new NanoQLException("internal error: cost function not set");
        }

        public Vector InitialValues()
        {
            if (putCostFct is not null && callCostFct is not null)
            {
                return 0.5 * (putCostFct.InitialValues() + callCostFct.InitialValues());
            }
            else if (putCostFct is not null)
            {
                return putCostFct.InitialValues();
            }
            else if (callCostFct is not null)
            {
                return callCostFct.InitialValues();
            }

            throw new NanoQLException("internal error: cost function not set");
        }
    }
}
