// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// GSR's numeraire time (the S-g1d plan's G1R1 (iv)): QuantLib's <c>Gsr::numeraireTime(T)</c> moves the model's process
/// in place (<c>gsr.hpp:196-199</c>); here <c>WithNumeraireTime</c> returns a copy at the new time, through the
/// process's <c>WithForwardMeasureTime</c>, and the receiver keeps its own.
/// </summary>
public class GsrNumeraireTimeTests
{
    [Fact]
    public void WithNumeraireTimeReturnsACopyAndLeavesTheReceiver()
    {
        var today = new Date(15, Month.January, 2025);
        Gsr model = Gaussian1dInputs.Constant(new FlatForward(today, 0.03, new Actual365Fixed()), 0.01, 0.01);
        Gsr copy = model.WithNumeraireTime(40.0);

        Assert.Equal(40.0, copy.NumeraireTime);
        Assert.Equal(40.0, ((GsrProcess)copy.StateProcess()).ForwardMeasureTime);
        Assert.Equal(60.0, model.NumeraireTime);
        Assert.Equal(60.0, ((GsrProcess)model.StateProcess()).ForwardMeasureTime);

        // A zero bond past the copy's numeraire time is refused there, and priced on the receiver.
        Assert.Throws<ArgumentException>(() => copy.Zerobond(50.0, 1.0, 0.0));
        Assert.True(model.Zerobond(50.0, 1.0, 0.0) > 0.0);
        Assert.Equal(model.Volatility, copy.Volatility);
        Assert.Equal(model.Reversion, copy.Reversion);
    }
}
