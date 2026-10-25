// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Bates solver vectors, <c>tests/vectors/cxx/methods/finitedifferences/solvers/fdmbatessolver.json</c>:
/// European and American options rolled back on the Bates operator, with Dirichlet boundaries, another integration
/// order, the Douglas scheme, damping steps and a quanto helper, read at points beside the spot and v0: the value,
/// delta and gamma, and the theta where it doesn't cancel.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmBatesSolverCxxVectorTests
{
    [Fact]
    public void FdmBatesSolverCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/solvers/fdmbatessolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement processSpec = c.Input("process");
            BatesProcess process = HestonCxxInputs.BatesProcessOf(processSpec);
            JsonElement scheme = c.Input("scheme");
            var solver = new FdmBatesSolver(
                process,
                DescOf(c, process, context),
                new FdmSchemeDesc((FdmSchemeDescFdmSchemeType)scheme[0].GetInt32(), scheme[1].GetDouble(), scheme[2].GetDouble()),
                c.Int("integrationOrder"),
                HestonCxxInputs.QuantoOf(c.Input("quanto"), processSpec));
            (double S, double V)[] points = Points(c.Input("points"));
            check.Output(c, "valueAt", points.Select(p => Lazy(() => solver.ValueAt(p.S, p.V))).ToArray());
            check.Output(c, "thetaAt", Points(c.Input("thetaPoints")).Select(p => Lazy(() => solver.ThetaAt(p.S, p.V))).ToArray());
            check.Output(c, "deltaAt", points.Select(p => Lazy(() => solver.DeltaAt(p.S, p.V))).ToArray());
            check.Output(c, "gammaAt", points.Select(p => Lazy(() => solver.GammaAt(p.S, p.V))).ToArray());
        }

        check.Complete();
    }

    private static (double S, double V)[] Points(JsonElement spec) =>
        [.. spec.EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble()))];

    // The harness's solver description: the predefined log-spot and variance meshers, the log inner value of the plain
    // vanilla payoff, the vanilla conditions with no dividends on the risk-free curve's date and day counter, the
    // Dirichlet boundaries, and the risk-free curve's time to the exercise's last date.
    private static FdmSolverDesc DescOf(VectorCase c, HestonProcess process, EvaluationContext context)
    {
        var mesher = new FdmMesherComposite(new Predefined1dMesher(Doubles(c.Input("spots"))), new Predefined1dMesher(Doubles(c.Input("variances"))));
        var curve = process.RiskFreeRate;
        JsonElement exercise = c.Input("exercise");
        var date = new Date(exercise[1].GetInt32());
        Exercise ex = exercise[0].GetString() == "American" ? new AmericanExercise(curve.ReferenceDate, date) : new EuropeanExercise(date);
        var calculator = new FdmLogInnerValue(new PlainVanillaPayoff((OptionType)c.Int("optionType"), c.Double("strike")), mesher, 0);
        FdmStepConditionComposite conditions =
            FdmStepConditionComposite.VanillaComposite([], ex, mesher, calculator, curve.ReferenceDate, curve.DayCounter, context);
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries =
        [
            .. c.Input("boundaries").EnumerateArray().Select(b => (BoundaryCondition<FdmLinearOp>)new FdmDirichletBoundary(
                mesher, b[0].GetDouble(), b[1].GetInt32(), (BoundaryConditionSide)b[2].GetInt32())),
        ];
        return new FdmSolverDesc(
            mesher,
            boundaries,
            conditions,
            calculator,
            curve.DayCounter.YearFraction(curve.ReferenceDate, ex.LastDate()),
            c.Int("tGrid"),
            c.Int("dampingSteps"));
    }
}
