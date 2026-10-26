// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/lookback/mclookbackengine.hpp ql/pricingengines/lookback/mclookbackengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo pricing engine for the four continuous lookback options, the extremum read at the path's points.
/// </summary>
/// <remarks>
/// <para>
/// A static class with one <c>Calculate</c> overload per lookback instrument, QuantLib's template parameter <c>I</c>
/// and its four pricer overloads, over one private simulation deriving <see cref="McSimulation{TPath, TStats}"/>
/// directly (the S-bar plan's BR1 (ii)); QuantLib's four <c>.cpp</c>-local path pricers are two private classes here, a
/// partial pricer the full one over its window's nodes. The full-period overloads refuse a partial-time option held as
/// the full-period type with QuantLib's "wrong argument type" (SD8). The seed is explicit (S2R4).
/// </para>
/// <para>
/// Documented divergences. The paths and the discount run on the engine's clock, <see cref="FdmBlackScholesOp.Clock"/>,
/// from the earliest reference date of the structures, where QuantLib's run on the risk-free curve's (BR5 (xix),
/// <c>Divergences/McLookbackMismatchedTimesTests.cs</c>). The extremum observed so far is folded into the path's, where
/// QuantLib's pricers take the extremum from the path's second node alone and price a seasoned option as a new one (BR5
/// (xx), <c>Divergences/McLookbackSeasonedTests.cs</c>), and it is read as the spot where it lies beyond it, the spot
/// being observed (BR5 (xxii)). A partial option's window date is a time of the grid, its start node included, where
/// QuantLib's pricers snap it to the closest time of a regular grid and the partial fixed pricer drops the window's
/// first node (BR5 (xx), <c>Divergences/McLookbackWindowTests.cs</c>). A partial fixed window opened before the
/// evaluation date is refused, as the analytic engine refuses it, where QuantLib's pricer starts it today, and a
/// partial floating window closed before the evaluation date takes the extremum as given, neither the spot nor a path's
/// node folded in, where QuantLib's folds today's spot into it; a window dated from the evaluation date to the clock's
/// origin opens or closes at the path's start (the S-bar code review's B2-2 and B2-3,
/// <c>Divergences/LookbackPartialWindowTodayTests.cs</c>). And the partial floating option pays the terminal spot less
/// λ times the minimum, or λ times the maximum less the terminal spot, as the instrument and the analytic engine have
/// it, where QuantLib's pricer drops λ (BR5 (xxi), <c>Divergences/McLookbackLambdaTests.cs</c>).
/// </para>
/// </remarks>
public static class MCLookbackEngine
{
    /// <summary>Prices a continuous fixed-strike lookback option by Monte Carlo.</summary> <typeparam name="TRng">The
    /// random-number traits.</typeparam> <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option, not a partial-time one.</param> <param name="context">The evaluation
    /// context.</param> <param name="process">The Black-Scholes process.</param> <param name="timeSteps">The number of
    /// time steps, or <see langword="null"/> for steps per year.</param> <param name="timeStepsPerYear">The number of
    /// time steps per year, or <see langword="null"/> for a number of steps.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param> <param name="requiredSamples">The
    /// number of samples, or <see langword="null"/>.</param> <param name="requiredTolerance">The absolute tolerance, or
    /// <see langword="null"/>; it takes precedence.</param> <param name="maxSamples">The most samples for a tolerance,
    /// or <see langword="null"/> for no limit.</param> <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has
    /// expired.</returns> <exception cref="ArgumentNullException">The option, the context or the process is
    /// <see langword="null"/>.</exception> <exception cref="ArgumentException">The time steps are given neither or both
    /// ways, or aren't positive.</exception> <exception cref="ArgumentOutOfRangeException">A number of samples is
    /// negative, or the seed is outside its range.</exception> <exception cref="NanoQLException"> The option is a
    /// partial-time one ("wrong argument type"), its terms fail its validation, the payoff isn't plain vanilla or its
    /// strike is negative, the spot isn't positive, or the samples can't reach the tolerance. </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        ContinuousFixedLookbackOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option is ContinuousPartialFixedLookbackOption)
        {
            throw new NanoQLException("wrong argument type");
        }

        return Price<TRng, TStats>(
            option,
            context,
            process,
            null,
            (grid, discount) =>
            {
                PlainVanillaPayoff payoff = PlainPayoff(option);
                double spot = process.X0;
                double minmax = payoff.OptionType == OptionType.Call
                    ? Math.Max(option.Minmax, spot)
                    : Math.Min(option.Minmax, spot);
                return new FixedPathPricer(payoff.OptionType, payoff.Strike, minmax, 1, discount);
            },
            timeSteps,
            timeStepsPerYear,
            brownianBridge,
            antitheticVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed);
    }

    /// <summary>Prices a continuous partial-time fixed-strike lookback option by Monte Carlo.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam> <typeparam name="TStats">The statistics
    /// accumulator.</typeparam> <param name="option">The option.</param> <param name="context">The evaluation
    /// context.</param> <param name="process">The Black-Scholes process.</param> <param name="timeSteps">The number of
    /// time steps, or <see langword="null"/> for steps per year.</param> <param name="timeStepsPerYear">The number of
    /// time steps per year, or <see langword="null"/> for a number of steps.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param> <param name="requiredSamples">The
    /// number of samples, or <see langword="null"/>.</param> <param name="requiredTolerance">The absolute tolerance, or
    /// <see langword="null"/>; it takes precedence.</param> <param name="maxSamples">The most samples for a tolerance,
    /// or <see langword="null"/> for no limit.</param> <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has
    /// expired.</returns> <exception cref="ArgumentNullException">The option, the context or the process is
    /// <see langword="null"/>.</exception> <exception cref="ArgumentException">The time steps are given neither or both
    /// ways, or aren't positive.</exception> <exception cref="ArgumentOutOfRangeException">A number of samples is
    /// negative, or the seed is outside its range.</exception> <exception cref="NanoQLException"> The option's terms
    /// fail its validation, the payoff isn't plain vanilla or its strike is negative, the spot isn't positive, the
    /// lookback period started before the evaluation date, or the samples can't reach the tolerance. </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        ContinuousPartialFixedLookbackOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(option);
        return Price<TRng, TStats>(
            option,
            context,
            process,
            option.LookbackPeriodStart,
            (grid, discount) =>
            {
                // the window from its start node on, the start a time of the grid (BR5 (xx)), from the first node where
                // it opens from the evaluation date to the clock's origin; a window opened before the evaluation date
                // refused, the option holding no extremum observed since (the S-bar code review's B2-2 and B2-3), where
                // QuantLib's prices it as opening today
                PlainVanillaPayoff payoff = PlainPayoff(option);
                if (option.LookbackPeriodStart < context.EvaluationDate)
                {
                    throw new NanoQLException(
                        "lookback period started before the evaluation date: no extremum observed since its start is given");
                }

                return new FixedPathPricer(payoff.OptionType, payoff.Strike, double.NaN, grid.WindowIndex, discount);
            },
            timeSteps,
            timeStepsPerYear,
            brownianBridge,
            antitheticVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed);
    }

    /// <summary>Prices a continuous floating-strike lookback option by Monte Carlo.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam> <typeparam name="TStats">The statistics
    /// accumulator.</typeparam> <param name="option">The option, not a partial-time one.</param>
    /// <param name="context">The evaluation context.</param> <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/> for a number of
    /// steps.</param> <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param> <param name="requiredSamples">The
    /// number of samples, or <see langword="null"/>.</param> <param name="requiredTolerance">The absolute tolerance, or
    /// <see langword="null"/>; it takes precedence.</param> <param name="maxSamples">The most samples for a tolerance,
    /// or <see langword="null"/> for no limit.</param> <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has
    /// expired.</returns> <exception cref="ArgumentNullException">The option, the context or the process is
    /// <see langword="null"/>.</exception> <exception cref="ArgumentException">The time steps are given neither or both
    /// ways, or aren't positive.</exception> <exception cref="ArgumentOutOfRangeException">A number of samples is
    /// negative, or the seed is outside its range.</exception> <exception cref="NanoQLException"> The option is a
    /// partial-time one ("wrong argument type"), its terms fail its validation, the payoff isn't floating, the spot
    /// isn't positive, or the samples can't reach the tolerance. </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        ContinuousFloatingLookbackOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option is ContinuousPartialFloatingLookbackOption)
        {
            throw new NanoQLException("wrong argument type");
        }

        return Price<TRng, TStats>(
            option,
            context,
            process,
            null,
            (grid, discount) =>
            {
                FloatingTypePayoff payoff = FloatingPayoff(option);
                double spot = process.X0;
                double minmax = payoff.OptionType == OptionType.Call
                    ? Math.Min(option.Minmax, spot)
                    : Math.Max(option.Minmax, spot);
                return new FloatingPathPricer(payoff.OptionType, minmax, 1.0, grid.Size - 1, discount);
            },
            timeSteps,
            timeStepsPerYear,
            brownianBridge,
            antitheticVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed);
    }

    /// <summary>Prices a continuous partial-time floating-strike lookback option by Monte Carlo.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam> <typeparam name="TStats">The statistics
    /// accumulator.</typeparam> <param name="option">The option.</param> <param name="context">The evaluation
    /// context.</param> <param name="process">The Black-Scholes process.</param> <param name="timeSteps">The number of
    /// time steps, or <see langword="null"/> for steps per year.</param> <param name="timeStepsPerYear">The number of
    /// time steps per year, or <see langword="null"/> for a number of steps.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param> <param name="requiredSamples">The
    /// number of samples, or <see langword="null"/>.</param> <param name="requiredTolerance">The absolute tolerance, or
    /// <see langword="null"/>; it takes precedence.</param> <param name="maxSamples">The most samples for a tolerance,
    /// or <see langword="null"/> for no limit.</param> <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has
    /// expired.</returns> <exception cref="ArgumentNullException">The option, the context or the process is
    /// <see langword="null"/>.</exception> <exception cref="ArgumentException">The time steps are given neither or both
    /// ways, or aren't positive.</exception> <exception cref="ArgumentOutOfRangeException">A number of samples is
    /// negative, or the seed is outside its range.</exception> <exception cref="NanoQLException"> The option's terms
    /// fail its validation, the payoff isn't floating, the spot isn't positive, or the samples can't reach the
    /// tolerance. </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        ContinuousPartialFloatingLookbackOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(option);
        return Price<TRng, TStats>(
            option,
            context,
            process,
            option.LookbackPeriodEnd,
            (grid, discount) =>
            {
                // the window to its end node, the end a time of the grid (BR5 (xx)), λ in the payoff (BR5 (xxi)); a
                // window closed before the evaluation date its extremum as given, neither the spot nor a path's node in
                // it (the S-bar code review's B2-2 and B2-3), where QuantLib's folds the spot in
                FloatingTypePayoff payoff = FloatingPayoff(option);
                double spot = process.X0;
                bool closed = option.LookbackPeriodEnd < context.EvaluationDate;
                double minmax = closed
                    ? option.Minmax
                    : payoff.OptionType == OptionType.Call ? Math.Min(option.Minmax, spot) : Math.Max(option.Minmax, spot);
                return new FloatingPathPricer(payoff.OptionType, minmax, option.Lambda, closed ? 0 : grid.WindowIndex, discount);
            },
            timeSteps,
            timeStepsPerYear,
            brownianBridge,
            antitheticVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed);
    }

    private static OneAssetOptionResults Price<TRng, TStats>(
        OneAssetOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        Date? window,
        Func<Grid, double, PathPricer<MonteCarloPath>> pricer,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        var engine = new Simulation<TRng, TStats>(
            option,
            process,
            FdmBlackScholesOp.Clock(process),
            window,
            pricer,
            timeSteps,
            timeStepsPerYear,
            brownianBridge,
            antitheticVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(engine.Price());
    }

    private static PlainVanillaPayoff PlainPayoff(OneAssetOption option) =>
        option.Payoff as PlainVanillaPayoff ?? throw new NanoQLException("non-plain payoff given");

    private static FloatingTypePayoff FloatingPayoff(OneAssetOption option) =>
        option.Payoff as FloatingTypePayoff ?? throw new NanoQLException("non-floating payoff given");

    // the simulation's grid and the index of the window's date on it (0 for a full-period option)
    private readonly record struct Grid(NanoQL.TimeGrid TimeGrid, int WindowIndex)
    {
        public int Size => TimeGrid.Size;
    }

    // QuantLib's engine object over one call's option, on the process's clock.
    private sealed class Simulation<TRng, TStats> : McSimulation<MonteCarloPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly OneAssetOption option;
        private readonly GeneralizedBlackScholesProcess process;
        private readonly StochasticProcess1D clockedProcess;
        private readonly TermStructure clock;
        private readonly Date? window;
        private readonly Func<Grid, double, PathPricer<MonteCarloPath>> pricer;
        private readonly int? timeSteps;
        private readonly int? timeStepsPerYear;
        private readonly bool brownianBridge;
        private readonly int? requiredSamples;
        private readonly double? requiredTolerance;
        private readonly int? maxSamples;
        private readonly long seed;

        public Simulation(
            OneAssetOption option,
            GeneralizedBlackScholesProcess process,
            TermStructure clock,
            Date? window,
            Func<Grid, double, PathPricer<MonteCarloPath>> pricer,
            int? timeSteps,
            int? timeStepsPerYear,
            bool brownianBridge,
            bool antitheticVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed)
            : base(antitheticVariate, false)
        {
            MCVanillaEngine<SingleVariate<TRng>, MonteCarloPath, TStats>.CheckSteps(timeSteps, timeStepsPerYear);
            MCVanillaEngine<SingleVariate<TRng>, MonteCarloPath, TStats>.CheckSamples(requiredSamples, maxSamples);
            Seeds.Check(seed);
            this.option = option;
            this.process = process;
            this.clock = clock;
            clockedProcess = process.OnClock(clock);
            this.window = window;
            this.pricer = pricer;
            this.timeSteps = timeSteps;
            this.timeStepsPerYear = timeStepsPerYear;
            this.brownianBridge = brownianBridge;
            this.requiredSamples = requiredSamples;
            this.requiredTolerance = requiredTolerance;
            this.maxSamples = maxSamples;
            this.seed = seed;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Price()
        {
            double spot = process.X0;
            if (!(spot > 0.0))
            {
                throw new NanoQLException("negative or null underlying given");
            }

            MonteCarloModel<MonteCarloPath, TStats> model = Calculate(requiredTolerance, requiredSamples, maxSamples ?? int.MaxValue);
            return new OneAssetOptionResults
            {
                NPV = model.SampleAccumulator.Mean(),
                ErrorEstimate = TRng.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
            };
        }

        protected override NanoQL.TimeGrid TimeGrid() => WindowedGrid().TimeGrid;

        protected override IPathGenerator<MonteCarloPath> PathGenerator()
        {
            NanoQL.TimeGrid grid = TimeGrid();
            IRsg gen = SingleVariate<TRng>.MakeSequenceGenerator(grid.Size - 1, seed);
            return SingleVariate<TRng>.MakePathGenerator(clockedProcess, grid, gen, brownianBridge);
        }

        protected override PathPricer<MonteCarloPath> PathPricer()
        {
            Grid grid = WindowedGrid();
            double discount = process.RiskFreeRate.DiscountAt(clock, grid.TimeGrid.Back());
            return pricer(grid, discount);
        }

        // QuantLib's grid to the exercise on the clock, a partial option's window date a time of it (BR5 (xx))
        private Grid WindowedGrid()
        {
            double residualTime = clockedProcess.Time(option.Exercise.LastDate());
            int steps;
            if (timeSteps is int s)
            {
                steps = s;
            }
            else if (timeStepsPerYear is int perYear)
            {
                steps = Math.Max((int)(perYear * residualTime), 1);
            }
            else
            {
                throw new NanoQLException("time steps not specified");
            }

            double windowTime = window is Date w ? clockedProcess.Time(w) : 0.0;
            if (!(windowTime > 0.0 && windowTime < residualTime))
            {
                var regular = new NanoQL.TimeGrid(residualTime, steps);
                return new Grid(regular, windowTime >= residualTime ? regular.Size - 1 : 0);
            }

            var grid = new NanoQL.TimeGrid([windowTime, residualTime], steps);
            return new Grid(grid, grid.ClosestIndex(windowTime));
        }
    }

    // A fixed-strike pricer: the extremum of the path from a node on, folded with a value observed before it (NaN for
    // none), QuantLib's LookbackFixedPathPricer and LookbackPartialFixedPathPricer.
    private sealed class FixedPathPricer : PathPricer<MonteCarloPath>
    {
        private readonly PlainVanillaPayoff payoff;
        private readonly double observed;
        private readonly int first;
        private readonly double discount;

        public FixedPathPricer(OptionType type, double strike, double observed, int first, double discount)
        {
            if (!(strike >= 0.0))
            {
                throw new NanoQLException("strike less than zero not allowed");
            }

            payoff = new PlainVanillaPayoff(type, strike);
            this.observed = observed;
            this.first = first;
            this.discount = discount;
        }

        public override double Evaluate(MonteCarloPath path)
        {
            ArgumentNullException.ThrowIfNull(path);
            int n = path.Length;
            if (!(n > 0))
            {
                throw new NanoQLException("the path cannot be empty");
            }

            bool call = payoff.OptionType == OptionType.Call;
            double extremum = observed;
            for (int i = first; i < n; ++i)
            {
                double x = path[i];
                if (double.IsNaN(extremum) || (call ? x > extremum : x < extremum))
                {
                    extremum = x;
                }
            }

            return payoff.Evaluate(extremum) * discount;
        }
    }

    // A floating-strike pricer: the extremum of the path from its second node to a node, folded with the one observed
    // so far, λ times it against the terminal spot; QuantLib's LookbackFloatingPathPricer and
    // LookbackPartialFloatingPathPricer.
    private sealed class FloatingPathPricer : PathPricer<MonteCarloPath>
    {
        private readonly OptionType type;
        private readonly double observed;
        private readonly double lambda;
        private readonly int last;
        private readonly double discount;

        public FloatingPathPricer(OptionType type, double observed, double lambda, int last, double discount)
        {
            this.type = type;
            this.observed = observed;
            this.lambda = lambda;
            this.last = last;
            this.discount = discount;
        }

        public override double Evaluate(MonteCarloPath path)
        {
            ArgumentNullException.ThrowIfNull(path);
            int n = path.Length;
            if (!(n > 0))
            {
                throw new NanoQLException("the path cannot be empty");
            }

            bool call = type == OptionType.Call;
            double extremum = observed;
            int end = Math.Min(last, n - 1);
            for (int i = 1; i <= end; ++i)
            {
                double x = path[i];
                if (call ? x < extremum : x > extremum)
                {
                    extremum = x;
                }
            }

            double terminal = path[n - 1];
            double value = call ? terminal - (lambda * extremum) : (lambda * extremum) - terminal;
            return Math.Max(value, 0.0) * discount;
        }
    }
}
