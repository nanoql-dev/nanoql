// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="InstrumentResults.Result{T}"/>, QuantLib's <c>Instrument::result&lt;T&gt;</c>: a missing tag is refused
/// with QuantLib's "&lt;tag&gt; not provided" as <see cref="NanoQLException"/> (<c>instrument.hpp:192-200</c>), and a
/// result of another type with <see cref="InvalidCastException"/>, .NET's type for a failed cast, where QuantLib's
/// <c>std::any_cast</c> throws <c>std::bad_any_cast</c>: the failure is the type argument's, not a value's, so it is
/// neither of the two error types a vector's throw stands for, and no vector asks for it.
/// </summary>
public class InstrumentResultTests
{
    [Fact]
    public void AResultIsReadByItsTagAndType()
    {
        var results = new InstrumentResults
        {
            NPV = 1.0,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("vega", 0.25).Add("count", 3),
        };

        Assert.Equal(0.25, results.Result<double>("vega"));
        Assert.Equal(3, results.Result<int>("count"));
        Assert.Equal(0.25, results.Result<double?>("vega"));

        Assert.Equal("delta not provided", Assert.Throws<NanoQLException>(() => results.Result<double>("delta")).Message);
        Assert.Throws<InvalidCastException>(() => results.Result<int>("vega"));
        Assert.Throws<InvalidCastException>(() => results.Result<string>("count"));
        Assert.Equal("tag", Assert.Throws<ArgumentNullException>(() => results.Result<double>(null!)).ParamName);

        // An expired instrument's results hold none.
        Assert.Equal("vega not provided", Assert.Throws<NanoQLException>(() => new InstrumentResults().Result<double>("vega")).Message);
    }
}
