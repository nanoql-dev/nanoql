// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's n-dimensional solver vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/solvers/fdmndimsolver.json</c>: <see cref="FdmNdimSolver"/>'s
/// interpolated value and theta at points inside the grid, in one to four dimensions on <see cref="FdmWienerOp"/>,
/// European and American average basket calls and puts, under Douglas's and Hundsdorfer's schemes.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmNdimSolverCxxVectorTests
{
    [Fact]
    public void FdmNdimSolverCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/solvers/fdmndimsolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var today = new Date(c.Int("today"));
            DayCounter dc = new Actual365Fixed();
            Fdm1dMesher[] meshers =
            [
                .. c.Input("meshers").EnumerateArray().Select(m =>
                    (Fdm1dMesher)new Uniform1dMesher(m[0].GetDouble(), m[1].GetDouble(), (int)m[2].GetDouble())),
            ];
            var mesher = new FdmMesherComposite(meshers);
            JsonElement r = c.Input("r");
            YieldTermStructure? rTS = r.ValueKind == JsonValueKind.Null ? null : new FlatForward(today, r.GetDouble(), dc);
            var op = new FdmWienerOp(mesher, rTS, c.Doubles("lambdas"));
            double[] payoff = c.Doubles("payoff");
            var basket = new AverageBasketPayoff(new PlainVanillaPayoff((OptionType)(int)payoff[0], payoff[1]), c.Doubles("weights"));
            var calculator = new FdmLogBasketInnerValue(basket, mesher);
            JsonElement exercise = c.Input("exercise");
            FdmStepConditionComposite conditions = exercise[0].GetString() == "American"
                ? FdmStepConditionComposite.VanillaComposite(
                    [], new AmericanExercise(today, new Date(exercise[1].GetInt32())), mesher, calculator, today, dc, context)
                : new FdmStepConditionComposite([], []);
            var desc = new FdmSolverDesc(mesher, [], conditions, calculator, c.Double("maturity"), c.Int("tGrid"), c.Int("dampingSteps"));
            double[] scheme = c.Doubles("scheme");
            var solver = new FdmNdimSolver(desc, new FdmSchemeDesc((FdmSchemeDescFdmSchemeType)(int)scheme[0], scheme[1], scheme[2]), op);
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "interpolateAt", points.Select(x => solver.InterpolateAt(x)).ToArray());
            check.Output(c, "thetaAt", points.Select(x => solver.ThetaAt(x)).ToArray());
        }

        check.Complete();
    }
}
