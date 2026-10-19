// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/calibrationhelper.hpp ql/models/calibrationhelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;

namespace NanoQL.Models;

/// <summary>
/// A liquid market instrument used during calibration, quoted by a Black (or Bachelier) volatility: its market value is
/// the Black price at the quoted volatility.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. QuantLib's helper computes its market value lazily, at its first use; here a derived helper's
/// constructor computes it, under the context it is given, through <see cref="ComputeMarketValue"/>.
/// </para>
/// <para>
/// <b>A helper of one's own.</b> The base's constructor computes nothing: a derived constructor first builds what its
/// <see cref="BlackPrice"/> reads, then calls <see cref="ComputeMarketValue"/> last, as QuantLib's derived helpers call
/// the base's <c>performCalculations</c> last in their own. C# runs a further override of <see cref="BlackPrice"/>,
/// called from that constructor, before the further class's constructor has run, so the class whose constructor calls
/// <see cref="ComputeMarketValue"/> seals <see cref="BlackPrice"/> (or is sealed, as <see cref="SwaptionHelper"/> is).
/// </para>
/// <para>An extension point, as in QuantLib: the swaption helper derives from it, as later ports' cap and Heston helpers will.</para>
/// </remarks>
public abstract class BlackCalibrationHelper : CalibrationHelper
{
    private readonly BlackCalibrationHelperCalibrationErrorType calibrationErrorType;

    /// <summary>Initializes a new instance of the <see cref="BlackCalibrationHelper"/> class.</summary>
    /// <param name="volatility">The quoted volatility.</param>
    /// <param name="calibrationErrorType">How the helper measures its error.</param>
    /// <param name="type">The volatility's type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="calibrationErrorType"/> or <paramref name="type"/> isn't a member of its type.
    /// </exception>
    protected BlackCalibrationHelper(
        double volatility,
        BlackCalibrationHelperCalibrationErrorType calibrationErrorType = BlackCalibrationHelperCalibrationErrorType.RelativePriceError,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0)
    {
        // QuantLib refuses another value where the calibration error switches on it.
        if (calibrationErrorType is < BlackCalibrationHelperCalibrationErrorType.RelativePriceError
            or > BlackCalibrationHelperCalibrationErrorType.ImpliedVolError)
        {
            throw new ArgumentOutOfRangeException(
                nameof(calibrationErrorType), calibrationErrorType, "unknown Calibration Error Type");
        }

        EnumArguments.Check(type, nameof(type));
        Volatility = volatility;
        VolatilityType = type;
        Shift = shift;
        this.calibrationErrorType = calibrationErrorType;
    }

    /// <summary>Gets the quoted volatility.</summary>
    public double Volatility { get; }

    /// <summary>Gets the volatility's type.</summary>
    public VolatilityType VolatilityType { get; }

    /// <summary>
    /// Gets the instrument's market value, its Black price at the quoted volatility, which a derived constructor
    /// computes through <see cref="ComputeMarketValue"/>.
    /// </summary>
    public double MarketValue { get; private set; }

    /// <summary>Gets the shift of a shifted lognormal volatility.</summary>
    protected double Shift { get; }

    /// <summary>The instrument's price according to a model.</summary>
    /// <param name="model">The model.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The model value.</returns>
    public abstract double ModelValue(ICalibratedModel model, EvaluationContext context);

    /// <summary>
    /// The error resulting from the model's valuation, by the helper's error type: the relative or absolute price
    /// error, or the difference between the volatility the model value implies and the quoted one.
    /// </summary>
    /// <param name="model">The model the instrument is priced against.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">The instrument can't be priced, or the implied volatility not solved.</exception>
    /// <remarks>
    /// For the implied-volatility error, the model value is clamped to the volatilities 0.001 and 10 for a shifted
    /// lognormal volatility, 0.00005 and 0.5 for a normal one: the volatility is the bound when the model value is at
    /// or beyond the Black price there, and is solved with an accuracy of 1e-12 in 5,000 evaluations otherwise.
    /// </remarks>
    public override double CalibrationError(ICalibratedModel model, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        double error;

        switch (calibrationErrorType)
        {
            case BlackCalibrationHelperCalibrationErrorType.RelativePriceError:
                error = Math.Abs(MarketValue - ModelValue(model, context)) / MarketValue;
                break;
            case BlackCalibrationHelperCalibrationErrorType.PriceError:
                error = MarketValue - ModelValue(model, context);
                break;
            case BlackCalibrationHelperCalibrationErrorType.ImpliedVolError:
                {
                    double minVol = VolatilityType == VolatilityType.ShiftedLognormal ? 0.0010 : 0.00005;
                    double maxVol = VolatilityType == VolatilityType.ShiftedLognormal ? 10.0 : 0.50;
                    double lowerPrice = BlackPrice(minVol, context);
                    double upperPrice = BlackPrice(maxVol, context);
                    double modelPrice = ModelValue(model, context);

                    double implied;
                    if (modelPrice <= lowerPrice)
                    {
                        implied = minVol;
                    }
                    else if (modelPrice >= upperPrice)
                    {
                        implied = maxVol;
                    }
                    else
                    {
                        implied = ImpliedVolatility(modelPrice, 1e-12, 5000, minVol, maxVol, context);
                    }

                    error = implied - Volatility;
                }

                break;
            default:
                throw new NanoQLException("unknown Calibration Error Type");
        }

        return error;
    }

    /// <summary>The times a lattice for the instrument must stop at: QuantLib's <c>addTimesTo</c>.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The times, not guaranteed to be sorted.</returns>
    public abstract ImmutableArray<double> Times(EvaluationContext context);

    /// <summary>
    /// The Black volatility a value implies: the volatility whose <see cref="BlackPrice"/> is the target, solved by
    /// <see cref="Brent"/> from the quoted volatility as its guess.
    /// </summary>
    /// <param name="targetValue">The value.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <param name="minVol">The bracket's lower end.</param>
    /// <param name="maxVol">The bracket's upper end.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The implied volatility.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The accuracy isn't positive, the bracket is empty, or the quoted volatility is outside it (the solver's refusal
    /// of its guess).
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The bracket doesn't bracket the target, the solver exceeds its evaluations, or the instrument can't be priced.
    /// </exception>
    public double ImpliedVolatility(
        double targetValue, double accuracy, int maxEvaluations, double minVol, double maxVol, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        Brent solver = new Brent().SetMaxEvaluations(maxEvaluations);
        try
        {
            return solver.Solve(new ImpliedVolatilityError(this, targetValue, context), accuracy, Volatility, minVol, maxVol);
        }
        catch (ArgumentException e) when (e.ParamName == "xMin")
        {
            // The solver's empty range is this member's own argument: it names its own parameter.
            throw new ArgumentException(DiscountingSwapEngine.QuantLibText(e), nameof(minVol), e);
        }
    }

    /// <summary>The instrument's Black (or Bachelier, for a normal volatility) price at a volatility.</summary>
    /// <param name="volatility">The volatility.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The price.</returns>
    public abstract double BlackPrice(double volatility, EvaluationContext context);

    /// <summary>
    /// Sets the market value to the Black price at the quoted volatility, QuantLib's base <c>performCalculations</c>:
    /// for a derived constructor, last, once what <see cref="BlackPrice"/> reads is built.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    protected void ComputeMarketValue(EvaluationContext context) => MarketValue = BlackPrice(Volatility, context);

    // QuantLib's error lambda: the target less the Black price at the volatility.
    private readonly struct ImpliedVolatilityError(
        BlackCalibrationHelper helper, double targetValue, EvaluationContext context) : ISolver1DFunction
    {
        public double Evaluate(double x) => targetValue - helper.BlackPrice(x, context);
    }
}
