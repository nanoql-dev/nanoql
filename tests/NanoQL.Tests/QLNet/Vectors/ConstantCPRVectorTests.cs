// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.QLNet;

/// <summary>
/// The <see cref="ConstantCPR"/>'s vectors, <c>tests/vectors/qlnet/Instruments/Bonds/ConstantCPR.json</c>, QLNet
/// 1.13.2's: the CPR and SMM at CPRs from 0 to 1 on three dates.
/// </summary>
[Trait("Oracle", "QLNet")]
public class ConstantCPRVectorTests
{
    [Fact]
    public void ConstantCPRVector()
    {
        var file = VectorFile.Load("qlnet/Instruments/Bonds/ConstantCPR");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var model = new ConstantCPR(c.Double("cpr"));
            Date[] dates = c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32())).ToArray();
            check.Output(c, "CPR", dates.Select(model.GetCPR).ToArray());
            check.Output(c, "SMM", dates.Select(model.GetSMM).ToArray());
        }

        check.Complete();
    }
}
