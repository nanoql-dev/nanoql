// SPDX-FileCopyrightText: Copyright (C) 2014 Bernd Lewerenz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/continuousarithmeticasianvecerengine.hpp ql/experimental/exoticoptions/continuousarithmeticasianvecerengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// Vecer's engine for continuous arithmetic average-price Asian options: the option's value as a one-dimensional PDE in
/// the replicating strategy's variable, solved by Crank-Nicolson (see
/// &lt;http://www.stat.columbia.edu/~vecer/asian-vecer.pdf&gt;); a put by the Asian put-call parity.
/// </summary>
/// <remarks>
/// <para>
/// The engine reads every structure on one clock, the earliest reference date of the process's structures on the
/// risk-free day counter, each at the instant a time denotes and nothing accrued before its own date (the S-xeq plan's
/// XER5 (xxii)), where QuantLib measures the time from the evaluation date on the risk-free day counter beside each
/// curve's zero rate from its own date: each rate is the curve's own zero rate to the maturity times its own time over
/// the clock's, and the volatility its own Black volatility at the maturity times the square root of its time over the
/// clock's; on structures sharing the evaluation date and the day counter each factor is 1 and QuantLib's numbers
/// stand. Where the initial point lies on the grid's upper end the price is the last node's value, the interpolation's
/// own limit, where QuantLib reads one node past the grid (XER5 (xxi)). The operators are built per call.
/// </para>
/// <para>
/// The averaging starts at the engine's start date, as QuantLib's; an option carrying a start date of its own that
/// differs is refused, where QuantLib ignores it. QuantLib's current average is never read by its engine, nor here.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class ContinuousArithmeticAsianVecerEngine
{
    /// <summary>Prices a continuous arithmetic average-price Asian option.</summary>
    /// <param name="option">The option: arithmetic, European, on a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="currentAverage">The average so far, never read (QuantLib's engine stores it unread).</param>
    /// <param name="startDate">The averaging's start, on or after the evaluation date.</param>
    /// <param name="timeSteps">The number of time steps.</param>
    /// <param name="assetSteps">The number of grid steps in the strategy's variable.</param>
    /// <param name="zMin">The grid's lower end, at most 0.</param>
    /// <param name="zMax">The grid's upper end, at least 0.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The average isn't arithmetic, the exercise isn't European, the payoff isn't striked, 0 isn't on the grid, the
    /// start date precedes the evaluation date ("Seasoned Asian not yet implemented") or differs from the option's, or
    /// the initial point isn't on the grid ("spot not on grid").
    /// </exception>
    public static OneAssetOptionResults Calculate(
        ContinuousAveragingAsianOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        double currentAverage,
        Date startDate,
        int timeSteps = 100,
        int assetSteps = 100,
        double zMin = -1.0,
        double zMax = 1.0)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        _ = currentAverage;
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults
        {
            NPV = Value(option, context, process, startDate, timeSteps, assetSteps, zMin, zMax),
        });
    }

    private static double Value(
        ContinuousAveragingAsianOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        Date startDate,
        int timeSteps,
        int assetSteps,
        double zMin,
        double zMax)
    {
        double expectedAverage;
        if (!(option.AverageType == AverageType.Arithmetic))
        {
            throw new NanoQLException("not an Arithmetic average option");
        }

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European Option");
        }

        if (option.StartDate is Date ownStart && ownStart != startDate)
        {
            throw new NanoQLException("the option's start date differs from the engine's");
        }

        double s0 = process.StateVariable;

        // payoff
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        // original time to maturity
        Date maturity = option.Exercise.LastDate();
        double x = payoff.Strike;
        if (!(zMin <= 0 && zMax >= 0))
        {
            throw new NanoQLException("strike (0 for vecer fixed strike asian)  not on Grid");
        }

        // the engine's clock: each read at the instant its time denotes (XER5 (xxii))
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        double clockT = clock.AccrualTimeTo(maturity);
        BlackVolTermStructure volatility = process.BlackVolatility;
        double sigma = volatility.BlackVol(maturity, x) * ScaleOnClock(volatility, clockT, maturity, Math.Sqrt);
        double r = RateOnClock(process.RiskFreeRate, clockT, maturity);
        double q = RateOnClock(process.DividendYield, clockT, maturity);

        Date today = context.EvaluationDate;
        if (!(startDate >= today))
        {
            throw new NanoQLException("Seasoned Asian not yet implemented");
        }

        // Expiry in Years
        double t = clockT;
        double t1 = clock.AccrualTimeTo(startDate); // Average Begin
        double t2 = t; // Average End (In this version only Maturity...)

        if ((t2 - t1) < 0.001)
        {
            // its a vanilla option. Use vanilla engine
            var europeanOption = new VanillaOption(payoff, option.Exercise);
            return AnalyticEuropeanEngine.Calculate(europeanOption, context, process).NPV!.Value;
        }

        double theta = 0.5; // Mixed Scheme: 0.5 = Crank Nicolson
        double z0 = ContStrategy(0, t1, t2, q, r) - (Math.Exp(-r * t) * x / s0);
        if (!(z0 >= zMin && z0 <= zMax))
        {
            throw new NanoQLException("spot not on grid");
        }

        double h = (zMax - zMin) / assetSteps; // Space step size
        double k = t / timeSteps; // Time Step size
        double sigma2 = sigma * sigma, vecerTerm;

        var sVec = new Vector(assetSteps + 1);
        var uInitial = new Vector(assetSteps + 1);
        Vector u;
        Vector rhs;
        for (int i = 0; i <= sVec.Size - 1; i++)
        {
            sVec[i] = zMin + (i * h); // Value of Underlying on the grid
        }

        // Begin gamma construction
        var gammaOp = new TridiagonalOperator(assetSteps + 1);
        gammaOp.SetFirstRow(0.0, 0.0);
        gammaOp.SetMidRows(1 / (h * h), -2 / (h * h), 1 / (h * h));
        gammaOp.SetLastRow(0.0, 0.0);
        Vector upperD = gammaOp.UpperDiagonal();
        Vector lowerD = gammaOp.LowerDiagonal();
        Vector dia = gammaOp.Diagonal();

        // Construct Vecer operator
        TridiagonalOperator explicitPart;
        TridiagonalOperator implicitPart;
        for (int i = 0; i <= sVec.Size - 1; i++)
        {
            uInitial[i] = Math.Max(sVec[i], 0.0); // Call Payoff
        }

        u = uInitial;

        // Start Time Loop
        for (int j = 1; j <= timeSteps; j++)
        {
            if (theta != 1.0)
            {
                // Explicit Part
                for (int i = 1; i <= sVec.Size - 2; i++)
                {
                    vecerTerm = sVec[i] - (Math.Exp(-q * (t - ((j - 1) * k))) * ContStrategy(t - ((j - 1) * k), t1, t2, q, r));
                    gammaOp.SetMidRow(
                        i,
                        0.5 * sigma2 * vecerTerm * vecerTerm * lowerD[i - 1],
                        0.5 * sigma2 * vecerTerm * vecerTerm * dia[i],
                        0.5 * sigma2 * vecerTerm * vecerTerm * upperD[i]);
                }

                explicitPart = TridiagonalOperator.Identity(gammaOp.Size) + ((1 - theta) * k * gammaOp);
                explicitPart.SetFirstRow(1.0, 0.0); // Apply before applying
                explicitPart.SetLastRow(-1.0, 1.0); // Neumann BC

                u = explicitPart.ApplyTo(u);

                // Apply after applying (Neumann BC)
                u[assetSteps] = u[assetSteps - 1] + h;
                u[0] = 0;
            } // End Explicit Part

            if (theta != 0.0)
            {
                // Implicit Part
                for (int i = 1; i <= sVec.Size - 2; i++)
                {
                    vecerTerm = sVec[i] - (Math.Exp(-q * (t - (j * k))) * ContStrategy(t - (j * k), t1, t2, q, r));
                    gammaOp.SetMidRow(
                        i,
                        0.5 * sigma2 * vecerTerm * vecerTerm * lowerD[i - 1],
                        0.5 * sigma2 * vecerTerm * vecerTerm * dia[i],
                        0.5 * sigma2 * vecerTerm * vecerTerm * upperD[i]);
                }

                implicitPart = TridiagonalOperator.Identity(gammaOp.Size) - (theta * k * gammaOp);

                // Apply before solving
                implicitPart.SetFirstRow(1.0, 0.0);
                implicitPart.SetLastRow(-1.0, 1.0);
                rhs = u.Clone();
                rhs[0] = 0; // Lower BC
                rhs[assetSteps] = h; // Upper BC (Neumann) Delta=1
                u = implicitPart.SolveFor(rhs);
            } // End implicit Part
        } // End Time Loop

        var rounding = new DownRounding(0);
        int lowerI = (int)rounding.Evaluate((z0 - zMin) / h);

        // Interpolate solution; the last node at the grid's upper end, where QuantLib reads past it (XER5 (xxi))
        double pv = lowerI >= assetSteps
            ? u[assetSteps]
            : u[lowerI] + ((u[lowerI + 1] - u[lowerI]) * (z0 - sVec[lowerI]) / h);

        double value = s0 * pv;

        if (payoff.OptionType == OptionType.Put)
        {
            // Apply Call Put Parity for Asians
            if (r == q)
            {
                expectedAverage = s0;
            }
            else
            {
                expectedAverage = s0 * (Math.Exp((r - q) * t2) - Math.Exp((r - q) * t1)) / ((r - q) * (t2 - t1));
            }

            double asianForward = Math.Exp(-r * t2) * (expectedAverage - x);
            value -= asianForward;
        }

        return value;
    }

    // Replication of Average by holding this amount in Assets
    private static double ContStrategy(double t, double t1, double t2, double v, double r)
    {
        const double eps = 0.00001;
        if (!(t1 <= t2))
        {
            throw new NanoQLException("Average Start must be before Average End");
        }

        if (Math.Abs(t - t2) < eps)
        {
            return 0.0;
        }

        if (t < t1)
        {
            if (Math.Abs(r - v) >= eps)
            {
                return Math.Exp(v * (t - t2)) * (1 - Math.Exp((v - r) * (t2 - t1))) / ((r - v) * (t2 - t1));
            }

            return Math.Exp(v * (t - t2));
        }

        if (Math.Abs(r - v) >= eps)
        {
            return Math.Exp(v * (t - t2)) * (1 - Math.Exp((v - r) * (t2 - t))) / ((r - v) * (t2 - t1));
        }

        return Math.Exp(v * (t - t2)) * (t2 - t) / (t2 - t1);
    }

    // A curve's continuous zero rate to the date on the engine's clock: its own rate times its own time over the
    // clock's, the rate at which it accrues per unit of the clock's time; 0 where it accrues nothing; its own rate where
    // the clock's time is 0
    private static double RateOnClock(YieldTermStructure curve, double clockT, Date date)
    {
        double rate = date <= curve.ReferenceDate
            ? 0.0
            : curve.ZeroRate(date, curve.DayCounter, Compounding.Continuous, Frequency.NoFrequency).Rate;
        return rate * ScaleOnClock(curve, clockT, date, f => f);
    }

    // A structure's own time to the date over the clock's, through f (the square root for a volatility); 1 where the
    // clock's time is 0
    private static double ScaleOnClock(TermStructure structure, double clockT, Date date, Func<double, double> f)
    {
        if (clockT == 0.0)
        {
            return 1.0;
        }

        double own = structure.AccrualTimeTo(date);
        return own == clockT ? 1.0 : f(own / clockT);
    }
}
