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
/// The FdmHestonHullWhiteSolver vectors,
/// <c>tests/vectors/methods/finitedifferences/solvers/fdmhestonhullwhitesolver.json</c>: European and American options
/// rolled back on the Heston-Hull-White operator under four schemes, with dividends and damping steps, read at points
/// beside the spot, v0 and the short rate's state 0: the value, delta, gamma, and the theta where it doesn't cancel.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmHestonHullWhiteSolverVectorTests
{
    [Fact]
    public void FdmHestonHullWhiteSolverVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/solvers/fdmhestonhullwhitesolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement setup = c.Input("setup");
            (HestonProcess process, HullWhiteProcess hullWhite, FdmSolverDesc desc) = SetupOf(setup, context);
            var solver = new FdmHestonHullWhiteSolver(
                process, hullWhite, setup.GetProperty("correlation").GetDouble(), desc, SchemeOf(c.Input("scheme")));
            (double S, double V, double R)[] points = Points(c.Input("points"));
            (double S, double V, double R)[] thetaPoints = Points(c.Input("thetaPoints"));
            check.Output(c, "valueAt", points.Select(p => Lazy(() => solver.ValueAt(p.S, p.V, p.R))).ToArray());
            check.Output(c, "deltaAt", points.Select(p => Lazy(() => solver.DeltaAt(p.S, p.V, p.R, 0.01 * p.S))).ToArray());
            check.Output(c, "gammaAt", points.Select(p => Lazy(() => solver.GammaAt(p.S, p.V, p.R, 0.01 * p.S))).ToArray());
            check.Output(c, "thetaAt", thetaPoints.Select(p => Lazy(() => solver.ThetaAt(p.S, p.V, p.R))).ToArray());
        }

        check.Complete();
    }

    // The set-up's processes and solver description: its mesher, the log inner value of its payoff in direction 0, the
    // vanilla conditions on the risk-free curve's date and day counter, no boundary conditions, and the risk-free
    // curve's time to the exercise's last date.
    internal static (HestonProcess Process, HullWhiteProcess HullWhite, FdmSolverDesc Desc) SetupOf(
        JsonElement setup, EvaluationContext context)
    {
        HestonProcess process = HestonProcessVectorTests.HestonProcessOf(setup.GetProperty("process"));
        HullWhiteProcess hullWhite = FdmHestonHullWhiteOpVectorTests.HullWhiteProcessOf(setup.GetProperty("hullWhite"), process);
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
        var desc = new FdmSolverDesc(
            mesher,
            [],
            conditions,
            calculator,
            curve.DayCounter.YearFraction(curve.ReferenceDate, exercise.LastDate()),
            setup.GetProperty("tGrid").GetInt32(),
            setup.GetProperty("dampingSteps").GetInt32());
        return (process, hullWhite, desc);
    }

    // Points of three coordinates.
    internal static (double, double, double)[] Points(JsonElement e) =>
        [.. e.EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble(), p[2].GetDouble()))];
}
