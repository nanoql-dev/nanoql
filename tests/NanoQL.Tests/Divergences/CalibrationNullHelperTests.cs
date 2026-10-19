// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A null helper among a calibration's. QuantLib's <c>calibrate</c> and <c>value</c> take a vector of helper pointers,
/// which may hold a null one, and the calibration function dereferences each at its first evaluation
/// (<c>model.cpp:49-51, 59-61</c>), undefined for a null pointer (the wheel's binding refuses <c>None</c> before the
/// call). nanoql's <c>Calibrate</c> and <c>Value</c> copy the helpers and refuse a null one first, with
/// <see cref="ArgumentException"/>, "null calibration helper", naming <c>helpers</c>.
/// </summary>
public class CalibrationNullHelperTests
{
    [Fact]
    public void ANullHelperIsRefused()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var model = new HullWhite(new FlatForward(context.EvaluationDate, 0.03, new Actual365Fixed()));
        CalibrationHelper[] helpers = [null!];

        ArgumentException calibrate = Assert.Throws<ArgumentException>(() => model.Calibrate(
            helpers, new LevenbergMarquardt(), new EndCriteria(100, 10, 1e-8, 1e-8, 1e-8), context));
        Assert.Equal("helpers", calibrate.ParamName);
        Assert.StartsWith("null calibration helper", calibrate.Message, StringComparison.Ordinal);

        ArgumentException value = Assert.Throws<ArgumentException>(() => model.Value(new Vector([0.1, 0.01]), helpers, context));
        Assert.Equal("helpers", value.ParamName);
        Assert.StartsWith("null calibration helper", value.Message, StringComparison.Ordinal);
    }
}
