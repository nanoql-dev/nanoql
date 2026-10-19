// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The bond vectors, <c>tests/vectors/instruments/bond.json</c>: bonds built by QuantLib's two public constructors,
/// their inspectors with no date and at 20 dates, the prices for a yield and the yield for a price, and their
/// discounting engine's results with the members that read them. A refused construction is the case's one output,
/// "construct".
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BondVectorTests
{
    [Fact]
    public void BondVector()
    {
        var file = VectorFile.Load("instruments/bond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => BondInputs.BondOf(c.Input("bond"), context));
                continue;
            }

            Bond bond = BondInputs.BondOf(c.Input("bond"), context);
            BondInputs.CheckStatic(check, c, bond, context);
            BondInputs.CheckDated(check, c, bond, context);
            BondInputs.CheckEngine(check, c, bond, context);
        }

        check.Complete();
    }
}
