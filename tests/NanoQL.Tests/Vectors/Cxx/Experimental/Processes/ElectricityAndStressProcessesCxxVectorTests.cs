// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using RsgType = NanoQL.Numerics.InverseCumulativeRsg<
    NanoQL.Numerics.RandomSequenceGenerator<NanoQL.Numerics.MersenneTwisterUniformRng>,
    NanoQL.Numerics.InverseCumulativeNormal>;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's process vectors, <c>tests/vectors/cxx/experimental/processes/electricityandstressprocesses.json</c>:
/// Geman-Roncoroni's paths at seed 421 and its members at three points, and the extended and vega-stressed processes on
/// the members QuantLib and nanoql share, a vega-stressed box set through the setters among them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ElectricityAndStressProcessesCxxVectorTests
{
    private static readonly Date Reference = new(46311);

    [Fact]
    public void ElectricityAndStressProcessesCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/processes/electricityandstressprocesses");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name.StartsWith("geman-roncoroni values", StringComparison.Ordinal))
            {
                GemanRoncoroniProcess gr = Vpp();
                double[][] grPoints = [.. c.Input("points").EnumerateArray().Select(p => p.EnumerateArray().Select(e => e.GetDouble()).ToArray())];
                check.Output(c, "drift", grPoints.Select(x => gr.Drift(x[0], x[1])).ToArray());
                check.Output(c, "diffusion", grPoints.Select(x => gr.Diffusion(x[0], x[1])).ToArray());
                check.Output(c, "stdDeviation", grPoints.Select(x => gr.StdDeviation(x[0], x[1], x[2])).ToArray());
                continue;
            }

            if (c.Name.StartsWith("geman", StringComparison.Ordinal))
            {
                Paths(c, check);
                continue;
            }

            double spot = c.Double("spot");
            YieldTermStructure q = Flat(c.Double("q")), r = Flat(c.Double("r"));
            var vol = new BlackConstantVol(Reference, new NullCalendar(), c.Double("vol"), new Actual365Fixed());
            double[][] points = [.. c.Input("points").EnumerateArray().Select(p => p.EnumerateArray().Select(e => e.GetDouble()).ToArray())];
            if (c.Name.StartsWith("extended", StringComparison.Ordinal))
            {
                var scheme = (ExtendedBlackScholesMertonProcessDiscretization)c.Int("scheme");
                var p = new ExtendedBlackScholesMertonProcess(spot, q, r, vol, evolDisc: scheme);
                check.Output(c, "drift", points.Select(x => p.Drift(x[0], x[1])).ToArray());
                check.Output(c, "diffusion", points.Select(x => p.Diffusion(x[0], x[1])).ToArray());
                if (scheme == ExtendedBlackScholesMertonProcessDiscretization.PredictorCorrector)
                {
                    check.Output(c, "evolve", points.Select(x => p.Evolve(x[0], x[1], x[2], x[3])).ToArray());
                }

                continue;
            }

            double[] box = c.Doubles("box");
            if (c.Name.StartsWith("vega setters", StringComparison.Ordinal))
            {
                // QuantLib's setters on its default box, each nanoql's returning a copy
                VegaStressedBlackScholesProcess set = new VegaStressedBlackScholesProcess(spot, q, r, vol)
                    .SetLowerTimeBorderForStressTest(box[0])
                    .SetUpperTimeBorderForStressTest(box[1])
                    .SetLowerAssetBorderForStressTest(box[2])
                    .SetUpperAssetBorderForStressTest(box[3])
                    .SetStressLevel(box[4]);
                check.Output(c, "box", new[]
                {
                    set.LowerTimeBorderForStressTest, set.UpperTimeBorderForStressTest, set.LowerAssetBorderForStressTest,
                    set.UpperAssetBorderForStressTest, set.StressLevel,
                });
                check.Output(c, "diffusion", points.Select(x => set.Diffusion(x[0], x[1])).ToArray());
                continue;
            }

            var s = new VegaStressedBlackScholesProcess(spot, q, r, vol, box[0], box[1], box[2], box[3], box[4]);
            check.Output(c, "drift", points.Select(x => s.Drift(x[0], x[1])).ToArray());
            check.Output(c, "diffusion", points.Select(x => s.Diffusion(x[0], x[1])).ToArray());
            check.Output(c, "expectation", points.Select(x => s.Expectation(x[0], x[1], x[2])).ToArray());
            if (box[4] == 0.0)
            {
                check.Output(c, "stdDeviation", points.Select(x => s.StdDeviation(x[0], x[1], x[2])).ToArray());
                check.Output(c, "variance", points.Select(x => s.Variance(x[0], x[1], x[2])).ToArray());
                check.Output(c, "evolve", points.Select(x => s.Evolve(x[0], x[1], x[2], x[3])).ToArray());
            }
        }

        check.Complete();
    }

    private static YieldTermStructure Flat(double rate) => new FlatForward(Reference, rate, new Actual365Fixed());

    // vpp's testGemanRoncoroniProcess process, the harness's
    private static GemanRoncoroniProcess Vpp() =>
        new(3.3, 3.1, 0.05, -0.09, 0.07, -0.40, -0.40, 1.6, 1.0, 0.5, 10.0, -7.0, -0.3, 35.0, 9.0, 0.10, 1.9);

    // vpp's testGemanRoncoroniProcess set-up, the harness's
    private static void Paths(VectorCase c, VectorAssert check)
    {
        GemanRoncoroniProcess grProcess = Vpp();
        StochasticProcess1D eouProcess = new ExtendedOrnsteinUhlenbeckProcess(
            5.0, Math.Sqrt(1.4), 1.1, x => 1.0 + (0.08 * x), ExtendedOrnsteinUhlenbeckProcessDiscretization.Trapezodial);
        var correlation = new Matrix(2, 2, 1.0);
        correlation[0, 1] = correlation[1, 0] = 0.25;
        var pArray = new StochasticProcessArray([grProcess, eouProcess], correlation);
        var grid = new TimeGrid(10.0, 2500);
        RsgType rsg = PseudoRandom.MakeSequenceGenerator(pArray.Size * (grid.Size - 1), 421);
        var generator = new MultiPathGenerator<RsgType>(pArray, grid, rsg, false);
        int paths = c.Int("paths"), steps = c.Int("steps");
        var electricity = new List<double>();
        var fuel = new List<double>();
        for (int n = 0; n < paths; ++n)
        {
            MultiPath path = generator.Next().Value;
            for (int i = 0; i <= steps; ++i)
            {
                electricity.Add(path[0][i]);
                fuel.Add(path[1][i]);
            }
        }

        check.Output(c, "electricity", electricity.ToArray());
        check.Output(c, "fuel", fuel.ToArray());
    }
}
