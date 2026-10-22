// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmDirichletBoundary vectors,
/// <c>tests/vectors/methods/finitedifferences/utilities/fdmdirichletboundary.json</c>:
/// <see cref="FdmDirichletBoundary"/> on composite meshers, each direction on both sides: the arrays after
/// <see cref="FdmDirichletBoundary.ApplyAfterApplying(Span{double})"/> and
/// <see cref="FdmDirichletBoundary.ApplyAfterSolving"/>, which write in place, and the scalar form at points on and
/// beyond the direction's extremes; and the refusal of a boundary on no side.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmDirichletBoundaryVectorTests
{
    [Fact]
    public void FdmDirichletBoundaryVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/fdmdirichletboundary");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FdmDirichletBoundary Build() => new(
                MesherOf(c.Input("mesher"), context),
                c.Input("valueOnBoundary").GetDouble(),
                c.Input("direction").GetInt32(),
                (BoundaryConditionSide)c.Input("side").GetInt32());
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => Build());
                continue;
            }

            FdmDirichletBoundary boundary = Build();
            double[] applied = Doubles(c.Input("values")), solved = Doubles(c.Input("values"));
            boundary.ApplyAfterApplying(applied.AsSpan());
            boundary.ApplyAfterSolving(solved.AsSpan());
            check.Output(c, "applyAfterApplying", applied);
            check.Output(c, "applyAfterSolving", solved);
            check.Output(
                c,
                "applyAfterApplyingAt",
                c.Input("points").EnumerateArray()
                    .Select(p => (object?)boundary.ApplyAfterApplying(p[0].GetDouble(), p[1].GetDouble()))
                    .ToArray());
        }

        check.Complete();
    }
}
