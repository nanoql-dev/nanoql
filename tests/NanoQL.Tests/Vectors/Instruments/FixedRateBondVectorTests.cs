// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The fixed-rate bond vectors, <c>tests/vectors/instruments/bonds/fixedratebond.json</c>: 14 bonds, their frequency
/// and day counter, and the outputs every bond file shares (<see cref="BondInputs"/>). A refused construction is the
/// case's one output, "construct".
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FixedRateBondVectorTests
{
    [Fact]
    public void FixedRateBondVector()
    {
        var file = VectorFile.Load("instruments/bonds/fixedratebond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => BondInputs.BondOf(c.Input("bond"), context));
                continue;
            }

            var bond = (FixedRateBond)BondInputs.BondOf(c.Input("bond"), context);
            BondInputs.CheckStatic(check, c, bond, context);
            check.Output(c, "frequency", bond.Frequency);
            check.Output(c, "dayCounter", bond.DayCounter.Name);
            BondInputs.CheckDated(check, c, bond, context);
            BondInputs.CheckEngine(check, c, bond, context);
        }

        check.Complete();
    }
}
