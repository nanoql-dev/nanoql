// SPDX-FileCopyrightText: Copyright (C) 2013, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/markovfunctional.hpp ql/models/shortrate/onefactormodels/markovfunctional.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.Models.MarkovFunctionalModelSettingsAdjustments;

namespace NanoQL.Models;

/// <summary>
/// The one-factor Markov-functional model, its numeraire calibrated to a swaption or a caplet smile: see
/// http://ssrn.com/abstract_id=2183721 and http://quantlib.org/slides/qlws13/caspers.pdf.
/// </summary>
/// <remarks>
/// <para>
/// The model needs an input smile that is arbitrage free, smooth (at least a C¹ call price function) and whose call
/// prices fall fast enough in the strike. Kahale's method, or a SABR smile made arbitrage free by it, may pretreat the
/// smile; either implies zero density for rates below minus the shift, so the curve's at-the-money forwards must lie
/// above it. Without a pretreatment the smile must be arbitrage free and cover the strikes from the lower to the upper
/// rate bound. The calibration assumes one curve, the model's, throughout; a swap's floating leg is
/// P(t, T₀) − P(t, T₁), and the calibration instruments start at their fixing dates. A shifted smile's lower rate bound
/// moves by the shift. A custom smile inverts the digitals itself.
/// </para>
/// <para>
/// Immutable, a <see cref="Gaussian1dModel"/> implementing <see cref="ICalibratedModel"/> over its volatilities: it
/// calibrates its numeraire when it is built, under the context it is given (the playbook's <c>LazyObject</c> row),
/// where QuantLib's calibrates at its first use under the evaluation date then. <see cref="WithParams"/> keeps the
/// smiles and re-tabulates the numeraire, QuantLib's <c>generateArguments</c>; <see cref="ForceArbitrageIndices"/>
/// returns a copy calibrated again; <see cref="ModelOutputs"/> computes the diagnostics per call. Its first volatility
/// is fixed by default in a calibration (<see cref="DefaultFixParameters"/>), as QuantLib's <c>calibrate</c> fixes it:
/// the prices don't depend on the volatilities' level.
/// </para>
/// <para>
/// Six of QuantLib's defects are fixed. A caplet calibration point's end date, and the outputs' forward rates' and
/// caplets', are the index's maturity date for the value date, as QuantLib's master computes the Gaussian1d forward
/// rate after the pin (#2848), where QuantLib advances the value date by the tenor itself
/// (<c>markovfunctional.cpp:278</c>, <c>:949</c>, <c>:1109</c>). With flat payoff extrapolation, the right tails
/// integrate the last node's value, where QuantLib's integrate the second-last's (<c>:516-519</c>, <c>:1064-1066</c>,
/// <c>:1141-1143</c>). A curve given to the numeraire is read at the numeraire time with extrapolation, as the model's
/// own is (<c>:775-785</c>), and a curve on another clock at the instant QuantLib's argument denotes, by the date a
/// time comes from; and the model's own curve, which the ratio with a given curve reads, with extrapolation, as the
/// calibration reads it (<c>:783-785</c>, <c>:797-799</c>), so a model whose calibration extrapolates its curve prices
/// with a given curve too. The settings refuse any two smile pretreatments (<see
/// cref="MarkovFunctionalModelSettings"/>), and the state process is its equation's (<see cref="MfStateProcess"/>), its
/// variance continuous in a small reversion.
/// </para>
/// <para>
/// From QuantLib's master after the pin (#2912, <c>Divergences/MarkovFunctionalUpstreamFixesTests.cs</c>): the upper
/// rate bound's digital is struck at the bound, as the rest of the model reads it, where QuantLib 1.44 subtracts the
/// smile's shift (<c>:432-436</c>), so a shifted smile capped its rates below the bound; and the settings ask for two
/// grid points, the master's minimum.
/// </para>
/// </remarks>
public sealed class MarkovFunctional : Gaussian1dModel, ICalibratedModel
{
    private readonly MarkovFunctionalModelSettings modelSettings;
    private readonly bool capletCalibrated;
    private readonly double reversion;
    private readonly ImmutableArray<Date> volstepdates;
    private readonly ImmutableArray<double> volsteptimes;
    private readonly ImmutableArray<double> volatilities;
    private readonly ImmutableArray<Parameter> arguments;
    private readonly MfStateProcess process;
    private readonly SwaptionVolatilityStructure? swaptionVol;
    private readonly OptionletVolatilityStructure? capletVol;
    private readonly SwapIndex? swapIndexBase;
    private readonly IborIndex iborIndex;
    private readonly Date numeraireDate;
    private readonly double numeraireTime;
    private readonly ImmutableArray<CalibrationPoint> calibrationPoints;
    private readonly ImmutableArray<double> times;
    private readonly Vector y;
    private readonly ImmutableArray<double> normalIntegralX;
    private readonly ImmutableArray<double> normalIntegralW;
    private readonly ImmutableArray<(int Left, int Right)> forcedArbitrageIndices;
    private readonly ImmutableArray<string> setupMessages;
    private readonly Smiles smiles;
    private readonly Tabulation tabulation;

    /// <summary>Initializes a new instance of the <see cref="MarkovFunctional"/> class, calibrated to a swaption
    /// smile.</summary>
    /// <param name="termStructure">The model's curve.</param>
    /// <param name="reversion">The constant reversion.</param>
    /// <param name="volstepdates">The volatilities' step dates, increasing, after the curve's reference date.</param>
    /// <param name="volatilities">The volatilities: one more than the step dates.</param>
    /// <param name="swaptionVol">The swaption volatilities.</param>
    /// <param name="swaptionExpiries">The calibration swaptions' expiries, distinct.</param>
    /// <param name="swaptionTenors">The calibration swaptions' tenors, one per expiry.</param>
    /// <param name="swapIndexBase">The swap index whose swaps the swaptions exercise into.</param>
    /// <param name="context">The evaluation context the calibration runs under.</param>
    /// <param name="modelSettings">The numerical settings; <see langword="null"/> for QuantLib's defaults.</param>
    /// <exception cref="ArgumentNullException">An argument but the settings is null.</exception>
    /// <exception cref="ArgumentException">The expiries and tenors differ in number or are none, an expiry occurs more
    /// than once, a step date isn't after the reference date or after the date before it, or the volatilities aren't
    /// one more than the step dates.</exception>
    /// <exception cref="NanoQLException">The settings are refused, or the calibration fails.</exception>
    public MarkovFunctional(
        YieldTermStructure termStructure,
        double reversion,
        IEnumerable<Date> volstepdates,
        IEnumerable<double> volatilities,
        SwaptionVolatilityStructure swaptionVol,
        IEnumerable<Date> swaptionExpiries,
        IEnumerable<Period> swaptionTenors,
        SwapIndex swapIndexBase,
        EvaluationContext context,
        MarkovFunctionalModelSettings? modelSettings = null)
        : this(
            Checked(termStructure),
            reversion,
            Copied(volstepdates),
            Copied(volatilities),
            CheckedWith(swaptionVol, "swaption volatility structure is empty"),
            null,
            SwaptionPoints(Copied(swaptionExpiries), Copied(swaptionTenors)),
            Copied(swaptionTenors),
            Checked(swapIndexBase),
            swapIndexBase.IborIndex,
            false,
            Checked(context),
            modelSettings ?? new MarkovFunctionalModelSettings())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MarkovFunctional"/> class, calibrated to a caplet
    /// smile.</summary>
    /// <param name="termStructure">The model's curve.</param>
    /// <param name="reversion">The constant reversion.</param>
    /// <param name="volstepdates">The volatilities' step dates, increasing, after the curve's reference date.</param>
    /// <param name="volatilities">The volatilities: one more than the step dates.</param>
    /// <param name="capletVol">The caplet volatilities.</param>
    /// <param name="capletExpiries">The calibration caplets' expiries, distinct.</param>
    /// <param name="iborIndex">The index the caplets fix on.</param>
    /// <param name="context">The evaluation context the calibration runs under.</param>
    /// <param name="modelSettings">The numerical settings; <see langword="null"/> for QuantLib's defaults.</param>
    /// <exception cref="ArgumentNullException">An argument but the settings is null.</exception>
    /// <exception cref="ArgumentException">The expiries are none or an expiry occurs more than once, a step date isn't
    /// after the reference date or after the date before it, or the volatilities aren't one more than the step
    /// dates.</exception>
    /// <exception cref="NanoQLException">The settings are refused, or the calibration fails.</exception>
    public MarkovFunctional(
        YieldTermStructure termStructure,
        double reversion,
        IEnumerable<Date> volstepdates,
        IEnumerable<double> volatilities,
        OptionletVolatilityStructure capletVol,
        IEnumerable<Date> capletExpiries,
        IborIndex iborIndex,
        EvaluationContext context,
        MarkovFunctionalModelSettings? modelSettings = null)
        : this(
            Checked(termStructure),
            reversion,
            Copied(volstepdates),
            Copied(volatilities),
            null,
            CheckedWith(capletVol, "caplet volatility structure is empty"),
            CapletPoints(Copied(capletExpiries)),
            [],
            null,
            Checked(iborIndex),
            true,
            Checked(context),
            modelSettings ?? new MarkovFunctionalModelSettings())
    {
    }

    // QuantLib's constructor body and initialize(), then its first performCalculations().
    private MarkovFunctional(
        YieldTermStructure termStructure,
        double reversion,
        ImmutableArray<Date> volstepdates,
        ImmutableArray<double> volatilities,
        SwaptionVolatilityStructure? swaptionVol,
        OptionletVolatilityStructure? capletVol,
        ImmutableArray<Date> expiries,
        ImmutableArray<Period> swaptionTenors,
        SwapIndex? swapIndexBase,
        IborIndex iborIndex,
        bool capletCalibrated,
        EvaluationContext context,
        MarkovFunctionalModelSettings settings)
        : base(termStructure)
    {
        this.reversion = reversion;
        this.volstepdates = volstepdates;
        this.volatilities = volatilities;
        this.swaptionVol = swaptionVol;
        this.capletVol = capletVol;
        this.swapIndexBase = swapIndexBase;
        this.iborIndex = iborIndex;
        this.capletCalibrated = capletCalibrated;
        modelSettings = settings.Validate();
        forcedArbitrageIndices = [];

        var messages = new List<string> { "initializing" };

        var gaussHermite = new GaussHermiteIntegration(modelSettings.GaussHermitePoints);
        Vector x = gaussHermite.X(), w = gaussHermite.Weights();
        double[] nx = new double[x.Size], nw = new double[x.Size];
        for (int i = 0; i < x.Size; i++)
        {
            nw[i] = w[i] * (Math.Exp(-x[i] * x[i]) * MathConstants.M_1_SQRTPI);
            nx[i] = x[i] * MathConstants.M_SQRT2;
        }

        normalIntegralX = [.. nx];
        normalIntegralW = [.. nw];

        volsteptimes = UpdateTimes1(termStructure, volstepdates);

        var memo = new Gaussian1dMemo();
        var points = new SortedDictionary<Date, CalibrationPoint>();
        for (int i = 0; i < expiries.Length; i++)
        {
            if (capletCalibrated)
            {
                MakeCapletCalibrationPoint(points, expiries[i]);
            }
            else
            {
                MakeSwaptionCalibrationPoint(points, expiries[i], swaptionTenors[i], memo, context);
            }
        }

        numeraireDate = FindNumeraireDate(points, memo, context);
        calibrationPoints = [.. points.Values];

        (numeraireTime, times) = UpdateTimes2();

        Parameter sigma = new PiecewiseConstantParameter(volsteptimes, new PositiveConstraint());
        for (int i = 0; i < sigma.Size; i++)
        {
            sigma = sigma.SetParam(i, volatilities[i]);
        }

        arguments = [sigma];
        process = new MfStateProcess(reversion, volsteptimes, sigma.Params().AsSpan().ToArray());
        SetStateProcess(process);

        y = YGrid(modelSettings.YStdDevs, modelSettings.YGridPoints);

        // the discrete numeraire, 1 everywhere, and its interpolations
        var rows = new double[times.Length][];
        var numeraire = new Interpolation[times.Length];
        for (int i = 0; i < times.Length; i++)
        {
            rows[i] = Enumerable.Repeat(1.0, (2 * modelSettings.YGridPoints) + 1).ToArray();
            numeraire[i] = NumeraireInterpolation(rows[i]);
        }

        // QuantLib's performCalculations(): the times, unchanged, the smiles and the numeraire
        messages.Add("updating times");
        messages.Add("updating smiles");
        setupMessages = [.. messages];
        smiles = UpdateSmiles(context);
        tabulation = UpdateNumeraireTabulation(new Tabulation(rows, numeraire));
    }

    // A copy with other volatilities (WithParams) or other forced arbitrage indices (ForceArbitrageIndices): the
    // smiles kept or computed again, and the numeraire tabulated again from the receiver's.
    private MarkovFunctional(
        MarkovFunctional other,
        ImmutableArray<Parameter> arguments,
        ImmutableArray<(int Left, int Right)> forcedArbitrageIndices,
        EvaluationContext? context)
        : base(other.TermStructure)
    {
        modelSettings = other.modelSettings;
        capletCalibrated = other.capletCalibrated;
        reversion = other.reversion;
        volstepdates = other.volstepdates;
        volsteptimes = other.volsteptimes;
        volatilities = other.volatilities;
        swaptionVol = other.swaptionVol;
        capletVol = other.capletVol;
        swapIndexBase = other.swapIndexBase;
        iborIndex = other.iborIndex;
        numeraireDate = other.numeraireDate;
        numeraireTime = other.numeraireTime;
        calibrationPoints = other.calibrationPoints;
        times = other.times;
        y = other.y;
        normalIntegralX = other.normalIntegralX;
        normalIntegralW = other.normalIntegralW;
        this.arguments = arguments;
        this.forcedArbitrageIndices = forcedArbitrageIndices;
        process = other.process.WithVols([.. arguments[0].Params().AsSpan()]);
        SetStateProcess(process);
        if (context is null)
        {
            setupMessages = other.setupMessages;
            smiles = other.smiles;
        }
        else
        {
            setupMessages = ["initializing", "updating times", "updating smiles"];
            smiles = UpdateSmiles(context);
        }

        tabulation = UpdateNumeraireTabulation(other.tabulation.Copy());
    }

    /// <inheritdoc/>
    public ImmutableArray<Parameter> Arguments => arguments;

    /// <inheritdoc/>
    public Constraint Constraint => new CalibratedModel.PrivateConstraint(arguments);

    /// <summary>Gets the model's settings, as validated.</summary>
    public MarkovFunctionalModelSettings ModelSettings => modelSettings;

    /// <summary>Gets the numeraire's date: the calibration instruments' last payment date.</summary>
    public Date NumeraireDate => numeraireDate;

    /// <summary>Gets the numeraire's time on the model's curve.</summary>
    public double NumeraireTime => numeraireTime;

    /// <summary>Gets the volatilities: QuantLib's <c>volatility()</c>, the volatility argument's parameters.</summary>
    public ImmutableArray<double> Volatility => [.. arguments[0].Params().AsSpan()];

    /// <summary>Gets the calibration flags QuantLib's <c>calibrate</c> uses when none are given: the first volatility
    /// fixed.</summary>
    public ImmutableArray<bool> DefaultFixParameters => FixedFirstVolatility();

    /// <summary>The calibration flags that fix the first volatility, the others free.</summary>
    /// <returns>One flag per volatility, the first set.</returns>
    public ImmutableArray<bool> FixedFirstVolatility()
    {
        bool[] c = new bool[volatilities.Length];
        c[0] = true;
        return [.. c];
    }

    /// <summary>The smile pretreatments' arbitrage-free regions: per calibration point from the last to the first,
    /// skipping those without a pretreatment, the grid indices of Kahale's core.</summary>
    /// <returns>The indices; <see langword="null"/> for a custom smile, QuantLib's pair of <c>Null&lt;Size&gt;()</c>,
    /// which is arbitrage free by assumption.</returns>
    public ImmutableArray<(int Left, int Right)?> ArbitrageIndices() => smiles.ArbitrageIndices;

    /// <summary>
    /// A copy of the model whose smile pretreatments' arbitrage-free regions start and end at the given grid indices,
    /// at the least and the most, calibrated again: useful for sensitivities, where a region moving would move the
    /// prices.
    /// </summary>
    /// <param name="indices">Per calibration point from the last to the first, as <see cref="ArbitrageIndices"/>
    /// orders them, the core's first and last index; empty to compute the regions again.</param>
    /// <param name="context">The evaluation context the calibration runs under.</param>
    /// <returns>The copy; the receiver doesn't change.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">The calibration fails.</exception>
    public MarkovFunctional ForceArbitrageIndices(ReadOnlySpan<(int Left, int Right)> indices, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new MarkovFunctional(this, arguments, [.. indices], context);
    }

    /// <summary>
    /// The model's diagnostics, per calibration point: the yield curve's fit, the smile's fit on the moneyness grid,
    /// the adjustment factors and the calibration's messages.
    /// </summary>
    /// <param name="context">The evaluation context the swaptions' underlying swaps are built under.</param>
    /// <returns>The outputs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <remarks>
    /// Computed per call: QuantLib's are kept in the model behind a dirty flag, and its second computation after a
    /// recalculation appends its at-the-money levels and annuities to the first's.
    /// </remarks>
    public MarkovFunctionalModelOutputs ModelOutputs(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var memo = new Gaussian1dMemo();

        // yield term structure
        var marketZerorate = ImmutableArray.CreateBuilder<double>();
        var modelZerorate = ImmutableArray.CreateBuilder<double>();
        for (int i = 1; i < times.Length - 1; i++)
        {
            marketZerorate.Add(TermStructure.ZeroRate(times[i], Compounding.Continuous, Frequency.Annual).Rate);

            // we need to put a small positive time here since the zerobond
            // implementation optimizes the case t=0.0 then using the
            // initial yts
            modelZerorate.Add(-Math.Log(Zerobond(times[i], 1.0E-10)) / times[i]);
        }

        // volatility surface
        var strikesOut = ImmutableArray.CreateBuilder<ImmutableArray<double>>();
        var marketCallOut = ImmutableArray.CreateBuilder<ImmutableArray<double>>();
        var marketPutOut = ImmutableArray.CreateBuilder<ImmutableArray<double>>();
        var modelCallOut = ImmutableArray.CreateBuilder<ImmutableArray<double>>();
        var modelPutOut = ImmutableArray.CreateBuilder<ImmutableArray<double>>();
        var marketVegaOut = ImmutableArray.CreateBuilder<ImmutableArray<double>>();
        var marketRawCallOut = ImmutableArray.CreateBuilder<ImmutableArray<double>>();
        var marketRawPutOut = ImmutableArray.CreateBuilder<ImmutableArray<double>>();

        for (int p = 0; p < calibrationPoints.Length; p++)
        {
            CalibrationPoint point = calibrationPoints[p];
            SmilePoint smile = smiles.Points[p];
            SmileSection sec = smile.SmileSection;
            SmileSection rawSec = smile.RawSmileSection;
            var ssutils = new SmileSectionUtils(sec, modelSettings.SmileMoneynessCheckpoints.AsSpan(), smile.Atm);
            double shift = sec.Shift;
            ImmutableArray<double> money = ssutils.MoneyGrid();
            var strikes = new List<double>();
            var marketCall = new List<double>();
            var marketPut = new List<double>();
            var modelCall = new List<double>();
            var modelPut = new List<double>();
            var marketVega = new List<double>();
            var marketRawCall = new List<double>();
            var marketRawPut = new List<double>();
            for (int j = 0; j < money.Length; j++)
            {
                strikes.Add(sec.VolatilityType == VolatilityType.Normal ? smile.Atm + money[j] : (money[j] * (smile.Atm + shift)) - shift);
                try
                {
                    marketRawCall.Add(rawSec.OptionPrice(strikes[j], OptionType.Call, smile.Annuity));
                    marketRawPut.Add(rawSec.OptionPrice(strikes[j], OptionType.Put, smile.Annuity));
                }
                catch (NanoQLException)
                {
                    // the smile section might not be able to output an
                    // option price because it has no atm level
                    marketRawCall.Add(0.0);
                    marketRawPut.Add(0.0);
                }

                marketCall.Add(sec.OptionPrice(strikes[j], OptionType.Call, smile.Annuity));
                marketPut.Add(sec.OptionPrice(strikes[j], OptionType.Put, smile.Annuity));
                modelCall.Add(
                    point.IsCaplet
                        ? CapletPriceInternal(OptionType.Call, point.Expiry, strikes[j], null, 0.0, true, null, memo)
                        : SwaptionPriceInternal(OptionType.Call, point.Expiry, point.Tenor, strikes[j], null, 0.0, true, null, memo, context));
                modelPut.Add(
                    point.IsCaplet
                        ? CapletPriceInternal(OptionType.Put, point.Expiry, strikes[j], null, 0.0, true, null, memo)
                        : SwaptionPriceInternal(OptionType.Put, point.Expiry, point.Tenor, strikes[j], null, 0.0, true, null, memo, context));
                marketVega.Add(sec.Vega(strikes[j], smile.Annuity));
            }

            strikesOut.Add([.. strikes]);
            marketCallOut.Add([.. marketCall]);
            marketPutOut.Add([.. marketPut]);
            modelCallOut.Add([.. modelCall]);
            modelPutOut.Add([.. modelPut]);
            marketVegaOut.Add([.. marketVega]);
            marketRawCallOut.Add([.. marketRawCall]);
            marketRawPutOut.Add([.. marketRawPut]);
        }

        return new MarkovFunctionalModelOutputs(
            modelSettings,
            [.. calibrationPoints.Select(point => point.Expiry)],
            [.. calibrationPoints.Select(point => point.Tenor)],
            [.. smiles.Points.Select(smile => smile.Atm)],
            [.. smiles.Points.Select(smile => smile.Annuity)],
            tabulation.AdjustmentFactors,
            tabulation.DigitalsAdjustmentFactors,
            [.. setupMessages, .. tabulation.Messages],
            strikesOut.ToImmutable(),
            marketRawCallOut.ToImmutable(),
            marketRawPutOut.ToImmutable(),
            marketCallOut.ToImmutable(),
            marketPutOut.ToImmutable(),
            modelCallOut.ToImmutable(),
            modelPutOut.ToImmutable(),
            marketVegaOut.ToImmutable(),
            marketZerorate.ToImmutable(),
            modelZerorate.ToImmutable());
    }

    /// <inheritdoc/>
    public Vector Params() => CalibratedModel.FlattenParams(arguments);

    /// <inheritdoc/>
    /// <remarks>
    /// QuantLib's <c>setParams</c> with its <c>generateArguments</c>: the state process takes the new volatilities and
    /// the numeraire is tabulated again, the smiles and the calibration points kept. As QuantLib's, the parameters
    /// aren't checked against the arguments' constraints, and the refusals of a wrong size come before any write.
    /// </remarks>
    public ICalibratedModel WithParams(Vector parameters)
    {
        ImmutableArray<Parameter> newArguments = CalibratedModel.ArgumentsWithParams(arguments, parameters);
        return new MarkovFunctional(this, newArguments, forcedArbitrageIndices, null);
    }

    internal override double NumeraireCore(double t, Date? tDate, double y, YieldTermStructure? yts, Gaussian1dMemo memo)
    {
        if (t == 0)
        {
            return yts is null ? TermStructure.Discount(numeraireTime, true) : Discount(yts, numeraireTime, null);
        }

        return NumeraireArray(t, [y], tabulation)[0]
            * (yts is null
                ? 1.0
                : Discount(yts, numeraireTime, null) / Discount(yts, t, tDate, false) * TermStructure.Discount(t, true)
                    / TermStructure.Discount(numeraireTime, true));
    }

    internal override double ZerobondCore(double T, Date? maturity, double t, Date? tDate, double y, YieldTermStructure? yts, Gaussian1dMemo memo)
    {
        if (t == 0.0)
        {
            return yts is null ? TermStructure.Discount(T, true) : Discount(yts, T, maturity);
        }

        return ZerobondArray(T, t, [y])[0]
            * (yts is null
                ? 1.0
                : Discount(yts, T, maturity, false) / Discount(yts, t, tDate, false) * TermStructure.Discount(t, true)
                    / TermStructure.Discount(T, true));
    }

    /// <inheritdoc/>
    protected override double NumeraireImpl(double t, double y, YieldTermStructure? yts) => NumeraireCore(t, null, y, yts, new Gaussian1dMemo());

    /// <inheritdoc/>
    protected override double ZerobondImpl(double T, double t, double y, YieldTermStructure? yts) => ZerobondCore(T, null, t, null, y, yts, new Gaussian1dMemo());

    private static ImmutableArray<T> Copied<T>(IEnumerable<T> values, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(values))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(values, paramName);
        return [.. values];
    }

    // QuantLib's "yield term structure handle is empty", and the other null checks.
    private static T Checked<T>(T value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return value;
    }

    private static T CheckedWith<T>(T value, string message, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class =>
        value ?? throw new ArgumentNullException(paramName, message);

    // QuantLib's checks of the swaption expiries and tenors.
    private static ImmutableArray<Date> SwaptionPoints(ImmutableArray<Date> swaptionExpiries, ImmutableArray<Period> swaptionTenors)
    {
        if (!(swaptionExpiries.Length == swaptionTenors.Length))
        {
            throw new ArgumentException(
                $"number of swaption expiries ({swaptionExpiries.Length}) is differnt from number of swaption tenors ({swaptionTenors.Length})",
                nameof(swaptionTenors));
        }

        if (swaptionExpiries.IsEmpty)
        {
            throw new ArgumentException("need at least one swaption expiry to calibrate numeraire", nameof(swaptionExpiries));
        }

        return swaptionExpiries;
    }

    private static ImmutableArray<Date> CapletPoints(ImmutableArray<Date> capletExpiries)
    {
        if (capletExpiries.IsEmpty)
        {
            throw new ArgumentException("need at least one caplet expiry to calibrate numeraire", nameof(capletExpiries));
        }

        return capletExpiries;
    }

    // QuantLib's updateTimes1: each step date's time on the model curve, positive and increasing.
    private static ImmutableArray<double> UpdateTimes1(YieldTermStructure termStructure, ImmutableArray<Date> volstepdates)
    {
        var steptimes = new double[volstepdates.Length];
        for (int j = 0; j < volstepdates.Length; j++)
        {
            steptimes[j] = termStructure.TimeFromReference(volstepdates[j]);
            if (j == 0)
            {
                if (!(steptimes[0] > 0.0))
                {
                    throw new ArgumentException(
                        string.Create(CultureInfo.InvariantCulture, $"volsteptimes must be positive ({steptimes[0]})"),
                        nameof(volstepdates));
                }
            }
            else if (!(steptimes[j] > steptimes[j - 1]))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"volsteptimes must be strictly increasing ({steptimes[j - 1]}@{j - 1}, {steptimes[j]}@{j})"),
                    nameof(volstepdates));
            }
        }

        return [.. steptimes];
    }

    // std::max(a, b) and std::min(a, b), which keep or drop a NaN by position.
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    private static string Show(double x) => DataFormattersFunctions.General(x);

    // The numeraire's interpolation over one row of the discrete numeraire: a monotonic cubic spline, extrapolated
    // flat, as QuantLib's numeraire_.
    private Interpolation NumeraireInterpolation(double[] row) =>
        new FlatExtrapolator(
            new CubicInterpolation(
                y.AsSpan(),
                row,
                CubicInterpolationDerivativeApprox.Spline,
                true,
                CubicInterpolationBoundaryCondition.Lagrange,
                0.0,
                CubicInterpolationBoundaryCondition.Lagrange,
                0.0).EnableExtrapolation()).EnableExtrapolation();

    // QuantLib's updateTimes2: the numeraire time, the calibration times and the volatilities' count.
    private (double NumeraireTime, ImmutableArray<double> Times) UpdateTimes2()
    {
        double nt = TermStructure.TimeFromReference(numeraireDate);
        var t = new List<double> { 0.0 };
        foreach (CalibrationPoint point in calibrationPoints)
        {
            t.Add(TermStructure.TimeFromReference(point.Expiry));
        }

        t.Add(nt);
        if (!(volatilities.Length == volsteptimes.Length + 1))
        {
            throw new ArgumentException(
                $"there must be n+1 volatilities ({volatilities.Length}) for n volatility step times ({volsteptimes.Length})",
                nameof(volatilities));
        }

        return (nt, [.. t]);
    }

    private void MakeSwaptionCalibrationPoint(SortedDictionary<Date, CalibrationPoint> points, Date expiry, Period tenor, Gaussian1dMemo memo, EvaluationContext context)
    {
        if (points.ContainsKey(expiry))
        {
            throw new ArgumentException($"swaption expiry ({expiry}) occurs more than once in calibration set", "swaptionExpiries");
        }

        VanillaSwap underlying = memo.UnderlyingSwap(swapIndexBase!, expiry, tenor, context);

        Schedule sched = underlying.FixedSchedule;
        NanoQL.Time.Calendar cal = CalendarOf(sched);
        BusinessDayConvention bdc = underlying.PaymentConvention;

        var yearFractions = new List<double>();
        var paymentDates = new List<Date>();
        for (int k = 1; k < sched.Size; k++)
        {
            yearFractions.Add(swapIndexBase!.DayCounter.YearFraction(k == 1 ? expiry : sched.Date(k - 1), sched.Date(k)));
            paymentDates.Add(cal.Adjust(sched.Date(k), bdc));
        }

        points[expiry] = new CalibrationPoint(expiry, false, tenor, [.. paymentDates], [.. yearFractions]);
    }

    private void MakeCapletCalibrationPoint(SortedDictionary<Date, CalibrationPoint> points, Date expiry)
    {
        if (points.ContainsKey(expiry))
        {
            throw new ArgumentException($"caplet expiry ({expiry}) occurs more than once in calibration set", "capletExpiries");
        }

        Date valueDate = iborIndex.ValueDate(expiry);

        // the index's maturity date, as QuantLib's master computes the forward rate (#2848)
        Date endDate = iborIndex.MaturityDate(valueDate);

        // adjust the first period to start on expiry
        points[expiry] = new CalibrationPoint(expiry, true, iborIndex.Tenor, [endDate], [iborIndex.DayCounter.YearFraction(expiry, endDate)]);
    }

    // QuantLib's loop in initialize(): the numeraire date, the last payment date, with a calibration point added at
    // every payment date before it that isn't one, so that the tabulation reads only tabulated numeraires.
    private Date FindNumeraireDate(SortedDictionary<Date, CalibrationPoint> points, Gaussian1dMemo memo, EvaluationContext context)
    {
        bool done;
        Date numeraire = Date.MinDate;
        do
        {
            Date numeraireKnown = numeraire;
            done = true;
            Date[] keys = [.. points.Keys.Reverse()];
            for (int i = 0; i < keys.Length && done; ++i)
            {
                CalibrationPoint point = points[keys[i]];
                if (point.PaymentDates[^1] > numeraire)
                {
                    numeraire = point.PaymentDates[^1];
                    numeraireKnown = point.PaymentDates[^1];
                    done = i == 0;
                }

                for (int j = point.PaymentDates.Length - 1; j >= 0 && done; --j)
                {
                    Date d = point.PaymentDates[j];
                    if (d < numeraireKnown)
                    {
                        if (capletCalibrated)
                        {
                            MakeCapletCalibrationPoint(points, d);
                            done = false;
                            break;
                        }
                        else
                        {
                            int months = Math.Max(1, (int)((numeraireKnown - d) / 365.25 * 12.0));
                            while (memo.UnderlyingSwap(swapIndexBase!, d, new Period(months, TimeUnit.Months), context).MaturityDate() < numeraireKnown)
                            {
                                ++months;
                            }

                            MakeSwaptionCalibrationPoint(points, d, new Period(months, TimeUnit.Months), memo, context);
                            done = false;
                            break;
                        }
                    }
                }

                if (done)
                {
                    numeraireKnown = keys[i];
                }
            }
        }
        while (!done);
        return numeraire;
    }

    // QuantLib's updateSmiles: per calibration point, from the last to the first, its annuity and at-the-money level on
    // the model's curve, its market smile at that level, the smile's pretreatment and the digitals at the rate bounds.
    private Smiles UpdateSmiles(EvaluationContext context)
    {
        var points = new SmilePoint[calibrationPoints.Length];
        var arbitrageIndices = new List<(int Left, int Right)?>();
        MarkovFunctionalModelSettingsAdjustments adjustments = modelSettings.Adjustments;

        int pointIndex = 0;
        for (int p = calibrationPoints.Length - 1; p >= 0; p--)
        {
            CalibrationPoint point = calibrationPoints[p];
            SmileSection smileSection;
            double annuity, atm;
            if (point.IsCaplet)
            {
                annuity = point.YearFractions[0] * TermStructure.Discount(point.PaymentDates[0], true);
                atm = (TermStructure.Discount(point.Expiry, true) - TermStructure.Discount(point.PaymentDates[0], true)) / annuity;
                smileSection = capletVol!.SmileSection(point.Expiry, true);
            }
            else
            {
                annuity = 0.0;
                for (int k = 0; k < point.PaymentDates.Length; k++)
                {
                    annuity += point.YearFractions[k] * TermStructure.Discount(point.PaymentDates[k], true);
                }

                atm = (TermStructure.Discount(point.Expiry, true) - TermStructure.Discount(point.PaymentDates[^1], true)) / annuity;
                smileSection = swaptionVol!.SmileSection(point.Expiry, point.Tenor, true);
            }

            SmileSection rawSmileSection = new AtmSmileSection(smileSection, atm);
            SmileSection pretreated;

            int forcedLeftIndex = -1;
            int forcedRightIndex = int.MaxValue;
            if (forcedArbitrageIndices.Length > pointIndex)
            {
                forcedLeftIndex = forcedArbitrageIndices[pointIndex].Left;
                forcedRightIndex = forcedArbitrageIndices[pointIndex].Right;
            }

            if ((adjustments & KahaleSmile) != 0)
            {
                var kahale = new KahaleSmileSection(
                    rawSmileSection,
                    atm,
                    (adjustments & KahaleInterpolation) != 0,
                    (adjustments & SmileExponentialExtrapolation) != 0,
                    (adjustments & SmileDeleteArbitragePoints) != 0,
                    modelSettings.SmileMoneynessCheckpoints.AsSpan(),
                    modelSettings.DigitalGap,
                    forcedLeftIndex,
                    forcedRightIndex);
                pretreated = kahale;
                arbitrageIndices.Add(kahale.CoreIndices());
            }
            else if ((adjustments & SabrSmile) != 0)
            {
                var ssutils = new SmileSectionUtils(rawSmileSection, modelSettings.SmileMoneynessCheckpoints.AsSpan());
                List<double> k = [.. ssutils.StrikeGrid()];
                k.RemoveAt(0); // the first strike is zero which we do
                               // not want in the sabr calibration
                if (!(rawSmileSection.VolatilityType == VolatilityType.ShiftedLognormal))
                {
                    throw new NanoQLException("MarkovFunctional: SABR calibration to normal input volatilities is not supported");
                }

                if (!(k.Count >= 4))
                {
                    throw new NanoQLException($"for sabr calibration at least 4 points are needed (is {k.Count})");
                }

                var v = new double[k.Count];
                for (int j = 0; j < k.Count; j++)
                {
                    v[j] = rawSmileSection.Volatility(k[j]);
                }

                // TODO should we fix beta to avoid numerical instabilities
                // during calibration ?
                SabrInterpolatedSmileSection sabrSection = SabrInterpolatedSmileSection.Create(
                    point.Expiry,
                    atm,
                    [.. k],
                    false,
                    rawSmileSection.Volatility(atm),
                    v,
                    0.03,
                    0.80,
                    0.50,
                    0.00,
                    context,
                    false,
                    false,
                    false,
                    false,
                    true,
                    null,
                    null,
                    new Actual365Fixed(),
                    rawSmileSection.Shift);

                // we make the sabr section arbitrage free by superimposing
                // a kahalesection
                var kahale = new KahaleSmileSection(
                    sabrSection,
                    atm,
                    false,
                    (adjustments & SmileExponentialExtrapolation) != 0,
                    (adjustments & SmileDeleteArbitragePoints) != 0,
                    modelSettings.SmileMoneynessCheckpoints.AsSpan(),
                    modelSettings.DigitalGap,
                    forcedLeftIndex,
                    forcedRightIndex);
                pretreated = kahale;
                arbitrageIndices.Add(kahale.CoreIndices());
            }
            else if ((adjustments & CustomSmile) != 0)
            {
                // Custom smile section is af by assumption
                pretreated = modelSettings.CustomSmileFactory!.SmileSection(rawSmileSection, atm);
                arbitrageIndices.Add(null);
            }
            else
            {
                // no smile pretreatment
                pretreated = rawSmileSection;
            }

            // custom smile will take care of this itself
            double? minRateDigital = null, maxRateDigital = null;
            if ((adjustments & CustomSmile) == 0)
            {
                minRateDigital = pretreated.DigitalOptionPrice(
                    modelSettings.LowerRateBound - pretreated.Shift, OptionType.Call, annuity, modelSettings.DigitalGap);
                // QuantLib's fix after v1.44 (#2912): the upper bound's digital struck at the bound, as the model reads
                // the bound unshifted, where 1.44 subtracts the smile's shift
                maxRateDigital = pretreated.DigitalOptionPrice(
                    modelSettings.UpperRateBound, OptionType.Call, annuity, modelSettings.DigitalGap);
            }

            points[p] = new SmilePoint(atm, annuity, pretreated, rawSmileSection, minRateDigital, maxRateDigital);
            ++pointIndex;
        }

        return new Smiles([.. points], [.. arbitrageIndices]);
    }

    // QuantLib's updateNumeraireTabulation: per calibration point, from the last to the first, the numeraire on the
    // state grid at its expiry, inverting the model's digital prices to market rates on the point's smile.
    private Tabulation UpdateNumeraireTabulation(Tabulation tab)
    {
        var messages = new List<string> { "updating numeraire tabulation" };
        var adjustmentFactors = new List<double>();
        var digitalsAdjustmentFactors = new List<double>();
        MarkovFunctionalModelSettingsAdjustments adjustments = modelSettings.Adjustments;

        int idx = times.Length - 2;
        for (int p = calibrationPoints.Length - 1; p >= 0; p--, --idx)
        {
            CalibrationPoint point = calibrationPoints[p];
            SmilePoint smile = smiles.Points[p];
            MarkovFunctionalCustomSmileSection? mfSec = null;
            if ((adjustments & CustomSmile) != 0)
            {
                mfSec = smile.SmileSection as MarkovFunctionalCustomSmileSection
                    ?? throw new NanoQLException("no CustomSmileSection given, this is unexpected...");
            }

            double[] discreteDeflatedAnnuities = new double[y.Size];
            double[] deflatedFinalPayments = [];

            double numeraire0 = TermStructure.Discount(numeraireTime, true);
            double normalization = TermStructure.Discount(times[idx], true) / numeraire0;

            for (int k = 0; k < point.PaymentDates.Length; k++)
            {
                deflatedFinalPayments = DeflatedZerobondArray(TermStructure.TimeFromReference(point.PaymentDates[k]), times[idx], y.AsSpan(), tab);
                for (int j = 0; j < y.Size; j++)
                {
                    discreteDeflatedAnnuities[j] += deflatedFinalPayments[j] * point.YearFractions[k];
                }
            }

            var deflatedAnnuities = new CubicInterpolation(
                y.AsSpan(),
                discreteDeflatedAnnuities,
                CubicInterpolationDerivativeApprox.Spline,
                true,
                CubicInterpolationBoundaryCondition.Lagrange,
                0.0,
                CubicInterpolationBoundaryCondition.Lagrange,
                0.0);
            ImmutableArray<double> ca = deflatedAnnuities.ACoefficients(), cb = deflatedAnnuities.BCoefficients(), cc = deflatedAnnuities.CCoefficients();

            double digitalsCorrectionFactor = 1.0;
            digitalsAdjustmentFactors.Insert(0, digitalsCorrectionFactor);

            double digital = 0.0, swapRate, swapRate0;
            int n = y.Size;

            for (int c = 0; c == 0 || (c == 1 && (adjustments & AdjustDigitals) != 0); c++)
            {
                if (c == 1)
                {
                    digitalsCorrectionFactor = smile.Annuity / digital;
                    digitalsAdjustmentFactors[0] = digitalsCorrectionFactor;
                }

                digital = 0.0;
                swapRate0 = modelSettings.UpperRateBound / 2.0; // initial guess
                for (int j = n - 1; j >= 0; j--)
                {
                    double integral = 0.0;

                    if (j == n - 1)
                    {
                        if ((adjustments & NoPayoffExtrapolation) == 0)
                        {
                            if ((adjustments & ExtrapolatePayoffFlat) != 0)
                            {
                                // the right tail at the last node's value, where QuantLib's takes the second-last's
                                //
                                integral = GaussianShiftedPolynomialIntegral(
                                    0.0, 0.0, 0.0, 0.0, discreteDeflatedAnnuities[j], y[j - 1], y[j], 100.0);
                            }
                            else
                            {
                                integral = GaussianShiftedPolynomialIntegral(
                                    0.0, cc[j - 1], cb[j - 1], ca[j - 1], discreteDeflatedAnnuities[j - 1], y[j - 1], y[j], 100.0);
                            }
                        }
                    }
                    else
                    {
                        integral = GaussianShiftedPolynomialIntegral(
                            0.0, cc[j], cb[j], ca[j], discreteDeflatedAnnuities[j], y[j], y[j], y[j + 1]);
                    }

                    if (integral < 0)
                    {
                        messages.Add($"WARNING: integral for digitalPrice is negative for j={j} ({Show(integral)}) --- reset it to zero.");
                        integral = 0.0;
                    }

                    digital += integral * numeraire0 * digitalsCorrectionFactor;

                    bool check = true;
                    if (mfSec is not null)
                    {
                        swapRate = mfSec.InverseDigitalCall(digital, smile.Annuity);
                    }
                    else if (digital >= smile.MinRateDigital!.Value)
                    {
                        swapRate = modelSettings.LowerRateBound - smile.RawSmileSection.Shift;
                        check = false;
                    }
                    else if (digital <= smile.MaxRateDigital!.Value)
                    {
                        swapRate = modelSettings.UpperRateBound;
                        check = false;
                    }
                    else
                    {
                        swapRate = MarketSwapRate(smile, digital, swapRate0, smile.RawSmileSection.Shift);
                    }

                    if (check && j < n - 1 && swapRate > swapRate0)
                    {
                        messages.Add(
                            $"WARNING: swap rate is decreasing in y for t={Show(times[idx])}, j={j} (y, swap rate) is ({Show(y[j])},"
                            + $"{Show(swapRate)}) but for j={j + 1} it is ({Show(y[j + 1])},{Show(swapRate0)}) --- reset rate to "
                            + $"{Show(swapRate0)} in node j={j}");
                        swapRate = swapRate0;
                    }

                    swapRate0 = swapRate;
                    double numeraire = 1.0 / Max((swapRate * discreteDeflatedAnnuities[j]) + deflatedFinalPayments[j], 1E-6);
                    tab.Rows[idx][j] = numeraire * normalization;
                }
            }

            if ((adjustments & AdjustYts) != 0)
            {
                tab.Numeraire[idx] = NumeraireInterpolation(tab.Rows[idx]);
                double modelDeflatedZerobond = DeflatedZerobondArray(times[idx], 0.0, [0.0], tab)[0];
                double marketDeflatedZerobond = TermStructure.Discount(times[idx], true) / TermStructure.Discount(numeraireTime, true);
                for (int j = n - 1; j >= 0; j--)
                {
                    tab.Rows[idx][j] *= modelDeflatedZerobond / marketDeflatedZerobond;
                }

                adjustmentFactors.Insert(0, modelDeflatedZerobond / marketDeflatedZerobond);
            }
            else
            {
                adjustmentFactors.Insert(0, 1.0);
            }

            tab.Numeraire[idx] = NumeraireInterpolation(tab.Rows[idx]);
        }

        tab.AdjustmentFactors = [.. adjustmentFactors];
        tab.DigitalsAdjustmentFactors = [.. digitalsAdjustmentFactors];
        tab.Messages = [.. messages];
        return tab;
    }

    // QuantLib's numeraireArray: the numeraire at a time on a state grid, interpolated between the tabulated times in
    // the reciprocal, linearly in time.
    private double[] NumeraireArray(double t, ReadOnlySpan<double> ys, Tabulation tab)
    {
        double[] res = new double[ys.Length];
        Array.Fill(res, TermStructure.Discount(numeraireTime, true));
        if (t < NumericLimits.QL_EPSILON)
        {
            return res;
        }

        double inverseNormalization = TermStructure.Discount(numeraireTime, true) / TermStructure.Discount(t, true);

        double tz = Min(t, times[^1]);
        int i = Math.Min(Interpolation.UpperBound(times.AsSpan()[..^1], t), times.Length - 1);

        double ta = times[i - 1];
        double tb = times[i];
        double dt = tb - ta;

        for (int j = 0; j < ys.Length; j++)
        {
            double na = tab.Numeraire[i - 1].Evaluate(ys[j]);
            double nb = tab.Numeraire[i].Evaluate(ys[j]);
            res[j] = inverseNormalization / (((tz - ta) / nb) + ((tb - tz) / na)) * dt;
        }

        return res;
    }

    private double[] ZerobondArray(double T, double t, ReadOnlySpan<double> ys)
    {
        double[] deflated = DeflatedZerobondArray(T, t, ys, tabulation);
        double[] numeraire = NumeraireArray(t, ys, tabulation);
        for (int j = 0; j < deflated.Length; j++)
        {
            deflated[j] *= numeraire[j];
        }

        return deflated;
    }

    // QuantLib's deflatedZerobondArray: the zero bond over the numeraire, by Gauss-Hermite integration over the state
    // at its maturity.
    private double[] DeflatedZerobondArray(double T, double t, ReadOnlySpan<double> ys, Tabulation tab)
    {
        double[] result = new double[ys.Length];

        // Gauss Hermite
        double stdDev0t = process.StdDeviation(0.0, 0.0, t);

        // we use that the standard deviation is independent of $x$ here
        double stdDev0T = process.StdDeviation(0.0, 0.0, T);
        double stdDevtT = process.StdDeviation(t, 0.0, T - t);

        int points = modelSettings.GaussHermitePoints;
        double[] ya = new double[points];
        for (int j = 0; j < ys.Length; j++)
        {
            for (int i = 0; i < points; i++)
            {
                ya[i] = ((ys[j] * stdDev0t) + (stdDevtT * normalIntegralX[i])) / stdDev0T;
            }

            double[] res = NumeraireArray(T, ya, tab);
            for (int i = 0; i < points; i++)
            {
                result[j] += normalIntegralW[i] / res[i];
            }
        }

        return result;
    }

    // QuantLib's marketSwapRate: the strike whose market digital is the model's, by Brent's solver within the bounds.
    private double MarketSwapRate(SmilePoint smile, double digitalPrice, double guess, double shift)
    {
        double ZeroHelper(double strike) =>
            smile.SmileSection.DigitalOptionPrice(strike, OptionType.Call, smile.Annuity, modelSettings.DigitalGap) - digitalPrice;

        var b = new Brent();
        return b.Solve(
            ZeroHelper,
            modelSettings.MarketRateAccuracy,
            Max(Min(guess, modelSettings.UpperRateBound - 0.00001), modelSettings.LowerRateBound - shift + 0.00001),
            modelSettings.LowerRateBound - shift,
            modelSettings.UpperRateBound);
    }

    // The methods below are tagged internal in QuantLib: they are intended only to produce the volatility diagnostics
    // in the model outputs, since the numeraire calibration's instruments, which start at their fixing dates, have no
    // pricing engine.
    private double ForwardRateInternal(Date fixing, Date? referenceDate, double y, bool zeroFixingDays, IborIndex? iborIdx, Gaussian1dMemo memo)
    {
        iborIdx ??= iborIndex;

        Date valueDate = zeroFixingDays ? fixing : iborIdx.ValueDate(fixing);

        // the index's maturity date, as QuantLib's master computes the forward rate (#2848)
        Date endDate = iborIdx.MaturityDate(iborIdx.ValueDate(fixing));
        double dcf = iborIdx.DayCounter.YearFraction(valueDate, endDate);

        return (Zerobond(valueDate, referenceDate, y, null, memo) - Zerobond(endDate, referenceDate, y, null, memo))
            / (dcf * Zerobond(endDate, referenceDate, y, null, memo));
    }

    private double SwapRateInternal(
        Date fixing, Period tenor, Date? referenceDate, double y, bool zeroFixingDays, SwapIndex? swapIdx, Gaussian1dMemo memo, EvaluationContext context)
    {
        swapIdx ??= swapIndexBase ?? throw new NanoQLException("No swap index given");

        VanillaSwap underlying = memo.UnderlyingSwap(swapIdx, fixing, tenor, context);

        Schedule sched = underlying.FixedSchedule;
        double annuity = SwapAnnuityInternal(fixing, tenor, referenceDate, y, zeroFixingDays, swapIdx, memo, context);
        double atm = (Zerobond(zeroFixingDays ? fixing : sched.Dates[0], referenceDate, y, null, memo)
                - Zerobond(CalendarOf(sched).Adjust(sched.Dates[^1], underlying.PaymentConvention), referenceDate, y, null, memo))
            / annuity;
        return atm;
    }

    private double SwapAnnuityInternal(
        Date fixing, Period tenor, Date? referenceDate, double y, bool zeroFixingDays, SwapIndex? swapIdx, Gaussian1dMemo memo, EvaluationContext context)
    {
        swapIdx ??= swapIndexBase ?? throw new NanoQLException("No swap index given");

        VanillaSwap underlying = memo.UnderlyingSwap(swapIdx, fixing, tenor, context);

        Schedule sched = underlying.FixedSchedule;

        double annuity = 0.0;
        for (int j = 1; j < sched.Size; j++)
        {
            annuity += Zerobond(CalendarOf(sched).Adjust(sched.Date(j), underlying.PaymentConvention), referenceDate, y, null, memo)
                * swapIdx.DayCounter.YearFraction(j == 1 && zeroFixingDays ? fixing : sched.Date(j - 1), sched.Date(j));
        }

        return annuity;
    }

    private double SwaptionPriceInternal(
        OptionType type,
        Date expiry,
        Period tenor,
        double strike,
        Date? referenceDate,
        double y,
        bool zeroFixingDays,
        SwapIndex? swapIdx,
        Gaussian1dMemo memo,
        EvaluationContext context)
    {
        double fixingTime = TermStructure.TimeFromReference(expiry);
        double referenceTime = referenceDate is Date r ? TermStructure.TimeFromReference(r) : 0.0;

        Vector yg = YGrid(modelSettings.YStdDevs, modelSettings.YGridPoints, fixingTime, referenceTime, y, memo);
        Vector z = YGrid(modelSettings.YStdDevs, modelSettings.YGridPoints, 1.0, 0.0, 0.0, memo);
        double[] p = new double[yg.Size];

        for (int i = 0; i < yg.Size; i++)
        {
            double annuity = SwapAnnuityInternal(expiry, tenor, expiry, yg[i], zeroFixingDays, swapIdx, memo, context);
            double atm = SwapRateInternal(expiry, tenor, expiry, yg[i], zeroFixingDays, swapIdx, memo, context);
            p[i] = annuity * Max((type == OptionType.Call ? 1.0 : -1.0) * (atm - strike), 0.0)
                / NumeraireCore(fixingTime, expiry, yg[i], null, memo);
        }

        return IntegratedPayoff(type, z, p) * NumeraireCore(referenceTime, referenceDate, y, null, memo);
    }

    private double CapletPriceInternal(
        OptionType type, Date expiry, double strike, Date? referenceDate, double y, bool zeroFixingDays, IborIndex? iborIdx, Gaussian1dMemo memo)
    {
        iborIdx ??= iborIndex;

        double fixingTime = TermStructure.TimeFromReference(expiry);
        double referenceTime = referenceDate is Date r ? TermStructure.TimeFromReference(r) : 0.0;

        Vector yg = YGrid(modelSettings.YStdDevs, modelSettings.YGridPoints, fixingTime, referenceTime, y, memo);
        Vector z = YGrid(modelSettings.YStdDevs, modelSettings.YGridPoints, 1.0, 0.0, 0.0, memo);
        double[] p = new double[yg.Size];

        Date valueDate = iborIdx.ValueDate(expiry);

        // the index's maturity date, as QuantLib's master computes the forward rate (#2848)
        Date endDate = iborIdx.MaturityDate(valueDate);
        double dcf = iborIdx.DayCounter.YearFraction(zeroFixingDays ? expiry : valueDate, endDate);

        for (int i = 0; i < yg.Size; i++)
        {
            double annuity = Zerobond(endDate, expiry, yg[i], null, memo) * dcf;
            double atm = ForwardRateInternal(expiry, expiry, yg[i], zeroFixingDays, iborIdx, memo);
            p[i] = annuity * Max((type == OptionType.Call ? 1.0 : -1.0) * (atm - strike), 0.0)
                / NumeraireCore(fixingTime, expiry, yg[i], null, memo);
        }

        return IntegratedPayoff(type, z, p) * NumeraireCore(referenceTime, referenceDate, y, null, memo);
    }

    // The payoff's cubic spline integrated over the state grid and, with payoff extrapolation, past it: flat, at the
    // boundary nodes' values (the right tail's the last node's, where QuantLib's takes the second-last's), or the end
    // segments' cubics, the call's right and the put's left.
    private double IntegratedPayoff(OptionType type, Vector z, double[] p)
    {
        var payoff = new CubicInterpolation(
            z.AsSpan(),
            p,
            CubicInterpolationDerivativeApprox.Spline,
            true,
            CubicInterpolationBoundaryCondition.Lagrange,
            0.0,
            CubicInterpolationBoundaryCondition.Lagrange,
            0.0);
        ImmutableArray<double> a = payoff.ACoefficients(), b = payoff.BCoefficients(), c = payoff.CCoefficients();

        double price = 0.0;
        for (int i = 0; i < z.Size - 1; i++)
        {
            price += GaussianShiftedPolynomialIntegral(0.0, c[i], b[i], a[i], p[i], z[i], z[i], z[i + 1]);
        }

        MarkovFunctionalModelSettingsAdjustments adjustments = modelSettings.Adjustments;
        if ((adjustments & NoPayoffExtrapolation) == 0)
        {
            if ((adjustments & ExtrapolatePayoffFlat) != 0)
            {
                price += GaussianShiftedPolynomialIntegral(0.0, 0.0, 0.0, 0.0, p[z.Size - 1], z[z.Size - 2], z[z.Size - 1], 100.0);
                price += GaussianShiftedPolynomialIntegral(0.0, 0.0, 0.0, 0.0, p[0], z[0], -100.0, z[0]);
            }
            else
            {
                if (type == OptionType.Call)
                {
                    price += GaussianShiftedPolynomialIntegral(
                        0.0, c[z.Size - 2], b[z.Size - 2], a[z.Size - 2], p[z.Size - 2], z[z.Size - 2], z[z.Size - 1], 100.0);
                }

                if (type == OptionType.Put)
                {
                    price += GaussianShiftedPolynomialIntegral(0.0, c[0], b[0], a[0], p[0], z[0], -100.0, z[0]);
                }
            }
        }

        return price;
    }

    // QuantLib's CalibrationPoint, its smile's part apart (SmilePoint): a calibration instrument's expiry, kind, tenor,
    // payment dates and accrual fractions.
    private sealed record CalibrationPoint(Date Expiry, bool IsCaplet, Period Tenor, ImmutableArray<Date> PaymentDates, ImmutableArray<double> YearFractions);

    // A calibration point's smile: its annuity and at-the-money level on the model's curve, its pretreated and raw
    // smile sections, and the digitals at the rate bounds (none for a custom smile).
    private sealed record SmilePoint(
        double Atm, double Annuity, SmileSection SmileSection, SmileSection RawSmileSection, double? MinRateDigital, double? MaxRateDigital);

    // The smiles of every calibration point, in date order, and QuantLib's arbitrageIndices_, from the last point.
    private sealed record Smiles(ImmutableArray<SmilePoint> Points, ImmutableArray<(int Left, int Right)?> ArbitrageIndices);

    // QuantLib's discreteNumeraire_ and numeraire_, with the tabulation's outputs: a working type the constructor
    // fills and never changes after.
    private sealed class Tabulation(double[][] rows, Interpolation[] numeraire)
    {
        public double[][] Rows { get; } = rows;

        public Interpolation[] Numeraire { get; } = numeraire;

        public ImmutableArray<double> AdjustmentFactors { get; set; } = [];

        public ImmutableArray<double> DigitalsAdjustmentFactors { get; set; } = [];

        public ImmutableArray<string> Messages { get; set; } = [];

        // The start of a copy's tabulation: the receiver's rows and interpolations, which QuantLib's model keeps.
        public Tabulation Copy() => new([.. Rows.Select(r => (double[])r.Clone())], (Interpolation[])Numeraire.Clone());
    }
}
