// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/randomdefaultlatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The base of the latent models' Monte Carlo simulations, independent of the copula and of the generator: it draws
/// the factors and the variables and finds each default's day; the derived models say what a sample's events are.
/// Its statistics read the stored paths.
/// </summary>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
/// <typeparam name="TUsng">The uniform sequence generator, which the copula inverts (QuantLib's default
/// <see cref="SobolRsg"/>).</typeparam>
/// <remarks>
/// <para>
/// The simulation runs once, when the model is bound to a basket, on the basket's live names, and is stored in the
/// bound copy (the S-xcr plan's XR5, XR1 (b)): QuantLib's simulates lazily at its first statistic and keeps a model's
/// paths across baskets (XR8 (viii)). The path count is required and at least 1 (QuantLib's defaults to 0, "stats will
/// crash on div by zero, FIX ME"; XR8 (xlv)); the seed is required, in [1, 4294967295] (XR4 (a)), QuantLib's default
/// 2863311530.
/// </para>
/// <para>
/// A default's day counts from the evaluation date, its probability 0 before its curve's reference date (XR8 (x)):
/// QuantLib's counts from each curve's date and reads the count from the evaluation date, so curves dated T+2 read
/// today's statistics. Defaults count to a horizon, QuantLib's 4,050 days by default, a construction parameter here,
/// and a statistic dated past it is refused (XR8 (xliv)): QuantLib's reads any later date, its expected loss stopping
/// where the simulation does.
/// </para>
/// <para>
/// The default correlation is the paths' Pearson correlation, refused where a name's frequency is 0 or 1 (0/0):
/// QuantLib's divides the joint frequency by n − 1 and the single ones by n, so a name's correlation with itself is
/// 1.0013 on 5,000 paths (XR8 (xxiv)). The n-th event counts every default on a day, in name order: QuantLib's keys
/// its order by day, so two names defaulting on one day count once (<c>randomdefaultlatentmodel.hpp:270-286</c>). The
/// percentile and the shortfall are refused where they read past the sorted losses (the percentile at a position of
/// n, the shortfall where ⌈p·n⌉ = n, XR8 (xxix)); the percentile's interval keeps its indices within the losses, so at
/// 0 it reads QuantLib's value without its read before the start.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class RandomLM<TCopula, TInitTraits, TUsng> : DefaultLossModel, ITrancheLossDistribution
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
    where TUsng : class, ISeedableRsg<TUsng>
{
    private readonly long seed;
    private readonly int nSims;
    private readonly int maxHorizon;
    private ImmutableArray<ImmutableArray<RandomLMEvent>> simsBuffer;

    /// <summary>Initializes a new instance of the <see cref="RandomLM{TCopula, TInitTraits, TUsng}"/> class.</summary>
    /// <param name="nSims">The number of paths, at least 1.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295].</param>
    /// <param name="maxHorizon">The simulation's horizon in days from the evaluation date, at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count or the seed lies outside its range.</exception>
    private protected RandomLM(int nSims, long seed, int maxHorizon)
    {
        if (nSims < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nSims), nSims, "a random model needs at least one path");
        }

        if (seed < 1 || seed > 4294967295L)
        {
            throw new ArgumentOutOfRangeException(nameof(seed), seed, "the seed must lie in [1, 4294967295]");
        }

        if (maxHorizon < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHorizon), maxHorizon, "the horizon must be at least one day");
        }

        this.nSims = nSims;
        this.seed = seed;
        this.maxHorizon = maxHorizon;
    }

    /// <summary>Gets the number of paths.</summary>
    public int NumberOfSimulations => nSims;

    /// <summary>Gets the simulation's horizon in days from the evaluation date.</summary>
    public int MaxHorizon => maxHorizon;

    /// <summary>Gets the copula of the bound, restricted latent model, which the sampler inverts.</summary>
    private protected abstract TCopula SimulationCopula { get; }

    /// <summary>The probability of a given or larger number of defaults in the basket by a date.</summary>
    /// <param name="n">The number of defaults; at 0, 1.</param>
    /// <param name="d">The date, after the evaluation date and within the horizon.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException">The date isn't after the evaluation date ("Date for statistic must be in
    /// the future."), or lies past the horizon.</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public override double ProbAtLeastNEvents(int n, Date d)
    {
        int val = FutureDay(d);
        if (n == 0)
        {
            return 1.0;
        }

        double counts = 0.0;
        for (int iSim = 0; iSim < nSims; iSim++)
        {
            int simCount = 0;
            foreach (RandomLMEvent e in simsBuffer[iSim])
            {
                // duck type on the members:
                if (val > e.DayFromRef)
                {
                    simCount++;
                }
            }

            if (simCount >= n)
            {
                counts++;
            }
        }

        return counts / nSims;
    }

    /// <summary>
    /// The probabilities of each name being the n-th to default by a date, in the simulated basket's order: a
    /// statistic of heavy dispersion (Joshi and Kainth 2004; Chen and Glasserman 2008 for remedies, not implemented).
    /// </summary>
    /// <param name="n">The order, from 1 to the basket's size.</param>
    /// <param name="d">The date, after the evaluation date and within the horizon.</param>
    /// <returns>The probabilities.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The order lies outside [1, size] ("Impossible number of
    /// defaults.").</exception>
    /// <exception cref="ArgumentException">The date isn't after the evaluation date, or lies past the
    /// horizon.</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public override ImmutableArray<double> ProbsBeingNthEvent(int n, Date d)
    {
        int basketSize = BoundBasket.Size;
        if (!(n > 0 && n <= basketSize))
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, "Impossible number of defaults.");
        }

        int val = FutureDay(d);
        double[] hitsByDate = new double[basketSize];
        var namesDefaulting = new List<RandomLMEvent>();
        for (int iSim = 0; iSim < nSims; iSim++)
        {
            namesDefaulting.Clear();
            foreach (RandomLMEvent e in simsBuffer[iSim])
            {
                // if event is within time horizon...
                if (val > e.DayFromRef)
                {
                    namesDefaulting.Add(e);
                }
            }

            if (namesDefaulting.Count >= n)
            {
                // locate nth default in time, the names of one day in their order
                RandomLMEvent nth = namesDefaulting.OrderBy(e => e.DayFromRef).ElementAt(n - 1);
                hitsByDate[nth.NameIdx]++;
            }
        }

        return [.. hitsByDate.Select(x => x / nSims)];
    }

    /// <summary>Pearson's default correlation of two names by a date, on the paths.</summary>
    /// <param name="d">The date, after the evaluation date and within the horizon.</param>
    /// <param name="iName">The first name.</param>
    /// <param name="jName">The second name.</param>
    /// <returns>The correlation; 1 for a name with itself.</returns>
    /// <exception cref="ArgumentException">The date isn't after the evaluation date, or lies past the
    /// horizon.</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket, or a name's frequency is 0 or 1, where
    /// the correlation is 0/0.</exception>
    public override double DefaultCorrelation(Date d, int iName, int jName)
    {
        // a control variate with the probabilities is possible
        int val = FutureDay(d);
        double expectedDefiDefj = 0.0; // E[1_i 1_j]

        // the rest of magnitudes have known values (probabilities) but that
        //   would distort the simulation results.
        double expectedDefi = 0.0;
        double expectedDefj = 0.0;
        for (int iSim = 0; iSim < nSims; iSim++)
        {
            double imatch = 0.0, jmatch = 0.0;
            foreach (RandomLMEvent e in simsBuffer[iSim])
            {
                if (val > e.DayFromRef && e.NameIdx == iName)
                {
                    imatch = 1.0;
                }

                if (val > e.DayFromRef && e.NameIdx == jName)
                {
                    jmatch = 1.0;
                }
            }

            expectedDefiDefj += imatch * jmatch;
            expectedDefi += imatch;
            expectedDefj += jmatch;
        }

        // the sample's moments, all over n: QuantLib's divides the joint one by n - 1
        expectedDefiDefj /= nSims;
        expectedDefi /= nSims;
        expectedDefj /= nSims;
        double variances = expectedDefi * expectedDefj * (1.0 - expectedDefi) * (1.0 - expectedDefj);
        if (!(variances > 0.0))
        {
            throw new NanoQLException(string.Create(
                CultureInfo.InvariantCulture,
                $"the default correlation of names {iName} and {jName} is undefined: a name's simulated default frequency is 0 or 1"));
        }

        return (expectedDefiDefj - (expectedDefi * expectedDefj)) / Math.Sqrt(variances);
    }

    /// <inheritdoc/>
    public override double ExpectedTrancheLoss(Date d) => ExpectedTrancheLossInterval(d, 0.95).Mean;

    /// <summary>The expected tranche loss by a date, and the half-width of its confidence interval.</summary>
    /// <param name="d">The date, within the horizon.</param>
    /// <param name="confidencePerc">The interval's confidence.</param>
    /// <returns>The mean and the half-width.</returns>
    /// <exception cref="ArgumentException">The date lies past the horizon.</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public (double Mean, double HalfWidth) ExpectedTrancheLossInterval(Date d, double confidencePerc)
    {
        Basket basket = BoundBasket;
        Date today = basket.Context.EvaluationDate;
        int val = WithinHorizon(d, today);
        double attachAmount = basket.AttachmentAmount;
        double detachAmount = basket.DetachmentAmount;

        var lossStats = new GeneralStatistics();
        for (int iSim = 0; iSim < nSims; iSim++)
        {
            double portfSimLoss = PathLoss(simsBuffer[iSim], val, today, basket);
            lossStats.Add(Math.Min(Math.Max(portfSimLoss - attachAmount, 0.0), detachAmount - attachAmount));
        }

        return (lossStats.Mean(), lossStats.ErrorEstimate() * InverseCumulativeNormal.StandardValue(0.5 * (1.0 + confidencePerc)));
    }

    /// <summary>The distribution of the tranche's loss by a date: the probability of each loss or less, on the paths'
    /// histogram of at most 150 bins.</summary>
    /// <param name="d">The date, at or after the evaluation date and within the horizon.</param>
    /// <returns>The cumulative probabilities by loss.</returns>
    /// <exception cref="ArgumentException">The date precedes the evaluation date or lies past the
    /// horizon.</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public override ImmutableSortedDictionary<double, double> LossDistribution(Date d)
    {
        Histogram hist = ComputeHistogram(d);
        var distrib = ImmutableSortedDictionary.CreateBuilder<double, double>();

        // prob of losses less or equal to
        double suma = hist.Frequency(0);
        distrib.TryAdd(0.0, suma);
        for (int i = 1; i < hist.Bins; i++)
        {
            suma += hist.Frequency(i);
            distrib.TryAdd(hist.Breaks[i - 1], suma);
        }

        return distrib.ToImmutable();
    }

    /// <summary>The histogram of the paths' tranche losses by a date, of at most 150 bins.</summary>
    /// <param name="d">The date, at or after the evaluation date and within the horizon.</param>
    /// <returns>The histogram.</returns>
    /// <exception cref="ArgumentException">The date precedes the evaluation date ("Requested percentile date must lie
    /// after computation date.") or lies past the horizon.</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public Histogram ComputeHistogram(Date d)
    {
        Basket basket = BoundBasket;
        Date today = basket.Context.EvaluationDate;
        int val = TodayOrLater(d, today);
        double attachAmount = basket.AttachmentAmount;
        double detachAmount = basket.DetachmentAmount;

        double[] data = new double[nSims];
        for (int iSim = 0; iSim < nSims; iSim++)
        {
            double portfSimLoss = PathLoss(simsBuffer[iSim], val, today, basket);
            data[iSim] = Math.Min(Math.Max(portfSimLoss - attachAmount, 0.0), detachAmount - attachAmount);
        }

        // avoid using as many points as in the simulation.
        int nPts = Math.Min(data.Length, 150); // fix
        return new Histogram(data, nPts);
    }

    /// <summary>
    /// The tranche's expected shortfall above a level by a date: McNeil, Frey and Embrechts' definition for
    /// discontinuous distributions (Quantitative Risk Management, 2005, eq. 2.25), on the sorted paths.
    /// </summary>
    /// <param name="d">The date, at or after the evaluation date and within the horizon; at it, 0.</param>
    /// <param name="percent">The level.</param>
    /// <returns>The shortfall.</returns>
    /// <exception cref="ArgumentException">The date precedes the evaluation date or lies past the horizon; or the
    /// level's position ⌈p·n⌉ is n, past the sorted losses.</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public override double ExpectedShortfall(Date d, double percent)
    {
        Basket basket = BoundBasket;
        Date today = basket.Context.EvaluationDate;
        int val = TodayOrLater(d, today);
        double attachAmount = basket.AttachmentAmount;
        double detachAmount = basket.DetachmentAmount;
        if (val <= 0)
        {
            return 0.0; // plus basket realized losses
        }

        double[] losses = new double[nSims];
        for (int iSim = 0; iSim < nSims; iSim++)
        {
            double portfSimLoss = PathLoss(simsBuffer[iSim], val, today, basket);
            losses[iSim] = Math.Min(Math.Max(portfSimLoss - attachAmount, 0.0), detachAmount - attachAmount);
        }

        Array.Sort(losses);
        double posit = Math.Ceiling(percent * nSims);
        posit = posit >= 0.0 ? posit : 0.0;
        if (posit >= nSims)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"the shortfall's position {posit} lies past the {nSims} sorted losses"),
                nameof(percent));
        }

        int position = (int)posit;
        double perctlInf = losses[position]; // q_{\alpha}

        // the prob of values strictly larger than the quantile value.
        double probOverQ = (double)(nSims - position) / nSims;

        double tail = 0.0;
        for (int i = position; i < nSims; i++)
        {
            tail += losses[i];
        }

        return ((perctlInf * (1.0 - percent - probOverQ)) + (tail / nSims)) / (1.0 - percent);
    }

    /// <inheritdoc/>
    public override double Percentile(Date d, double percentile) => PercentileAndInterval(d, percentile).Value;

    /// <summary>
    /// The tranche's loss at a percentile by a date, with the 95% interval of the true value (M. Pritsker, "Evaluating
    /// value-at-risk methodologies: Accuracy versus computational time.", Wharton FIC, 1996, Appendix A).
    /// </summary>
    /// <param name="d">The date, within the horizon.</param>
    /// <param name="percentile">The percentile, in [0, 1).</param>
    /// <returns>The loss and the interval's ends.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The percentile lies outside [0, 1] ("Incorrect
    /// percentile").</exception>
    /// <exception cref="ArgumentException">The percentile's position ⌊n·p⌋ is n, past the sorted losses, or the date
    /// lies past the horizon.</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public (double Value, double Lower, double Upper) PercentileAndInterval(Date d, double percentile)
    {
        if (!(percentile >= 0.0 && percentile <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "Incorrect percentile");
        }

        Basket basket = BoundBasket;
        Date today = basket.Context.EvaluationDate;
        int val = WithinHorizon(d, today);
        double attachAmount = basket.AttachmentAmount;
        double detachAmount = basket.DetachmentAmount;

        double[] rankLosses = new double[nSims];
        for (int iSim = 0; iSim < nSims; iSim++)
        {
            double portfSimLoss = PathLoss(simsBuffer[iSim], val, today, basket);

            // update dataset for rank stat:
            rankLosses[iSim] = Math.Min(Math.Max(portfSimLoss - attachAmount, 0.0), detachAmount - attachAmount);
        }

        Array.Sort(rankLosses);
        double floor = Math.Floor(nSims * percentile);
        if (floor >= nSims)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"the percentile's position {floor} lies past the {nSims} sorted losses"),
                nameof(percentile));
        }

        int quantilePosition = (int)floor;
        double quantileValue = rankLosses[quantilePosition];

        // compute confidence interval:
        const double confInterval = 0.95; // as an argument?
        int r = quantilePosition - 1;
        int s = quantilePosition + 1;
        bool rLocked = false, sLocked = false;
        for (int delta = 1; delta < quantilePosition; delta++)
        {
            double cached = BetaFunctions.IncompleteBetaFunction(s, nSims + 1 - s, percentile, 1.0e-8, 500);

            // There was a fix in the repository on the gammadistribution. It
            // might impact these, it might be neccesary to multiply these values
            // by '-1'
            double pMinus = BetaFunctions.IncompleteBetaFunction(r + 1, nSims - r, percentile, 1.0e-8, 500) - cached;
            double pPlus = BetaFunctions.IncompleteBetaFunction(r, nSims - r + 1, percentile, 1.0e-8, 500) - cached;
            if (pMinus > confInterval && !rLocked)
            {
                rLocked = true;
            }

            if (pPlus >= confInterval && !sLocked)
            {
                sLocked = true;
            }

            if (rLocked && sLocked)
            {
                break;
            }

            r--;
            s++;
            s = Math.Min(nSims - 1, s);
        }

        // the interval's indices kept within the losses, where QuantLib's reads before the start at 0 and past the
        // end next to it
        double lowerPercentile = rankLosses[Math.Max(r, 0)];
        double upperPercentile = rankLosses[Math.Min(s, nSims - 1)];

        return (quantileValue, lowerPercentile, upperPercentile);
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> SplitVaRLevel(Date d, double loss)
    {
        ImmutableArray<double> varLevels = SplitVaRAndError(d, loss, 0.95)[0];

        // turn relative units into absolute:
        return [.. varLevels.Select(x => x * loss)];
    }

    /// <summary>
    /// Distributes a tranche loss level among the live names by a date, each name's share of the paths whose loss is
    /// above the level, with each share's confidence interval: [means, lower ends, upper ends].
    /// </summary>
    /// <param name="d">The date, within the horizon.</param>
    /// <param name="loss">The loss level, in loss units.</param>
    /// <param name="confInterval">The intervals' confidence.</param>
    /// <returns>The shares, their lower and their upper ends.</returns>
    /// <exception cref="ArgumentException">The date lies past the horizon.</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public ImmutableArray<ImmutableArray<double>> SplitVaRAndError(Date d, double loss, double confInterval)
    {
        Basket basket = BoundBasket;
        Date today = basket.Context.EvaluationDate;
        int val = WithinHorizon(d, today);
        double attachAmount = basket.AttachmentAmount;
        double detachAmount = basket.DetachmentAmount;
        int numLiveNames = basket.RemainingSize();

        double[] split = new double[numLiveNames];
        GeneralStatistics[] splitStats = [.. Enumerable.Range(0, numLiveNames).Select(_ => new GeneralStatistics())];
        var splitEventsBuffer = new List<RandomLMEvent>();
        for (int iSim = 0; iSim < nSims; iSim++)
        {
            double portfSimLoss = 0.0;
            splitEventsBuffer.Clear();
            foreach (RandomLMEvent e in simsBuffer[iSim])
            {
                if (val > e.DayFromRef)
                {
                    portfSimLoss += EventLoss(e, today, basket);

                    // and will sort later if buffer applies:
                    splitEventsBuffer.Add(e);
                }
            }

            portfSimLoss = Math.Min(Math.Max(portfSimLoss - attachAmount, 0.0), detachAmount - attachAmount);

            /* second pass; split is conditional to total losses within target
            losses/percentile:  */
            double ptflCumulLoss = 0.0;
            if (portfSimLoss > loss)
            {
                // by day, the events of one day in their order
                RandomLMEvent[] sorted = [.. splitEventsBuffer.OrderBy(e => e.DayFromRef)];
                Array.Clear(split);

                /*  if the name triggered a loss in the portf limits assign
                this loss to that name..  */
                foreach (RandomLMEvent e in sorted)
                {
                    double lossName = EventLoss(e, today, basket);
                    double tranchedLossBefore = Math.Min(Math.Max(ptflCumulLoss - attachAmount, 0.0), detachAmount - attachAmount);
                    ptflCumulLoss += lossName;
                    double tranchedLossAfter = Math.Min(Math.Max(ptflCumulLoss - attachAmount, 0.0), detachAmount - attachAmount);

                    // assign new losses:
                    split[e.NameIdx] += tranchedLossAfter - tranchedLossBefore;
                }

                for (int iName = 0; iName < numLiveNames; iName++)
                {
                    splitStats[iName].Add(split[iName] / Math.Min(Math.Max(ptflCumulLoss - attachAmount, 0.0), detachAmount - attachAmount));
                }
            }
        }

        // Compute error in VaR split
        var means = ImmutableArray.CreateBuilder<double>(numLiveNames);
        var rangeUp = ImmutableArray.CreateBuilder<double>(numLiveNames);
        var rangeDown = ImmutableArray.CreateBuilder<double>(numLiveNames);
        double confidFactor = InverseCumulativeNormal.StandardValue(0.5 + (confInterval / 2.0));
        for (int iName = 0; iName < numLiveNames; iName++)
        {
            means.Add(splitStats[iName].Mean());
            double error = confidFactor * splitStats[iName].ErrorEstimate();
            rangeDown.Add(means[^1] - error);
            rangeUp.Add(means[^1] + error);
        }

        return [means.MoveToImmutable(), rangeDown.MoveToImmutable(), rangeUp.MoveToImmutable()];
    }

    /// <summary>The horizon's default probabilities, read by the derived model on its bound basket before the
    /// simulation.</summary>
    /// <param name="maxHorizonDate">The horizon's date, the evaluation date plus <see cref="MaxHorizon"/> days.</param>
    private protected abstract void InitDates(Date maxHorizonDate);

    /// <summary>A path's events from a sample of every factor.</summary>
    /// <param name="values">The sample.</param>
    /// <param name="today">The evaluation date.</param>
    /// <param name="events">The path's events, added in name order.</param>
    private protected abstract void NextSample(ReadOnlySpan<double> values, Date today, List<RandomLMEvent> events);

    /// <summary>Runs the simulation on the bound model: the horizon's probabilities, then each path's
    /// sample.</summary>
    private protected void PerformCalculations()
    {
        Date today = BoundBasket.Context.EvaluationDate;
        InitDates(today + new Period(maxHorizon, TimeUnit.Days));
        LatentModelFactorSampler<TCopula, TInitTraits, TUsng> copulasRng =
            LatentModelFactorSampler.Create<TCopula, TInitTraits, TUsng>(SimulationCopula, seed);
        var sims = ImmutableArray.CreateBuilder<ImmutableArray<RandomLMEvent>>(nSims);
        var events = new List<RandomLMEvent>();
        for (int i = nSims; i != 0; i--)
        {
            events.Clear();
            NextSample(copulasRng.NextSequence().Value.Span, today, events);
            sims.Add([.. events]);
        }

        simsBuffer = sims.MoveToImmutable();
    }

    /// <summary>
    /// A default's day: the whole number of days from the evaluation date at which the curve's default probability
    /// reaches the simulated one, Brent's root from a guess of 0 and a step of 1 truncated, the probability 0 before
    /// the curve's reference date and before the evaluation date. Brent's bracket can step below 0 where the
    /// probability is flat at 0, a curve dated after the evaluation date; QuantLib's refuses there ("t &lt; 0").
    /// </summary>
    /// <param name="curve">The name's curve.</param>
    /// <param name="today">The evaluation date.</param>
    /// <param name="pd">The simulated default probability.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <returns>The day.</returns>
    private protected static int DefaultDay(DefaultProbabilityTermStructure curve, Date today, double pd, double accuracy)
    {
        Date reference = curve.ReferenceDate;
        double Root(double t)
        {
            if (!(t >= 0.0))
            {
                // a day before the evaluation date, where no default is simulated: QuantLib's "t < 0"
                return -pd;
            }

            Date d = today + new Period((int)t, TimeUnit.Days);
            return (d < reference ? 0.0 : curve.DefaultProbability(d, true)) - pd;
        }

        return (int)new Brent().Solve(Root, accuracy, 0.0, 1.0);
    }

    private static double EventLoss(RandomLMEvent e, Date today, Basket basket) =>
        basket.Exposure(basket.Names[e.NameIdx], new Date(e.DayFromRef + today.SerialNumber)) * (1.0 - e.Recovery);

    private static double PathLoss(ImmutableArray<RandomLMEvent> events, int val, Date today, Basket basket)
    {
        double portfSimLoss = 0.0;
        foreach (RandomLMEvent e in events)
        {
            // if event is within time horizon...
            if (val > e.DayFromRef)
            {
                // ...and is contained in the basket.
                portfSimLoss += EventLoss(e, today, basket);
            }
        }

        return portfSimLoss;
    }

    // A statistic's day after the evaluation date, within the horizon.
    private int FutureDay(Date d)
    {
        Date today = BoundBasket.Context.EvaluationDate;
        if (!(d > today))
        {
            throw new ArgumentException("Date for statistic must be in the future.", nameof(d));
        }

        return WithinHorizon(d, today);
    }

    // A statistic's day at or after the evaluation date, within the horizon.
    private int TodayOrLater(Date d, Date today)
    {
        // redundant test? should have been tested by the basket caller?
        if (!(d >= today))
        {
            throw new ArgumentException("Requested percentile date must lie after computation date.", nameof(d));
        }

        return WithinHorizon(d, today);
    }

    private int WithinHorizon(Date d, Date today)
    {
        int val = d.SerialNumber - today.SerialNumber;
        if (val > maxHorizon)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the statistic's date {d} lies past the simulation's horizon of {maxHorizon} days from {today}: no default after it is simulated"),
                nameof(d));
        }

        return val;
    }
}
