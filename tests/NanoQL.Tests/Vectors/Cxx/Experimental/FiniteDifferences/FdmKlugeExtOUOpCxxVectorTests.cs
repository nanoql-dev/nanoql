// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's energy operator vectors, <c>tests/vectors/cxx/experimental/finitedifferences/fdmklugeextouop.json</c>:
/// <see cref="FdmExtendedOrnsteinUhlenbeckOp"/>, <see cref="FdmExtOUJumpOp"/> and <see cref="FdmKlugeExtOUOp"/> after
/// setting their times, their apply, mixed and directional applications, splitting solves and preconditioner on a seeded
/// vector. The meshers as <c>tools/cxx/src/experimental/finitedifferences/fdmklugeextouop.cpp</c> builds them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmKlugeExtOUOpCxxVectorTests
{
    [Fact]
    public void FdmKlugeExtOUOpCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/finitedifferences/fdmklugeextouop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("process");
            int[] grids = [.. c.Input("grids").EnumerateArray().Select(g => g.GetInt32())];
            double maturity = c.Input("maturity").GetDouble();
            YieldTermStructure rTS = new FlatForward(context.EvaluationDate, c.Input("rate").GetDouble(), new Actual365Fixed());
            int order = c.Input("order").GetInt32();
            FdmLinearOpComposite op;
            switch (c.Input("op").GetString())
            {
                case "extended":
                {
                    ExtendedOrnsteinUhlenbeckProcess p = EnergyInputs.ExtendedOU(spec);
                    var mesher = new FdmMesherComposite(new FdmSimpleProcess1dMesher(grids[0], p, maturity));
                    op = new FdmExtendedOrnsteinUhlenbeckOp(mesher, p, rTS, []);
                    break;
                }

                case "jumps":
                {
                    ExtOUWithJumpsProcess p = EnergyInputs.Jumps(spec);
                    var mesher = new FdmMesherComposite(
                        new FdmSimpleProcess1dMesher(grids[0], p.ExtendedOrnsteinUhlenbeckProcess, maturity),
                        new ExponentialJump1dMesher(grids[1], p.Beta, p.JumpIntensity, p.Eta));
                    op = new FdmExtOUJumpOp(mesher, p, rTS, [], order);
                    break;
                }

                default:
                {
                    KlugeExtOUProcess p = EnergyInputs.KlugeOU(spec);
                    ExtOUWithJumpsProcess kluge = p.KlugeProcess;
                    var mesher = new FdmMesherComposite(
                        new FdmSimpleProcess1dMesher(grids[0], kluge.ExtendedOrnsteinUhlenbeckProcess, maturity),
                        new ExponentialJump1dMesher(grids[1], kluge.Beta, kluge.JumpIntensity, kluge.Eta),
                        new FdmSimpleProcess1dMesher(grids[2], p.ExtOUProcess, maturity));
                    op = new FdmKlugeExtOUOp(mesher, p, rTS, [], order);
                    break;
                }
            }

            double[] r = Doubles(c.Input("r"));
            double s = c.Input("s").GetDouble();
            var apply = new List<double[]>();
            var mixed = new List<double[]>();
            var direction = new List<double[][]>();
            var splitting = new List<double[][]>();
            var preconditioner = new List<double[]>();
            foreach (JsonElement t in c.Input("times").EnumerateArray())
            {
                op.SetTime(t[0].GetDouble(), t[1].GetDouble());
                apply.Add(Run(r, op.Apply));
                mixed.Add(Run(r, op.ApplyMixed));
                direction.Add([.. Enumerable.Range(0, op.Size).Select(d => Run(r, (x, y) => op.ApplyDirection(d, x, y)))]);
                splitting.Add([.. Enumerable.Range(0, op.Size).Select(d => Run(r, (x, y) => op.SolveSplitting(d, x, s, y)))]);
                preconditioner.Add(Run(r, (x, y) => op.Preconditioner(x, s, y)));
            }

            check.Output(c, "apply", apply.ToArray());
            check.Output(c, "applyMixed", mixed.ToArray());
            check.Output(c, "applyDirection", direction.ToArray());
            check.Output(c, "solveSplitting", splitting.ToArray());
            check.Output(c, "preconditioner", preconditioner.ToArray());
        }

        check.Complete();
    }

    private delegate void Map(ReadOnlySpan<double> r, Span<double> result);

    private static double[] Run(double[] r, Map f)
    {
        var result = new double[r.Length];
        f(r, result);
        return result;
    }
}
