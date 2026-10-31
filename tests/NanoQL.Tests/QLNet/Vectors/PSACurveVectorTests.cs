// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.QLNet;

/// <summary>
/// The <see cref="PSACurve"/>'s vectors, <c>tests/vectors/qlnet/Instruments/Bonds/PSACurve.json</c>, QLNet 1.13.2's:
/// the CPR and SMM at ages 0 to 360 months from two starts, and at month ends from the first, at eight speeds from 0 to
/// 1,600% PSA. Each case lists its dates: the generator leaves out those where QLNet's month isn't SF-6's, which
/// <c>Divergences/QLNet/PsaMonthCountTests</c> covers.
/// </summary>
[Trait("Oracle", "QLNet")]
public class PSACurveVectorTests
{
    [Fact]
    public void PSACurveVector()
    {
        var file = VectorFile.Load("qlnet/Instruments/Bonds/PSACurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var curve = new PSACurve(new Date(c.Int("startDate")), c.Double("multiplier"));
            Date[] dates = c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32())).ToArray();
            check.Output(c, "CPR", dates.Select(curve.GetCPR).ToArray());
            check.Output(c, "SMM", dates.Select(curve.GetSMM).ToArray());
        }

        check.Complete();
    }
}
