// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// C4's documented behaviours and refusals (the S-xeq plan's XER5 (xxxi), (xxxii), XED7): the shape lookups refusing a
/// time past the shape's last, where QuantLib reads one past its end; the jump process's one jump per step, QuantLib's
/// first-order scheme; the jump processes' paths drawn without allocating; and the energy units' refusals with
/// QuantLib's texts.
/// </summary>
public class ExperimentalEnergyConventionTests
{
    private static readonly Date Today = new(18, Month.December, 2011);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void AShapeLookupPastTheShapesLastTimeIsRefused()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(-1.0, 1.0, 5));
        var calculator = new FdmExpExtOUInnerValueCalculator(new PlainVanillaPayoff(OptionType.Call, 0.0), mesher, [(0.25, 0.1), (0.5, 0.2)]);
        FdmLinearOpIterator iter = mesher.Layout.Begin();

        // QuantLib's lower bound at (t − √ε, 0): the first time at or after it, the last at t = 0.5
        Assert.Equal(Math.Exp(0.2 - 1.0), calculator.InnerValue(iter, 0.5), 1e-15);
        Assert.Equal(Math.Exp(0.1 - 1.0), calculator.InnerValue(iter, 0.1), 1e-15);
        Assert.Equal(calculator.InnerValue(iter, 0.1), calculator.AvgInnerValue(iter, 0.1));
        Assert.StartsWith(
            "time 0.6 is past the shape's last time 0.5",
            Assert.Throws<NanoQLException>(() => calculator.InnerValue(iter, 0.6)).Message);

        var jumpMesher = new FdmMesherComposite(new Uniform1dMesher(-1.0, 1.0, 3), new Uniform1dMesher(0.0, 1.0, 3));
        var jumpCalculator = new FdmExtOUJumpModelInnerValue(new PlainVanillaPayoff(OptionType.Call, 0.0), jumpMesher, [(0.5, 0.2)]);
        Assert.Throws<NanoQLException>(() => jumpCalculator.InnerValue(jumpMesher.Layout.Begin(), 0.75));
    }

    [Fact]
    public void AStepDrawsAtMostOneJump()
    {
        // at an intensity of 1e6 over a step of 1 a jump is certain and many expected: the step adds one, QuantLib's scheme
        var process = new ExtOUWithJumpsProcess(new ExtendedOrnsteinUhlenbeckProcess(1.0, 0.5, 0.0, _ => 0.0), 0.5, 3.0, 1e6, 2.0);
        double[] result = new double[2];
        process.Evolve(0.0, [0.0, 0.5], 1.0, [0.1, 0.0, 0.3], result);
        double u2 = new CumulativeNormalDistribution().Evaluate(0.3);
        Assert.Equal((0.5 * Math.Exp(-3.0)) - (Math.Log(u2) / 2.0), result[1], 1e-15);
    }

    [Fact]
    public void TheJumpProcessesDrawPathsWithoutAllocating()
    {
        var power = new ExtOUWithJumpsProcess(new ExtendedOrnsteinUhlenbeckProcess(7.0, 1.4, 0.0, _ => 0.0), 0.0, 200.0, 4.0, 5.0);
        var kluge = new KlugeExtOUProcess(0.7, power, new ExtendedOrnsteinUhlenbeckProcess(4.45, 1.1, 0.0, _ => 0.0));
        var grid = new NanoQL.TimeGrid(1.0, 20);
        foreach (StochasticProcess process in new StochasticProcess[] { power, kluge })
        {
            foreach (bool antithetic in new[] { false, true })
            {
                // the draw, price and add of each sample, as the swing cases' Monte Carlo runs them
                AllocationAssert.AllocatesNothing(
                    () =>
                    {
                        IPathGenerator<MultiPath> generator = MultiVariate<PseudoRandom>.MakePathGenerator(
                            process, grid, PseudoRandom.MakeSequenceGenerator(process.Factors * (grid.Size - 1), 42), false);
                        var model = new MonteCarloModel<MultiPath, IncrementalStatistics>(
                            generator, new SpotPricer(), new IncrementalStatistics(), antithetic);
                        model.AddSamples(100);
                        return model;
                    },
                    model => model.AddSamples(2000));
            }
        }
    }

    [Fact]
    public void TheEnergyUnitsRefuseWithQuantLibsTexts()
    {
        var exercise = new SwingExercise(Today, Today + 2, 3600);
        Assert.Equal(
            "either a start limit or fuel limit is supported",
            Assert.Throws<NanoQLException>(() => new VanillaVPPOption(2.5, 8, 40, 2, 2, 20, 100, exercise, 3, 10).Validate()).Message);
        Assert.Equal(
            "start and running hour limt together is not supported",
            Assert.Throws<NanoQLException>(() => new FdmVPPStepConditionFactory(new VanillaVPPOption(2.5, 8, 40, 2, 2, 20, 100, exercise, 3, 10))).Message);
        Assert.Equal(
            "vpp type is not supported",
            Assert.Throws<NanoQLException>(
                () => new FdmVPPStepConditionFactory(new VanillaVPPOption(2.5, 8, 40, 2, 2, 20, 100, exercise, null, 10)).StateMesher()).Message);

        // a fuel list shorter than the power list: QuantLib reads past it
        var option = new VanillaVPPOption(2.5, 8, 40, 2, 2, 20, 100, exercise);
        Assert.Equal(
            "invalid time",
            Assert.Throws<NanoQLException>(
                () => DynProgVPPIntrinsicValueEngine.Calculate(option, Context, [20.0, 21.0], [40.0, 41.0, 42.0], 3.0)).Message);

        var mesh = new FdmVPPStepConditionMesher(0, new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 5)));
        var zero = new FdmZeroInnerValue();
        Assert.Equal(
            "mesher does not fit to vpp arguments",
            Assert.Throws<NanoQLException>(
                () => new FdmVPPStartLimitStepCondition(new(2.5, 8, 40, 2, 2, 20, 100, 3), null, mesh, zero, zero)).Message);
        var mesh2 = new FdmVPPStepConditionMesher(0, new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 2)));
        Assert.Equal(
            "minimum up time must be greater than one",
            Assert.Throws<NanoQLException>(
                () => new FdmVPPStartLimitStepCondition(new(2.5, 8, 40, 0, 2, 20, 100, 3), null, mesh2, zero, zero)).Message);

        var storage = new VanillaStorageOption(new BermudanExercise([Today + 1, Today + 2]), 50, 0, 1);
        var europeanStorage = new VanillaStorageOption(new BermudanExercise([Today + 2]), 50, 0, 1);
        YieldTermStructure rTS = new FlatForward(Today, 0.1, new Actual365Fixed());
        var ou = new ExtendedOrnsteinUhlenbeckProcess(1.0, 0.5, 3.0, _ => 3.0);
        Assert.True(double.IsFinite(FdSimpleExtOUStorageEngine.Calculate(storage, Context, ou, rTS, 1, 25).NPV!.Value));
        Assert.True(double.IsFinite(FdSimpleExtOUStorageEngine.Calculate(europeanStorage, Context, ou, rTS, 1, 25).NPV!.Value));

        var mesher2d = new FdmMesherComposite(new Uniform1dMesher(-1.0, 1.0, 5), new Uniform1dMesher(0.0, 1.0, 3));
        var power = new ExtOUWithJumpsProcess(ou, 0.0, 5.0, 1.0, 2.0);
        var klugeOU = new KlugeExtOUProcess(0.5, power, ou);
        var desc = new FdmSolverDesc(mesher2d, [], new FdmStepConditionComposite([], []), zero, 1.0, 1, 0);
        Assert.Throws<ArgumentException>(() => new FdmKlugeExtOUSolver(klugeOU, rTS, desc));

        var jumpOp = new FdmExtOUJumpOp(mesher2d, power, rTS, [new FdmDirichletBoundary(mesher2d, 0.0, 0, BoundaryConditionSide.Lower)], 4);
        Assert.Equal("boundary conditions are not supported", Assert.Throws<NanoQLException>(() => jumpOp.ToMatrixDecomp()).Message);
    }

    // the spot e^(x + y) at a path's end
    private sealed class SpotPricer : PathPricer<MultiPath>
    {
        public override double Evaluate(MultiPath path) => Math.Exp(path[0].Back() + path[1].Back());
    }
}
