// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.BasketInputs;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Fdm2dBlackScholesSolver vectors,
/// <c>tests/vectors/methods/finitedifferences/solvers/fdm2dblackscholessolver.json</c>:
/// <see cref="Fdm2dBlackScholesSolver"/>'s value, theta, deltas and gammas at five pairs of prices, on the problem the
/// two-dimensional engine builds, European and American, under two schemes and with the local volatility.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Fdm2dBlackScholesSolverVectorTests
{
    [Fact]
    public void Fdm2dBlackScholesSolverVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/solvers/fdm2dblackscholessolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("solver");
            ImmutableArray<GeneralizedBlackScholesProcess> p = ProcessesOf(spec.GetProperty("processes"), context);
            double t = spec.GetProperty("maturity").GetDouble();
            var m1 = new FdmBlackScholesMesher(
                spec.GetProperty("xGrid").GetInt32(), p[0], t, p[0].X0, null, null, 0.0001, 1.5, (p[0].X0, 0.1));
            var m2 = new FdmBlackScholesMesher(
                spec.GetProperty("yGrid").GetInt32(), p[1], t, p[1].X0, null, null, 0.0001, 1.5, (p[1].X0, 0.1));
            var mesher = new FdmMesherComposite(m1, m2);
            var calc = new FdmLogBasketInnerValue(BasketPayoffOf(spec.GetProperty("payoff")), mesher);
            FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
                [], ExerciseOf(spec.GetProperty("exercise"), context), mesher, calc, context.EvaluationDate,
                new Actual365Fixed(), context);
            var desc = new FdmSolverDesc(
                mesher,
                [],
                conditions,
                calc,
                t,
                spec.GetProperty("tGrid").GetInt32(),
                spec.GetProperty("dampingSteps").GetInt32());
            JsonElement overwrite = spec.GetProperty("illegalLocalVolOverwrite");
            var solver = new Fdm2dBlackScholesSolver(
                p[0],
                p[1],
                spec.GetProperty("correlation").GetDouble(),
                desc,
                SchemeOf(spec.GetProperty("scheme")),
                spec.GetProperty("localVol").GetBoolean(),
                overwrite.ValueKind == JsonValueKind.Null ? null : overwrite.GetDouble());
            (double U, double V)[] points =
                [.. c.Input("points").EnumerateArray().Select(q => (q[0].GetDouble(), q[1].GetDouble()))];
            check.Output(c, "valueAt", points.Select(q => solver.ValueAt(q.U, q.V)).ToArray());
            check.Output(c, "thetaAt", points.Select(q => solver.ThetaAt(q.U, q.V)).ToArray());
            check.Output(c, "deltaXat", points.Select(q => solver.DeltaXat(q.U, q.V)).ToArray());
            check.Output(c, "deltaYat", points.Select(q => solver.DeltaYat(q.U, q.V)).ToArray());
            check.Output(c, "gammaXat", points.Select(q => solver.GammaXat(q.U, q.V)).ToArray());
            check.Output(c, "gammaYat", points.Select(q => solver.GammaYat(q.U, q.V)).ToArray());
            check.Output(c, "gammaXYat", points.Select(q => solver.GammaXYat(q.U, q.V)).ToArray());
        }

        check.Complete();
    }
}
