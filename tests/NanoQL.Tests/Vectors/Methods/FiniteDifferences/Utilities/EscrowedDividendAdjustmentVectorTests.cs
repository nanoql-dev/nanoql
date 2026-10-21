// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The EscrowedDividendAdjustment vectors,
/// <c>tests/vectors/methods/finitedifferences/utilities/escroweddividendadjustment.json</c>:
/// <see cref="EscrowedDividendAdjustment"/> and <see cref="FdmEscrowedLogInnerValueCalculator"/> read through the
/// finite-difference engine's escrowed cash-dividend model, its NPV, delta, gamma and theta for European, American and
/// Bermudan options over four schedules on two processes; and its refusals of a non-positive adjusted spot and of a
/// quanto helper.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EscrowedDividendAdjustmentVectorTests
{
    [Fact]
    public void EscrowedDividendAdjustmentVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/escroweddividendadjustment");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckFdPricing(check, c);
        }

        check.Complete();
    }
}
