// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The constant estimator's vectors, <c>tests/vectors/models/volatility/constantestimator.json</c>: twelve volatility
/// series through windows of 1 to 10 values, the output's dates and values.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConstantEstimatorVectorTests
{
    [Fact]
    public void ConstantEstimatorVector()
    {
        var file = VectorFile.Load("models/volatility/constantestimator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            TimeSeries<double> result = new ConstantEstimator(c.Int("size")).Calculate(new TimeSeries<double>(c.DateInputs("dates"), c.Doubles("values")));
            check.Output(c, "dates", result.Dates().Select(d => (object?)d));
            check.Output(c, "values", result.Values());
        }

        check.Complete();
    }
}
