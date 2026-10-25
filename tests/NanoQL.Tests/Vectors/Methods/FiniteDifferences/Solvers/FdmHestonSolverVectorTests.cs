// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmHestonSolver vectors, <c>tests/vectors/methods/finitedifferences/solvers/fdmhestonsolver.json</c>: European
/// and American options rolled back on the Heston operator under four schemes, with dividends, damping steps, a quanto
/// helper and a leverage function, read at points beside the spot and v0: the value, delta, gamma, the mean-variance
/// delta and gamma, and the theta where it doesn't cancel.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmHestonSolverVectorTests
{
    [Fact]
    public void FdmHestonSolverVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/solvers/fdmhestonsolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement setup = c.Input("setup");
            HestonProcess process = HestonProcessVectorTests.HestonProcessOf(setup.GetProperty("process"));
            var solver = new FdmHestonSolver(
                process,
                DescOf(setup, process, context),
                SchemeOf(c.Input("scheme")),
                QuantoOf(c.Input("quanto"), context),
                FdHestonVanillaEngineVectorTests.LeverageOf(c.Input("leverage")));
            (double S, double V)[] points = [.. c.Input("points").EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble()))];
            (double S, double V)[] thetaPoints =
                [.. c.Input("thetaPoints").EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble()))];
            check.Output(c, "valueAt", points.Select(p => Lazy(() => solver.ValueAt(p.S, p.V))).ToArray());
            check.Output(c, "thetaAt", thetaPoints.Select(p => Lazy(() => solver.ThetaAt(p.S, p.V))).ToArray());
            check.Output(c, "deltaAt", points.Select(p => Lazy(() => solver.DeltaAt(p.S, p.V))).ToArray());
            check.Output(c, "gammaAt", points.Select(p => Lazy(() => solver.GammaAt(p.S, p.V))).ToArray());
            check.Output(c, "meanVarianceDeltaAt", points.Select(p => Lazy(() => solver.MeanVarianceDeltaAt(p.S, p.V))).ToArray());
            check.Output(c, "meanVarianceGammaAt", points.Select(p => Lazy(() => solver.MeanVarianceGammaAt(p.S, p.V))).ToArray());
        }

        check.Complete();
    }

    // The set-up's solver description: its mesher, the log inner value of its payoff in direction 0, the vanilla
    // conditions on the risk-free curve's date and day counter, no boundary conditions, and the risk-free curve's time
    // to the exercise's last date.
    private static FdmSolverDesc DescOf(JsonElement setup, HestonProcess process, EvaluationContext context)
    {
        FdmMesherComposite mesher = MesherOf(setup.GetProperty("mesher"), context);
        Exercise exercise = FdExerciseOf(setup.GetProperty("exercise"), context);
        var calculator = new FdmLogInnerValue(PayoffOf(setup.GetProperty("payoff")), mesher, 0);
        var curve = process.RiskFreeRate;
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            DividendsOf(setup.GetProperty("dividends")),
            exercise,
            mesher,
            calculator,
            curve.ReferenceDate,
            curve.DayCounter,
            context);
        return new FdmSolverDesc(
            mesher,
            [],
            conditions,
            calculator,
            curve.DayCounter.YearFraction(curve.ReferenceDate, exercise.LastDate()),
            setup.GetProperty("tGrid").GetInt32(),
            setup.GetProperty("dampingSteps").GetInt32());
    }
}
