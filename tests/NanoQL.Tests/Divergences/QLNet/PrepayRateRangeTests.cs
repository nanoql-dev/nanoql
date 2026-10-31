// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A prepayment rate outside SIFMA's range is refused at construction (Chapter SF: an SMM is "the percentage of the
/// mortgage loans outstanding at the beginning of a month assumed to terminate during the month", SF-5, and SF-6 caps
/// the CPR at 100), where QLNet takes it: <c>ConstantCPR(1.01)</c> gives an SMM of NaN, <c>ConstantCPR(-0.01)</c> a
/// negative SMM, and a negative PSA multiplier negative rates (<c>ConstantCPR.cs:25-36</c>, <c>PSACurve.cs:29-46</c>).
/// <see cref="ConstantCPR"/> refuses a CPR outside [0, 1] and <see cref="PSACurve"/> a negative or non-finite
/// multiplier, with <see cref="ArgumentOutOfRangeException"/>. QLNet's values are QLNet 1.13.2's.
/// </summary>
public class PrepayRateRangeTests
{
    [Fact]
    public void AConstantCprOutsideZeroToOneIsRefused()
    {
        // QLNet: ConstantCPR(1.01) CPR 1.01, SMM NaN; ConstantCPR(-0.01) CPR -0.01, SMM -0.0008295381143461622.
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConstantCPR(1.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConstantCPR(-0.01));
        Assert.Equal(1.0, new ConstantCPR(1.0).GetSMM(new Date(1, March, 2007)));
        Assert.Equal(0.0, new ConstantCPR(0.0).GetSMM(new Date(1, March, 2007)));
    }

    [Fact]
    public void ANegativePsaMultiplierIsRefused()
    {
        // QLNet: PSACurve(2007-03-01, -1) at MONTH 6 CPR -0.012, SMM -0.0009945418011427787.
        Assert.Throws<ArgumentOutOfRangeException>(() => new PSACurve(new Date(1, March, 2007), -1.0));
        Assert.Equal(0.0, new PSACurve(new Date(1, March, 2007), 0.0).GetCPR(new Date(1, August, 2007)));
    }
}
