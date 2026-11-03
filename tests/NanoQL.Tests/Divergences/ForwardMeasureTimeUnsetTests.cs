// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A forward-measure process built without its forward measure time: QuantLib's default constructors leave <c>T_</c>
/// uninitialized (<c>forwardmeasureprocess.hpp:42</c>, <c>:46</c>, <c>:60</c>, <c>:64</c>), so the forward Hull-White
/// and G2 processes' drift and expectation read an indeterminate value, undefined behaviour (the playbook's
/// undefined-behaviour row). nanoql's refuse with "forward measure time not set" until <c>WithForwardMeasureTime</c>
/// gives one.
/// </summary>
public class ForwardMeasureTimeUnsetTests
{
    [Fact]
    public void AnUnsetForwardMeasureTimeIsRefused()
    {
        var today = new Date(15, Month.January, 2025);
        var curve = new FlatForward(today, 0.04, new Actual365Fixed());
        var hullWhite = new HullWhiteForwardProcess(curve, 0.1, 0.01);
        var g2 = new G2ForwardProcess(0.1, 0.01, 0.2, 0.013, -0.5, curve);
        Vector x0 = g2.InitialValues();
        foreach (Action read in new Action[]
                 {
                     () => hullWhite.Drift(1.0, 0.04),
                     () => hullWhite.Expectation(0.0, 0.04, 0.5),
                     () => g2.Drift(1.0, x0),
                     () => g2.Expectation(0.0, x0, 0.5),
                 })
        {
            Assert.Equal("forward measure time not set", Assert.Throws<NanoQLException>(read).Message);
        }
    }
}
