// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The McSimulation vectors, <c>tests/vectors/pricingengines/mcsimulation.json</c>: QuantLib's MCEuropeanEngine NPV and
/// error estimate, reproduced by <see cref="McEuropeanTestEngine{TMc}"/>, a test-only engine on
/// <see cref="McSimulation{TPath, TStats}"/> with QuantLib's European path pricer and MCVanillaEngine's grid and path
/// generator, over pseudo-random and Sobol traits, steps, samples, seeds, antithetic variates, a Brownian bridge and
/// tolerances; and the sample limit's refusal.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class McSimulationVectorTests
{
    [Fact]
    public void McSimulationVector()
    {
        var file = VectorFile.Load("pricingengines/mcsimulation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int steps = c.Int("steps");
            long seed = c.Input("seed").GetInt64();
            bool antithetic = c.Bool("antithetic"), bridge = c.Bool("bridge");
            double? tolerance = c.Inputs.TryGetProperty("tolerance", out _) ? c.Double("tolerance") : null;
            int? samples = c.Inputs.TryGetProperty("samples", out _) ? c.Int("samples") : null;
            int maxSamples = c.Inputs.TryGetProperty("maxSamples", out _) ? c.Int("maxSamples") : int.MaxValue;
            Func<(double Npv, double? Error)> run = c.String("rng") == "pseudorandom"
                ? () => Engine<SingleVariate<PseudoRandom>>(c, steps, bridge, antithetic, seed).Run(tolerance, samples, maxSamples)
                : () => Engine<SingleVariate<LowDiscrepancy>>(c, steps, bridge, antithetic, seed).Run(tolerance, samples, maxSamples);
            if (!c.Expected.TryGetProperty("errorEstimate", out _))
            {
                check.Output(c, "npv", () => run().Npv);
                continue;
            }

            (double npv, double? error) = run();
            check.Output(c, "npv", npv);
            check.Output(c, "errorEstimate", error);
        }

        check.Complete();
    }

    // mcsimulation.py's market: spot 100, a flat 5% risk-free and 2% dividend yield on Actual/365 Fixed, a 20% constant
    // volatility, all from the evaluation date; a one-year call struck at 100.
    internal static McEuropeanTestEngine<TMc> Engine<TMc>(VectorCase c, int steps, bool bridge, bool antithetic, long seed)
        where TMc : IMcTraits<MonteCarloPath>
    {
        Date today = c.Context().EvaluationDate;
        var dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.02, dc), new FlatForward(today, 0.05, dc),
            new BlackConstantVol(today, new TARGET(), 0.2, dc));
        return new McEuropeanTestEngine<TMc>(
            process, today + new Period(1, TimeUnit.Years), 100.0, steps, bridge, antithetic, seed);
    }

    /// <summary>
    /// A test-only Monte Carlo engine for a European call on <see cref="McSimulation{TPath, TStats}"/>: QuantLib's
    /// MCVanillaEngine's time grid (the expiry's time on the process, in the steps given) and path generator (the traits'
    /// sequence generator of the factors times the steps), and MCEuropeanEngine's path pricer, the payoff at the path's end
    /// times the risk-free discount at the grid's last time.
    /// </summary>
    /// <typeparam name="TMc">The Monte Carlo traits.</typeparam>
    internal sealed class McEuropeanTestEngine<TMc> : McSimulation<MonteCarloPath, GeneralStatistics>
        where TMc : IMcTraits<MonteCarloPath>
    {
        private readonly GeneralizedBlackScholesProcess process;
        private readonly Date expiry;
        private readonly double strike;
        private readonly int timeSteps;
        private readonly bool brownianBridge;
        private readonly long seed;

        public McEuropeanTestEngine(
            GeneralizedBlackScholesProcess process, Date expiry, double strike, int timeSteps, bool brownianBridge,
            bool antitheticVariate, long seed)
            : base(antitheticVariate, false)
        {
            this.process = process;
            this.expiry = expiry;
            this.strike = strike;
            this.timeSteps = timeSteps;
            this.brownianBridge = brownianBridge;
            this.seed = seed;
        }

        // MCVanillaEngine::calculate: the model's mean, and its error estimate where the traits allow one.
        public (double Npv, double? Error) Run(double? requiredTolerance, int? requiredSamples, int maxSamples)
        {
            MonteCarloModel<MonteCarloPath, GeneralStatistics> model = Calculate(requiredTolerance, requiredSamples, maxSamples);
            return (model.SampleAccumulator.Mean(), TMc.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null);
        }

        // A model built from the hooks with no sample, for the tests that step one themselves.
        public MonteCarloModel<MonteCarloPath, GeneralStatistics> NewModel() =>
            new(PathGenerator(), PathPricer(), new GeneralStatistics(), AntitheticVariate);

        protected override NanoQL.TimeGrid TimeGrid() => new(process.Time(expiry), timeSteps);

        protected override IPathGenerator<MonteCarloPath> PathGenerator()
        {
            int dimensions = process.Factors;
            NanoQL.TimeGrid grid = TimeGrid();
            IRsg generator = TMc.MakeSequenceGenerator(dimensions * (grid.Size - 1), seed);
            return TMc.MakePathGenerator(process, grid, generator, brownianBridge);
        }

        protected override PathPricer<MonteCarloPath> PathPricer() =>
            new EuropeanPathPricer(OptionType.Call, strike, process.RiskFreeRate.Discount(TimeGrid().Back()));

        // QuantLib's EuropeanPathPricer: the payoff at the path's end, times the discount.
        private sealed class EuropeanPathPricer(OptionType type, double strike, double discount) : PathPricer<MonteCarloPath>
        {
            private readonly PlainVanillaPayoff payoff = new(type, strike);

            public override double Evaluate(MonteCarloPath path) => payoff.Evaluate(path.Back()) * discount;
        }
    }
}
