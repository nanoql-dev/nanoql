// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmBackwardSolver vectors, <c>tests/vectors/methods/finitedifferences/solvers/fdmbackwardsolver.json</c>: the
/// ten named <see cref="FdmSchemeDesc"/> descriptions, and <see cref="FdmBackwardSolver.Rollback"/> under the Douglas,
/// Crank-Nicolson and implicit Euler descriptions with damping steps, partial rollbacks and American, Bermudan,
/// snapshot and dividend conditions; and the refusal of an unknown scheme type.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmBackwardSolverVectorTests
{
    [Fact]
    public void FdmBackwardSolverVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/solvers/fdmbackwardsolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name == "named descriptions")
            {
                FdmSchemeDesc[] named =
                [
                    FdmSchemeDesc.Douglas(), FdmSchemeDesc.CrankNicolson(), FdmSchemeDesc.CraigSneyd(), FdmSchemeDesc.ModifiedCraigSneyd(),
                    FdmSchemeDesc.Hundsdorfer(), FdmSchemeDesc.ModifiedHundsdorfer(), FdmSchemeDesc.ExplicitEuler(),
                    FdmSchemeDesc.ImplicitEuler(), FdmSchemeDesc.MethodOfLines(), FdmSchemeDesc.TrBDF2(),
                ];
                check.Output(c, "type", named.Select(d => (object?)d.Type).ToArray());
                check.Output(c, "theta", named.Select(d => (object?)d.Theta).ToArray());
                check.Output(c, "mu", named.Select(d => (object?)d.Mu).ToArray());
                continue;
            }

            CheckRollback(check, c);
        }

        check.Complete();
    }
}
