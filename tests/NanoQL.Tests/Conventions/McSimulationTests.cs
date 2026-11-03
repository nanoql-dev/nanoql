// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="McSimulation{TPath, TStats}"/>'s contract: its calculation builds a new
/// <see cref="MonteCarloModel{TPath, TStats}"/> and returns it, so one engine runs calculations in any order, each its
/// own; the control-variate correction in QuantLib's order; and its refusals, with QuantLib's texts. The engine is
/// <see cref="McSimulationVectorTests"/>' test-only European engine, whose values the McSimulation vectors check
/// against the wheel's MCEuropeanEngine.
/// </summary>
public class McSimulationTests
{
    private static readonly Date Today = new(15, Month.March, 2026);

    [Fact]
    public void TwoCalculationsOnOneEngineAreEachTheirOwn()
    {
        McSimulationVectorTests.McEuropeanTestEngine<SingleVariate<PseudoRandom>> engine = Engine();
        (double Npv, double? Error) first = engine.Run(null, 1000, int.MaxValue);
        (double Npv, double? Error) other = engine.Run(null, 4095, int.MaxValue);
        (double Npv, double? Error) again = engine.Run(null, 1000, int.MaxValue);
        Assert.Equal(first, again);
        Assert.NotEqual(first.Npv, other.Npv);
    }

    [Fact]
    public void TheSampleLimitIsRefusedWithQuantLibsText()
    {
        NanoQLException e = Assert.Throws<NanoQLException>(() => Engine().Run(0.001, null, 5000));
        Assert.StartsWith("max number of samples (5000) reached, while error (", e.Message);
        Assert.EndsWith(") is still above tolerance (0.001)", e.Message);
    }

    [Fact]
    public void NeitherAToleranceNorANumberOfSamplesIsRefused()
    {
        NanoQLException e = Assert.Throws<NanoQLException>(() => Engine().Run(null, null, int.MaxValue));
        Assert.Equal("neither tolerance nor number of samples set", e.Message);
    }

    [Fact]
    public void AControlVariateNeedsAPriceAndThenAPathPricer()
    {
        NanoQLException e = Assert.Throws<NanoQLException>(() => new ControlVariateEngine(null, null).Run(100));
        Assert.Equal("engine does not provide control-variation price", e.Message);
        e = Assert.Throws<NanoQLException>(() => new ControlVariateEngine(1.5, null).Run(100));
        Assert.Equal("engine does not provide control-variation path pricer", e.Message);
    }

    [Fact]
    public void ValueWithSamplesRefusesFewerSamplesThanSimulated()
    {
        MonteCarloModel<MonteCarloPath, GeneralStatistics> model = Engine().NewModel();
        model.AddSamples(100);
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => McSimulation<MonteCarloPath, GeneralStatistics>.ValueWithSamples(model, 50));
        Assert.Equal("number of already simulated samples (100) greater than requested samples (50)", e.Message);
    }

    [Fact]
    public void AControlVariateCorrectsEachSampleInQuantLibsOrder()
    {
        // The control priced by the path pricer itself, on the model's paths or on a second generator's copies of them:
        // each sample is price + (cvValue - price), QuantLib's order, the value to the last bits, with or without
        // antithetic paths.
        foreach (bool antithetic in new[] { false, true })
        {
            foreach (bool ownGenerator in new[] { false, true })
            {
                var engine = new ControlVariateEngine(1.5, new LastValuePricer(), ownGenerator, antithetic);
                MonteCarloModel<MonteCarloPath, GeneralStatistics> model = engine.Run(200);
                Assert.Equal(200, model.SampleAccumulator.Samples);
                Assert.True(Math.Abs(model.SampleAccumulator.Min() - 1.5) <= 1e-15);
                Assert.True(Math.Abs(model.SampleAccumulator.Max() - 1.5) <= 1e-15);
            }
        }
    }

    [Fact]
    public void ATolerancedCalculationStopsAtItsError()
    {
        (double npv, double? error) = Engine().Run(0.05, null, int.MaxValue);
        Assert.True(error <= 0.05);
        Assert.True(npv > 0.0);
    }

    private static McSimulationVectorTests.McEuropeanTestEngine<SingleVariate<PseudoRandom>> Engine()
    {
        var dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new NanoQL.TermStructures.FlatForward(Today, 0.02, dc), new NanoQL.TermStructures.FlatForward(Today, 0.05, dc),
            new NanoQL.TermStructures.BlackConstantVol(Today, new TARGET(), 0.2, dc));
        return new McSimulationVectorTests.McEuropeanTestEngine<SingleVariate<PseudoRandom>>(
            process, Today + new Period(1, TimeUnit.Years), 100.0, 1, false, false, 42);
    }

    private sealed class LastValuePricer : PathPricer<MonteCarloPath>
    {
        public override double Evaluate(MonteCarloPath path) => path.Back();
    }

    // An engine on an Ornstein-Uhlenbeck process whose control variate is given as constructed.
    private sealed class ControlVariateEngine(
        double? controlValue, PathPricer<MonteCarloPath>? controlPricer, bool ownGenerator = false, bool antithetic = false)
        : McSimulation<MonteCarloPath, GeneralStatistics>(antithetic, true)
    {
        public MonteCarloModel<MonteCarloPath, GeneralStatistics> Run(int samples) => Calculate(null, samples, int.MaxValue);

        protected override NanoQL.TimeGrid TimeGrid() => new(1.0, 4);

        protected override IPathGenerator<MonteCarloPath> PathGenerator() =>
            SingleVariate<PseudoRandom>.MakePathGenerator(
                new OrnsteinUhlenbeckProcess(0.1, 0.2), TimeGrid(), PseudoRandom.MakeSequenceGenerator(4, 7), false);

        protected override PathPricer<MonteCarloPath> PathPricer() => new LastValuePricer();

        protected override PathPricer<MonteCarloPath>? ControlPathPricer() => controlPricer;

        protected override IPathGenerator<MonteCarloPath>? ControlPathGenerator() => ownGenerator ? PathGenerator() : null;

        protected override double? ControlVariateValue() => controlValue;
    }
}
