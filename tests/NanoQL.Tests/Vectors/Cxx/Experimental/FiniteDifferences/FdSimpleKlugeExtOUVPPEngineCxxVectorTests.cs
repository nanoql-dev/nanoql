// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's energy engine vectors,
/// <c>tests/vectors/cxx/experimental/finitedifferences/fdsimpleklugeextouvppengine.json</c>: the jump vanilla, storage,
/// spread, Kluge VPP and intrinsic engines' values, the VPP factory's state mesher and the start-limit condition's
/// rollback, step by step. The instruments as <c>tools/cxx/src/experimental/finitedifferences/fdsimpleklugeextouvppengine.cpp</c>
/// builds them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdSimpleKlugeExtOUVPPEngineCxxVectorTests
{
    private static readonly DayCounter Dc = new ActualActual(ActualActualConvention.ISDA);

    [Fact]
    public void FdSimpleKlugeExtOUVPPEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/finitedifferences/fdsimpleklugeextouvppengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date today = context.EvaluationDate;
            JsonElement a = c.Input("args");
            switch (c.Input("method").GetString())
            {
                case "FdExtOUJumpVanillaEngine::npv":
                {
                    Date maturity = today + new Period(a[4].GetInt32(), TimeUnit.Months);
                    Exercise exercise = a[3].GetBoolean() ? new AmericanExercise(today, maturity) : new EuropeanExercise(maturity);
                    var option = new VanillaOption(new PlainVanillaPayoff((OptionType)a[1].GetInt32(), a[2].GetDouble()), exercise);
                    int[] g = Ints(a[6]);
                    check.Output(c, "npv", () => FdExtOUJumpVanillaEngine.Calculate(
                        option, context, EnergyInputs.Jumps(a[0]), Curve(today, a[5]), g[0], g[1], g[2], EnergyInputs.Shape(a[7])).NPV);
                    break;
                }

                case "FdSimpleExtOUStorageEngine::npv":
                {
                    Date[] dates = [.. Enumerable.Range(1, a[5].GetInt32()).Select(d => today + d)];
                    var option = new VanillaStorageOption(new BermudanExercise(dates), a[2].GetDouble(), a[3].GetDouble(), a[4].GetDouble());
                    int[] g = Ints(a[6]);
                    int? yGrid = a[7].ValueKind == JsonValueKind.Null ? null : a[7].GetInt32();
                    check.Output(c, "npv", () => FdSimpleExtOUStorageEngine.Calculate(
                        option, context, EnergyInputs.ExtendedOU(a[0]), Curve(today, a[1]), g[0], g[1], yGrid, EnergyInputs.Shape(a[8])).NPV);
                    break;
                }

                case "FdKlugeExtOUSpreadEngine::npv":
                {
                    double heatRate = a[1].GetDouble();
                    var option = new BasketOption(
                        new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 0.0), [1.0, -heatRate]),
                        new EuropeanExercise(today + new Period(a[3].GetInt32(), TimeUnit.Months)));
                    int[] g = Ints(a[4]);
                    check.Output(c, "npv", () => FdKlugeExtOUSpreadEngine.Calculate(
                        option, context, EnergyInputs.KlugeOU(a[0]), Curve(today, a[2]), g[0], g[1], g[2], g[3], EnergyInputs.Shape(a[5]), EnergyInputs.Shape(a[6])).NPV);
                    break;
                }

                case "FdSimpleKlugeExtOUVPPEngine::npv":
                {
                    VanillaVPPOption option = Vpp(a[1], today, a[2].GetInt32());
                    int[] g = Ints(a[7]);
                    check.Output(c, "npv", () => FdSimpleKlugeExtOUVPPEngine.Calculate(
                        option, context, EnergyInputs.KlugeOU(a[0]), Curve(today, a[3]), EnergyInputs.Shape(a[4]), EnergyInputs.Shape(a[5]), a[6].GetDouble(), g[0], g[1], g[2], g[3]).NPV);
                    break;
                }

                case "DynProgVPPIntrinsicValueEngine::npv":
                    check.Output(c, "npv", () => DynProgVPPIntrinsicValueEngine.Calculate(
                        Vpp(a[0], today, a[1].GetInt32()), context, Doubles(a[2]), Doubles(a[3]), a[4].GetDouble()).NPV);
                    break;
                case "FdmVPPStepConditionFactory::stateMesher":
                    check.Output(c, "locations", () => new FdmVPPStepConditionFactory(Vpp(a[0], today, a[1].GetInt32())).StateMesher().Locations().ToArray());
                    break;
                default:
                {
                    VanillaVPPOption option = Vpp(a[0], today, a[1].GetInt32());
                    double[] fuel = Doubles(a[2]), power = Doubles(a[3]);
                    var factory = new FdmVPPStepConditionFactory(option);
                    var mesher = new FdmMesherComposite(factory.StateMesher());
                    FdmVPPStepCondition condition = factory.Build(
                        new FdmVPPStepConditionMesher(0, mesher), a[4].GetDouble(), new StepFuel(fuel), new StepSpark(option.HeatRate, fuel, power));
                    var state = new double[mesher.Layout.Dim[0]];
                    var states = new List<double[]>();
                    for (int j = power.Length; j > 0; --j)
                    {
                        condition.ApplyTo(state, j - 1);
                        states.Add([.. state]);
                    }

                    check.Output(c, "states", states.ToArray());
                    check.Output(c, "maxValue", condition.MaxValue(state));
                    break;
                }
            }
        }

        check.Complete();
    }

    private static int[] Ints(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetInt32())];

    private static YieldTermStructure Curve(Date today, JsonElement rate) => new FlatForward(today, rate.GetDouble(), Dc);

    private static VanillaVPPOption Vpp(JsonElement s, Date today, int days) =>
        new(
            s.GetProperty("heatRate").GetDouble(),
            s.GetProperty("pMin").GetDouble(),
            s.GetProperty("pMax").GetDouble(),
            s.GetProperty("tMinUp").GetInt32(),
            s.GetProperty("tMinDown").GetInt32(),
            s.GetProperty("startUpFuel").GetDouble(),
            s.GetProperty("startUpFixCost").GetDouble(),
            new SwingExercise(today, today + days, 3600),
            s.GetProperty("nStarts").ValueKind == JsonValueKind.Null ? null : s.GetProperty("nStarts").GetInt32());

    // the harness's calculators: the prices at the step t names
    private sealed class StepFuel(double[] fuel) : FdmInnerValueCalculator
    {
        public override double InnerValue(FdmLinearOpIterator iter, double t) => fuel[(int)t];

        public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
    }

    private sealed class StepSpark(double heatRate, double[] fuel, double[] power) : FdmInnerValueCalculator
    {
        public override double InnerValue(FdmLinearOpIterator iter, double t) => power[(int)t] - (heatRate * fuel[(int)t]);

        public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
    }
}
