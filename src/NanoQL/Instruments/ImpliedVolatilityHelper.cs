// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/impliedvolatility.hpp ql/instruments/impliedvolatility.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Instruments;

/// <summary>
/// The implied-volatility calculation for option classes: QuantLib's <c>detail::ImpliedVolatilityHelper</c>, the
/// volatility at which an engine's value is a target value, solved by Brent's method over a process whose Black
/// volatility is flat and moved by the solver.
/// </summary>
internal static class ImpliedVolatilityHelper
{
    /// <summary>
    /// The volatility at which the engine's body values the option at the target value: the option's arguments are
    /// checked once, then the body runs at each trial on a process cloned with a flat volatility.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="process">The process to clone.</param>
    /// <param name="body">The engine's body on a process: QuantLib's engine <c>calculate</c>, without the expiry
    /// check.</param>
    /// <param name="targetValue">The target value.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <param name="minVol">The lower end of the bracket.</param>
    /// <param name="maxVol">The upper end of the bracket.</param>
    /// <returns>The implied volatility.</returns>
    /// <exception cref="ArgumentException">The accuracy isn't positive, or the bracket is empty.</exception>
    /// <exception cref="NanoQLException">The value can't be bracketed, the solver exceeds its evaluations, or the
    /// engine fails.</exception>
    internal static double Calculate(
        Option option,
        GeneralizedBlackScholesProcess process,
        Func<GeneralizedBlackScholesProcess, InstrumentResults> body,
        double targetValue,
        double accuracy,
        int maxEvaluations,
        double minVol,
        double maxVol)
    {
        option.Validate();

        var f = new PriceError(process, body, targetValue);
        Brent solver = new Brent().SetMaxEvaluations(maxEvaluations);
        double guess = (minVol + maxVol) / 2.0;
        try
        {
            return solver.Solve(f, accuracy, guess, minVol, maxVol);
        }
        catch (ArgumentException e) when (e.ParamName == "xMin")
        {
            // The solver's empty range is the caller's own argument: it names the caller's parameter.
            throw new ArgumentException(DiscountingSwapEngine.QuantLibText(e), nameof(minVol), e);
        }
    }

    /// <summary>
    /// A process with the given one's spot and curves, and a flat Black volatility at the given level on its Black
    /// volatility's reference date, calendar and day counter, with QuantLib's default discretization.
    /// </summary>
    /// <param name="process">The process.</param>
    /// <param name="volatility">The flat volatility.</param>
    /// <returns>The cloned process.</returns>
    internal static GeneralizedBlackScholesProcess Clone(GeneralizedBlackScholesProcess process, double volatility)
    {
        BlackVolTermStructure blackVol = process.BlackVolatility;
        var flat = new BlackConstantVol(blackVol.ReferenceDate, blackVol.Calendar, volatility, blackVol.DayCounter);
        return new GeneralizedBlackScholesProcess(process.StateVariable, process.DividendYield, process.RiskFreeRate, flat);
    }

    // QuantLib's PriceError: the engine's value at a volatility, less the target.
    private readonly struct PriceError : ISolver1DFunction
    {
        private readonly GeneralizedBlackScholesProcess process;
        private readonly Func<GeneralizedBlackScholesProcess, InstrumentResults> body;
        private readonly double targetValue;

        public PriceError(
            GeneralizedBlackScholesProcess process,
            Func<GeneralizedBlackScholesProcess, InstrumentResults> body,
            double targetValue)
        {
            this.process = process;
            this.body = body;
            this.targetValue = targetValue;
        }

        public double Evaluate(double x)
        {
            InstrumentResults results = body(Clone(process, x));
            double value = results.NPV ?? throw new NanoQLException("pricing engine does not supply needed results");
            return value - targetValue;
        }
    }
}
