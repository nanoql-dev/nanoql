// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmBatesOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmbatesop.json</c>: the operator on
/// three Bates parameter sets and on the second with another integration order, Dirichlet boundaries and a quanto
/// helper, at three times, applied, applied in parts and solved by splitting on seeded vectors; and QuantLib's refusal
/// of its sparse matrix.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmBatesOpVectorTests
{
    [Fact]
    public void FdmBatesOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmbatesop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), context);
            ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries =
            [
                .. c.Input("boundaries").EnumerateArray().Select(b => (BoundaryCondition<FdmLinearOp>)new FdmDirichletBoundary(
                    mesher, b[0].GetDouble(), b[1].GetInt32(), (BoundaryConditionSide)b[2].GetInt32())),
            ];
            var op = new FdmBatesOp(
                mesher,
                BatesProcessVectorTests.BatesProcessOf(c.Input("process")),
                boundaries,
                c.Int("integrationOrder"),
                QuantoOf(c.Input("quanto"), context));
            ShortRateFdInputs.CheckOperator(check, c, op, mesher.Layout.Dim.Length);
            check.Output(c, "matrix", () => op.ToMatrix());
        }

        check.Complete();
    }
}
