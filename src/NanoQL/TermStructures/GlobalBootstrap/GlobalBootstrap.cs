// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2025 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/globalbootstrap.hpp ql/termstructures/globalbootstrap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The global bootstrapper, with additional restrictions: the curve's nodes are solved together, by an optimizer that
/// minimizes the helpers' quote errors and any additional penalties.
/// </summary>
/// <remarks>
/// <para>
/// A configuration struct (<see cref="IBootstrap"/>): its zero value is QuantLib's default construction. QuantLib's
/// three constructors are one, with optional parameters. An optimizer or end criteria not given is QuantLib's default,
/// built when a curve is built: a <see cref="LevenbergMarquardt"/> method and <see cref="EndCriteria"/> of 1000
/// iterations and 10 stationary ones, each tolerance the accuracy, which is the curve's when not given.
/// </para>
/// <para>
/// The curve's nodes are its first date, the pillars of its helpers alive at that date and the additional dates after
/// it, sorted, each once. The additional dates are a collection, where QuantLib's are a functor of the evaluation date,
/// since a curve is built once. The error terms are the alive helpers' quote errors, each times its instrument weight,
/// then the additional penalties: together they must be at least as many as the nodes after the first. The additional
/// helpers are bound to the curve as its helpers are, but add no pillar or error term of their own: the penalties
/// price them. The penalties and the initial-guess function receive the curve's working state
/// (<see cref="IBootstrapState"/>), whose helpers and additional helpers are bound to the curve, and the context, where
/// QuantLib's reach the helpers through the user's own pointers, which the bootstrap has linked to the curve. In a
/// multicurve the members' curves are set up in the order they were added, before any is optimized, so a guess function
/// that prices a helper on a bootstrapped member added after its own curve, or on a curve that reads one, meets that
/// member's refusal, "curve not set up yet"; the penalties run once every member is set up. QuantLib's guess function
/// takes the times and data, and prices nothing.
/// </para>
/// <para>
/// The additional variables (<see cref="AdditionalVariables"/>) are optimized beside the nodes, the argument's values
/// for the nodes first and then the variables', from their guesses (the S-core plan's CR3 (b)). The helpers that read
/// them are built by <see cref="VariableHelpers"/> from their values: once at their guesses when the curve is built,
/// which gives their pillars and latest relevant dates, and again at each evaluation of the cost function, as
/// QuantLib's helpers read their quotes then. They follow the curve's helpers, in the order the function returns them:
/// the alive ones add their pillars and their quote errors, after the curve's helpers' and before the penalties, and
/// the instrument weights cover the curve's helpers and then these, one weight each, as QuantLib's error vector reads
/// its instruments in their order, the variables' helpers among them. A later build whose helpers' pillars differ from
/// the first's is refused, as QuantLib fixes the pillars when the bootstrap starts. The curve keeps the variables'
/// values at the optimum (<see cref="BootstrapState{TTermStructure}.AdditionalVariables"/>), where QuantLib leaves them
/// in its quotes.
/// </para>
/// <para>
/// The guess is the initial-guess function's when given, which is called once per curve built and sees no data, as
/// QuantLib's sees none on a curve with no previous state; otherwise the traits' guess for the whole curve
/// (<see cref="IBootstrapTraits{TTermStructure}.GlobalGuess"/>) when they give one, or each node's guess in turn. The
/// traits' transforms (<see cref="IBootstrapTraits{TTermStructure}.TransformDirect"/> and its inverse), when they have
/// them, map the nodes to the unconstrained values the optimizer works on. The curve's nodes are the optimizer's last
/// evaluation, its optimum.
/// </para>
/// <para>
/// QuantLib builds the target interpolation over every node without updating it, and refuses, with no fallback to
/// linear interpolation, a configuration its construction refuses for that many points
/// (<see cref="IInterpolator.CheckPoints"/>); here it is then built over the first data, so a guess function that
/// prices a helper reads the curve at the traits' initial value, where QuantLib's reads an interpolation it hasn't
/// updated. A curve is always built afresh, so its bootstrap starts from the guess, where QuantLib re-bootstraps a
/// curve whose quotes changed from its previous state: a documented divergence.
/// </para>
/// </remarks>
public readonly struct GlobalBootstrap : IBootstrap, IMultiCurveBootstrap
{
    // Stored so that zero reads as QuantLib's defaults: a null accuracy is QuantLib's Null<Real>(), a null optimizer or
    // end criteria is built when a curve is, and empty collections and null functions are none.
    private readonly double? accuracy;
    private readonly OptimizationMethod? optimizer;
    private readonly EndCriteria? endCriteria;
    private readonly ImmutableArray<double> instrumentWeights;
    private readonly ImmutableArray<IBootstrapHelper> additionalHelpers;
    private readonly ImmutableArray<Date> additionalDates;
    private readonly Func<IBootstrapState, EvaluationContext, Vector>? additionalPenalties;
    private readonly Func<IBootstrapState, EvaluationContext, Vector>? initialGuessFn;
    private readonly AdditionalBootstrapVariables? additionalVariables;
    private readonly Func<ImmutableArray<double>, EvaluationContext, ImmutableArray<IBootstrapHelper>>? variableHelpers;

    /// <summary>Initializes a new instance of the <see cref="GlobalBootstrap"/> struct.</summary>
    /// <param name="accuracy">
    /// The accuracy of the default optimizer and end criteria; <see langword="null"/> takes the curve's.
    /// </param>
    /// <param name="optimizer">
    /// The optimization method; <see langword="null"/> for Levenberg–Marquardt at the accuracy.
    /// </param>
    /// <param name="endCriteria">
    /// The end criteria; <see langword="null"/> for 1000 iterations and 10 stationary ones at the accuracy.
    /// </param>
    /// <param name="instrumentWeights">
    /// The weights of the curve's helpers' quote errors, one per helper, alive or not; <see langword="null"/> or empty for
    /// weights of 1.
    /// </param>
    /// <param name="additionalHelpers">
    /// Helpers bound to the curve that add no pillar or error term of their own, for the penalties to price; of the
    /// curve's kind of term structure.
    /// </param>
    /// <param name="additionalDates">Dates added to the curve's nodes; those not after the curve's first date are dropped.</param>
    /// <param name="additionalPenalties">
    /// The additional error terms, from the curve's working state and the context, after the helpers' quote errors.
    /// </param>
    /// <param name="initialGuessFn">
    /// The guess for the nodes after the first, from the curve's working state (with no data) and the context, before the
    /// traits' inverse transform; it must give a value for every such node. In a multicurve, a helper it prices can't
    /// reference a bootstrapped member added after its own curve, or a curve that reads one, whose curve isn't set up
    /// yet.
    /// </param>
    /// <param name="additionalVariables">The variables optimized beside the nodes, or <see langword="null"/> for
    /// none.</param>
    /// <param name="variableHelpers">
    /// The helpers that read the variables, from their values and the context, after the curve's helpers; of the
    /// curve's kind of term structure. <see langword="null"/> for none.
    /// </param>
    /// <exception cref="ArgumentException">
    /// An additional helper is <see langword="null"/>, or an additional date is <c>default(Date)</c>.
    /// </exception>
    public GlobalBootstrap(
        double? accuracy = null,
        OptimizationMethod? optimizer = null,
        EndCriteria? endCriteria = null,
        IEnumerable<double>? instrumentWeights = null,
        IEnumerable<IBootstrapHelper>? additionalHelpers = null,
        IEnumerable<Date>? additionalDates = null,
        Func<IBootstrapState, EvaluationContext, Vector>? additionalPenalties = null,
        Func<IBootstrapState, EvaluationContext, Vector>? initialGuessFn = null,
        AdditionalBootstrapVariables? additionalVariables = null,
        Func<ImmutableArray<double>, EvaluationContext, ImmutableArray<IBootstrapHelper>>? variableHelpers = null)
    {
        this.accuracy = accuracy;
        this.optimizer = optimizer;
        this.endCriteria = endCriteria;
        this.instrumentWeights = instrumentWeights is null ? default : [.. instrumentWeights];
        if (additionalHelpers is not null)
        {
            ImmutableArray<IBootstrapHelper>.Builder helpers = ImmutableArray.CreateBuilder<IBootstrapHelper>();
            foreach (IBootstrapHelper helper in additionalHelpers)
            {
                if (helper is null)
                {
                    throw new ArgumentException("null rate helper", nameof(additionalHelpers));
                }

                helpers.Add(helper);
            }

            this.additionalHelpers = helpers.ToImmutable();
        }

        if (additionalDates is not null)
        {
            ImmutableArray<Date> dates = [.. additionalDates];
            foreach (Date d in dates)
            {
                Date.ThrowIfDefault(d, nameof(additionalDates));
            }

            this.additionalDates = dates;
        }

        this.additionalPenalties = additionalPenalties;
        this.initialGuessFn = initialGuessFn;
        this.additionalVariables = additionalVariables;
        this.variableHelpers = variableHelpers;
    }

    /// <summary>Gets the variables optimized beside the nodes, or <see langword="null"/> for none.</summary>
    public AdditionalBootstrapVariables? AdditionalVariables => additionalVariables;

    /// <summary>
    /// Gets the function building the helpers that read the variables, from their values and the context, or
    /// <see langword="null"/> for none.
    /// </summary>
    public Func<ImmutableArray<double>, EvaluationContext, ImmutableArray<IBootstrapHelper>>? VariableHelpers =>
        variableHelpers;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The instrument weights don't match the curve's helpers and the variables' helpers in number, the curve has fewer
    /// nodes than the interpolation requires, the guess function gives fewer values than the curve has nodes after the
    /// first, the variables moved a helper's pillar, or the optimizer fails to minimize to the required accuracy.
    /// </exception>
    /// <exception cref="InvalidCastException">
    /// An additional helper or a variable helper isn't a helper of the curve's kind of term structure.
    /// </exception>
    public void Calculate<TTermStructure, TTraits, TInterpolator>(
        BootstrapState<TTermStructure> state, TInterpolator interpolator, EvaluationContext context)
        where TTermStructure : TermStructure
        where TTraits : struct, IBootstrapTraits<TTermStructure>
        where TInterpolator : struct, IInterpolator
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        var contributor = new Contributor<TTermStructure, TTraits, TInterpolator>(this, state, interpolator, context, null);

        // single curve bootstrap
        Vector guess = contributor.SetupCostFunction();

        var costFunction = new SimpleCostFunction(x =>
        {
            contributor.SetCostFunctionArgument(x.AsSpan());
            return contributor.EvaluateCostFunction();
        });

        var problem = new Problem(costFunction, new NoConstraint(), guess.AsSpan());
        EndCriteriaType endType = contributor.Optimizer.Minimize(problem, contributor.Criteria).EndCriteriaType;
        if (!EndCriteria.Succeeded(endType))
        {
            throw new NanoQLException($"global bootstrap failed to minimize to required accuracy: {endType}");
        }
    }

    /// <inheritdoc/>
    MultiCurveBootstrapContributor IMultiCurveBootstrap.Contributor<TTermStructure, TTraits, TInterpolator>(
        BootstrapState<TTermStructure> state, TInterpolator interpolator, EvaluationContext context, Action setToValid) =>
        new Contributor<TTermStructure, TTraits, TInterpolator>(this, state, interpolator, context, setToValid);

    // A date QuantLib compares as later than another: a null date is QuantLib's Date(), earlier than every date.
    private static bool Later(Date? d, Date than) => d is Date date && date > than;

    // std::max(a, b), a null date being QuantLib's Date(), earlier than every date.
    private static Date Max(Date a, Date? b) => b is Date date && a < date ? date : a;

    // QuantLib's GlobalBootstrap<Curve> at work on one curve: its setup (the defaults and the weights' check) when it is
    // built, then its part of the cost function, which a single curve's optimization and a multicurve's both run.
    private sealed class Contributor<TTermStructure, TTraits, TInterpolator> : MultiCurveBootstrapContributor
        where TTermStructure : TermStructure
        where TTraits : struct, IBootstrapTraits<TTermStructure>
        where TInterpolator : struct, IInterpolator
    {
        private readonly GlobalBootstrap bootstrap;
        private readonly BootstrapState<TTermStructure> s;
        private readonly TInterpolator interpolator;
        private readonly EvaluationContext context;
        private readonly double[] instrumentWeights;
        private readonly Action? setToValid;
        private BootstrapHelper<TTermStructure>[] aliveInstruments = [];
        private double[] aliveInstrumentWeights = [];

        // The variables' helpers, after the curve's: built at the variables' guesses when the curve is, and again at
        // each argument; the alive ones' indices, and their pillars at the first build, which later builds must keep.
        private BootstrapHelper<TTermStructure>[] builtVariableHelpers;
        private int[] aliveVariableHelpers = [];
        private Date?[] variablePillars = [];

        public Contributor(
            GlobalBootstrap bootstrap,
            BootstrapState<TTermStructure> state,
            TInterpolator interpolator,
            EvaluationContext context,
            Action? setToValid)
        {
            this.bootstrap = bootstrap;
            s = state;
            this.interpolator = interpolator;
            this.context = context;
            this.setToValid = setToValid;

            // setup optimizer and EndCriteria
            double acc = bootstrap.accuracy ?? s.Accuracy;
            Optimizer = bootstrap.optimizer ?? new LevenbergMarquardt(acc, acc, acc);
            Criteria = bootstrap.endCriteria ?? new EndCriteria(1000, 10, acc, acc, acc);

            // the variables' helpers at the variables' guesses, after the curve's helpers, among which QuantLib's curve
            // holds them
            builtVariableHelpers = BuildVariableHelpers(GuessValues());

            // check number of instrument weights
            ImmutableArray<double> weights = bootstrap.instrumentWeights.IsDefault ? [] : bootstrap.instrumentWeights;
            int n = s.Instruments.Length + builtVariableHelpers.Length;
            if (!(weights.IsEmpty || weights.Length == n))
            {
                throw new NanoQLException(
                    $"GlobalBootstrap: number of instrument weights ({weights.Length}) must match number of instruments ({n})");
            }

            instrumentWeights = new double[n];
            Array.Fill(instrumentWeights, 1.0);
            weights.CopyTo(instrumentWeights);

            // do not initialize yet: instruments could be invalid here
            // but valid later when bootstrapping is actually required
        }

        public OptimizationMethod Optimizer { get; }

        public EndCriteria Criteria { get; }

        public override Vector SetupCostFunction()
        {
            Date firstDate = TTraits.InitialDate(s);

            // set up alive instruments and weights
            var alive = new List<int>();
            for (int i = 0; i < s.Instruments.Length; ++i)
            {
                if (Later(s.Instruments[i].PillarDate, firstDate))
                {
                    alive.Add(i);
                }
            }

            // and the variables' alive helpers, after the curve's
            var aliveVariable = new List<int>();
            for (int k = 0; k < builtVariableHelpers.Length; ++k)
            {
                if (Later(builtVariableHelpers[k].PillarDate, firstDate))
                {
                    aliveVariable.Add(k);
                }
            }

            aliveVariableHelpers = [.. aliveVariable];
            variablePillars = [.. builtVariableHelpers.Select(h => h.PillarDate)];

            // set up alive additional helpers, each re-dated for the curve's evaluation date as the curve's helpers are
            ImmutableArray<IBootstrapHelper> given = bootstrap.additionalHelpers.IsDefault ? [] : bootstrap.additionalHelpers;
            var additional = new BootstrapHelper<TTermStructure>[given.Length];
            var aliveAdditional = new List<int>();
            for (int i = 0; i < given.Length; ++i)
            {
                additional[i] = ((BootstrapHelper<TTermStructure>)given[i]).RedatedFor(s.Context);
                if (Later(additional[i].PillarDate, firstDate))
                {
                    aliveAdditional.Add(i);
                }
            }

            // calculate dates: first populate the dates vector, with the additional dates not expired, and make sure
            // they are sorted and unique
            var dates = new List<Date> { firstDate };
            foreach (int i in alive)
            {
                dates.Add(s.Instruments[i].PillarDate!.Value);
            }

            foreach (int k in aliveVariableHelpers)
            {
                dates.Add(builtVariableHelpers[k].PillarDate!.Value);
            }

            foreach (Date d in bootstrap.additionalDates.IsDefault ? [] : bootstrap.additionalDates)
            {
                if (d > firstDate)
                {
                    dates.Add(d);
                }
            }

            dates.Sort();
            var unique = new List<Date>(dates.Count);
            foreach (Date d in dates)
            {
                if (unique.Count == 0 || unique[^1] != d)
                {
                    unique.Add(d);
                }
            }

            // check if there are enough interpolation points
            if (!(unique.Count >= TInterpolator.RequiredPoints))
            {
                throw new NanoQLException(
                    $"GlobalBootstrap: not enough curve points ({unique.Count}) for interpolation requiring at least "
                    + $"{TInterpolator.RequiredPoints}");
            }

            // build times vector
            s.Dates = [.. unique];
            s.Times = [.. unique.Select(s.Curve.TimeFromReference)];

            // determine maxDate ensuring all instruments and additional helpers are covered
            Date maxDate = unique[^1];
            foreach (int i in alive)
            {
                maxDate = Max(maxDate, s.Instruments[i].LatestRelevantDate);
            }

            foreach (int k in aliveVariableHelpers)
            {
                maxDate = Max(maxDate, builtVariableHelpers[k].LatestRelevantDate);
            }

            foreach (int i in aliveAdditional)
            {
                maxDate = Max(maxDate, additional[i].LatestRelevantDate);
            }

            s.MaxDate = maxDate;

            // set the initial guess: the curve is built afresh. data[0] is the only relevant item, but reasonable
            // numbers might be needed for the whole data vector because, e.g., of interpolation's early checks
            s.Data = new double[unique.Count];
            Array.Fill(s.Data, TTraits.InitialValue(s));

            // setup helpers: QuantLib's check for a valid quote has no counterpart, a quote being a value
            aliveInstruments = new BootstrapHelper<TTermStructure>[alive.Count];
            aliveInstrumentWeights = new double[alive.Count + aliveVariableHelpers.Length];
            for (int k = 0; k < alive.Count; ++k)
            {
                int i = alive[k];
                s.Instruments[i] = s.Bind(s.Instruments[i]);
                aliveInstruments[k] = s.Instruments[i];
                aliveInstrumentWeights[k] = instrumentWeights[i];
            }

            for (int k = 0; k < aliveVariableHelpers.Length; ++k)
            {
                int i = aliveVariableHelpers[k];
                builtVariableHelpers[i] = s.Bind(builtVariableHelpers[i]);
                aliveInstrumentWeights[alive.Count + k] = instrumentWeights[s.Instruments.Length + i];
            }

            // setup additional helpers
            foreach (int i in aliveAdditional)
            {
                additional[i] = s.Bind(additional[i]);
            }

            s.AdditionalHelpers = additional;

            // setup interpolation: QuantLib's interpolateWithoutUpdate over every node, whose construction refuses here,
            // with no fallback; it is built with the first data at once
            s.Points = s.Times.Length;
            s.LinearFallback = false;
            interpolator.CheckPoints(s.Points);
            s.Rebuild(interpolator);

            // Initial guess. We have guesses for the curve values first (numberPillars), followed by guesses for the
            // additional variables.
            Vector guess = MakeInitialGuess();
            s.AdditionalVariables = GuessValues();
            if (bootstrap.additionalVariables is { } variables)
            {
                Vector additionalGuesses = variables.Initialize(false);
                var all = new Vector(guess.Size + additionalGuesses.Size);
                guess.AsSpan().CopyTo(all.AsSpan());
                additionalGuesses.AsSpan().CopyTo(all.AsSpan().Slice(guess.Size));
                guess = all;
            }

            return guess;
        }

        public override void SetCostFunctionArgument(ReadOnlySpan<double> x)
        {
            // x has the same layout as the guess: the first values go into the curve
            for (int i = 0; i < s.Times.Length - 1; ++i)
            {
                double value = x[i];
                if (TTraits.HasTransform)
                {
                    value = TTraits.TransformDirect(value, i + 1, s);
                }

                TTraits.UpdateGuess(s.Data, value, i + 1);
            }

            s.Rebuild(interpolator);

            // the variables' values, and their helpers built on them
            if (bootstrap.additionalVariables is { } variables)
            {
                ImmutableArray<double> values = variables.Values(x[(s.Times.Length - 1)..]);
                s.AdditionalVariables = values;
                if (bootstrap.variableHelpers is not null)
                {
                    BootstrapHelper<TTermStructure>[] rebuilt = BuildVariableHelpers(values);
                    if (rebuilt.Length != variablePillars.Length)
                    {
                        throw new NanoQLException("the additional variables moved a pillar");
                    }

                    for (int k = 0; k < rebuilt.Length; ++k)
                    {
                        if (rebuilt[k].PillarDate != variablePillars[k])
                        {
                            throw new NanoQLException("the additional variables moved a pillar");
                        }
                    }

                    foreach (int k in aliveVariableHelpers)
                    {
                        rebuilt[k] = s.Bind(rebuilt[k]);
                    }

                    builtVariableHelpers = rebuilt;
                }
            }
        }

        public override Vector EvaluateCostFunction()
        {
            Vector? additionalErrors = bootstrap.additionalPenalties?.Invoke(s, context);
            int nAdditional = additionalErrors?.Size ?? 0;
            int nAlive = aliveInstruments.Length + aliveVariableHelpers.Length;
            var result = new Vector(nAlive + nAdditional);
            for (int i = 0; i < aliveInstruments.Length; ++i)
            {
                result[i] = aliveInstruments[i].QuoteError(context) * aliveInstrumentWeights[i];
            }

            for (int k = 0; k < aliveVariableHelpers.Length; ++k)
            {
                int i = aliveInstruments.Length + k;
                result[i] = builtVariableHelpers[aliveVariableHelpers[k]].QuoteError(context) * aliveInstrumentWeights[i];
            }

            additionalErrors?.AsSpan().CopyTo(result.AsSpan().Slice(nAlive));
            return result;
        }

        public override void SetToValid() => setToValid?.Invoke();

        // The variables' values at their guesses, none without variables.
        private ImmutableArray<double> GuessValues() =>
            bootstrap.additionalVariables is { } variables ? variables.Values(variables.Initialize(false).AsSpan()) : [];

        // The variables' helpers on their values, re-dated for the curve's evaluation date as the curve's helpers are.
        private BootstrapHelper<TTermStructure>[] BuildVariableHelpers(ImmutableArray<double> values)
        {
            if (bootstrap.variableHelpers is not { } build)
            {
                return [];
            }

            ImmutableArray<IBootstrapHelper> given = build(values, context);
            if (given.IsDefault)
            {
                return [];
            }

            var helpers = new BootstrapHelper<TTermStructure>[given.Length];
            for (int i = 0; i < given.Length; ++i)
            {
                if (given[i] is null)
                {
                    throw new ArgumentException("null rate helper", "variableHelpers");
                }

                helpers[i] = ((BootstrapHelper<TTermStructure>)given[i]).RedatedFor(s.Context);
            }

            return helpers;
        }

        // The guesses for the curve's values, every node's but the first's.
        private Vector MakeInitialGuess()
        {
            Vector guess;
            bool needsTransform = true;
            int pillars = s.Times.Length - 1;
            if (bootstrap.initialGuessFn is { } initialGuessFn)
            {
                // the function's own array, as QuantLib's returns a copy
                guess = initialGuessFn(new NoDataState(s), context).Clone();
                if (!(guess.Size >= pillars))
                {
                    throw new NanoQLException($"initial guess has {guess.Size} values but the curve has {pillars} pillars");
                }
            }
            else if (TTraits.HasGlobalGuess)
            {
                (guess, needsTransform) = TTraits.GlobalGuess(s, false);
            }
            else
            {
                // the interpolation, which Traits::guess() might rely on, was built over the first data; as QuantLib's, it
                // isn't rebuilt after each Traits::updateGuess()
                guess = new Vector(pillars);
                for (int i = 0; i < pillars; ++i)
                {
                    // Just pass zero as the first alive helper, it's not used in the standard QL traits anyway. Update the
                    // data since Traits::guess() usually depends on previous values.
                    TTraits.UpdateGuess(s.Data, TTraits.Guess(i + 1, s, false, 0), i + 1);
                    guess[i] = s.Data[i + 1];
                }
            }

            if (TTraits.HasTransform && needsTransform)
            {
                for (int i = 0; i < guess.Size; ++i)
                {
                    guess[i] = TTraits.TransformInverse(guess[i], i + 1, s);
                }
            }

            return guess;
        }
    }

    // The working state as the initial-guess function sees it: QuantLib passes the curve's data only when they hold a
    // previous state's values, which a curve built afresh never has.
    private sealed class NoDataState(IBootstrapState s) : IBootstrapState
    {
        public ReadOnlySpan<double> Times => s.Times;

        public ReadOnlySpan<double> Data => [];

        public IReadOnlyList<IBootstrapHelper> Instruments => s.Instruments;

        public IReadOnlyList<IBootstrapHelper> AdditionalHelpers => s.AdditionalHelpers;

        public EvaluationContext Context => s.Context;
    }
}
