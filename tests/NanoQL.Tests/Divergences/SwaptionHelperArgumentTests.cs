// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A swaption helper on a null index. QuantLib's constructors keep the index unchecked and its first calculation
/// dereferences it (<c>swaptionhelper.cpp:153</c>), which the wheel's build refuses with "Boost assertion failed: px !=
/// 0" at the helper's first use, its construction succeeding. nanoql's helper computes at construction and refuses the
/// null index there, with <see cref="ArgumentNullException"/> naming it, in each constructor.
/// </summary>
public class SwaptionHelperArgumentTests
{
    [Fact]
    public void ANullIndexIsRefusedAtConstruction()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var curve = new FlatForward(context.EvaluationDate, 0.03, new Actual365Fixed());
        var exercise = new Date(15, Month.January, 2026);
        var tenor = new Period(1, TimeUnit.Years);

        Action[] constructors =
        [
            () => _ = new SwaptionHelper(tenor, new Period(5, TimeUnit.Years), 0.2, null!, tenor, null, null, curve, context),
            () => _ = new SwaptionHelper(exercise, new Period(5, TimeUnit.Years), 0.2, null!, tenor, null, null, curve, context),
            () => _ = new SwaptionHelper(exercise, exercise + 1825, 0.2, null!, tenor, null, null, curve, context),
        ];
        foreach (Action constructor in constructors)
        {
            Assert.Equal("index", Assert.Throws<ArgumentNullException>(constructor).ParamName);
        }
    }
}
