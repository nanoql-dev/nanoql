// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit.Sdk;

namespace NanoQL.Tests.Harness;

/// <summary>The vector harness's own tests: each comparison passes where it should and fails where it should.</summary>
public class HarnessTests
{
    [Fact]
    public void ValueInsideTheToleranceTimesTheScalePasses()
    {
        var file = One("""{ "x": 1.0, "y": { "value": 100.0, "scale": 1000.0 } }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "x", 1.0 + 0.9e-10);
        check.Output(file.Cases[0], "y", 100.0 + 0.9e-7);

        Assert.Empty(check.Mismatches);
        check.Complete();
    }

    [Fact]
    public void ValueAtTwiceTheToleranceTimesTheScaleFails()
    {
        var file = One("""{ "x": 1.0, "y": { "value": 100.0, "scale": 1000.0 } }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "x", 1.0 + 2e-10);
        check.Output(file.Cases[0], "y", 100.0 + 2e-7);

        Assert.Equal(2, check.Mismatches.Count);
        Assert.Throws<FailException>(check.Complete);
    }

    [Fact]
    public void ScaledAmountUsesItsOwnScaleNotOne()
    {
        var file = One("""{ "scaled": { "value": 1234.5, "scale": 1000000.0 }, "plain": 1234.5 }""");
        var check = new VectorAssert(file);

        // 5e-5 is inside 1e-10 x 1e6 = 1e-4, and outside 1e-10 x 1234.5.
        check.Output(file.Cases[0], "scaled", 1234.5 + 5e-5);
        check.Output(file.Cases[0], "plain", 1234.5 + 5e-5);

        Assert.Single(check.Mismatches);
        Assert.Contains("plain", check.Mismatches[0], StringComparison.Ordinal);
    }

    [Fact]
    public void NullMatchesOnlyNull()
    {
        var file = One("""{ "a": null, "b": null, "c": 0.0, "d": null }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", null);
        check.Output(file.Cases[0], "b", 0.0);
        check.Output(file.Cases[0], "c", null);
        check.Output(file.Cases[0], "d", (int?)0);

        Assert.Equal(3, check.Mismatches.Count);
        Assert.DoesNotContain(check.Mismatches, m => m.Contains("› a:", StringComparison.Ordinal));
    }

    [Fact]
    public void NaNMatchesOnlyNaN()
    {
        var file = One("""{ "a": "NaN", "b": "NaN", "c": 1.0 }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", double.NaN);
        check.Output(file.Cases[0], "b", 1.0);
        check.Output(file.Cases[0], "c", double.NaN);

        Assert.Equal(2, check.Mismatches.Count);
    }

    [Fact]
    public void ANaNOfEitherSignIsBitIdentical()
    {
        var file = One("""{ "a": "NaN", "b": "NaN" }""");
        var check = new VectorAssert(file);

        // the reader's NaN with its sign bit flipped: a file's "NaN" carries no sign
        check.Output(file.Cases[0], "a", double.NaN);
        check.Output(file.Cases[0], "b", BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(double.NaN) ^ long.MinValue));

        Assert.Empty(check.Mismatches);
        Assert.Equal(2, check.BitIdentical);
    }

    [Fact]
    public void InfinityMatchesOnlyPositiveInfinity()
    {
        var file = One("""{ "a": "Infinity", "b": "Infinity", "c": "Infinity", "d": "-Infinity" }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", double.PositiveInfinity);
        check.Output(file.Cases[0], "b", double.NegativeInfinity);
        check.Output(file.Cases[0], "c", double.MaxValue);
        check.Output(file.Cases[0], "d", double.NegativeInfinity);

        Assert.Equal(2, check.Mismatches.Count);
    }

    [Fact]
    public void ExpectedThrowFailsWhenNothingIsThrown()
    {
        var file = One("""{ "a": { "throws": true } }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", () => 1.0);

        Assert.Single(check.Mismatches);
        Assert.Throws<FailException>(check.Complete);
    }

    [Fact]
    public void ExpectedThrowNeedsAnArgumentOrNanoQLException()
    {
        var file = One("""{ "a": { "throws": true }, "b": { "throws": true }, "c": { "throws": true } }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", () => throw new ArgumentOutOfRangeException("x"));
        check.Output(file.Cases[0], "b", () => throw new NanoQLException("negative time"));
        check.Output(file.Cases[0], "c", () => throw new InvalidOperationException("unrelated"));

        Assert.Single(check.Mismatches);
        Assert.Contains("› c:", check.Mismatches[0], StringComparison.Ordinal);
    }

    [Fact]
    public void UnexpectedThrowFails()
    {
        var file = One("""{ "a": 1.0 }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", () => throw new NanoQLException("boom"));

        Assert.Single(check.Mismatches);
    }

    [Fact]
    public void StringMismatchFails()
    {
        var file = One("""{ "a": "Actual/360", "b": "Actual/360" }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", "Actual/360");
        check.Output(file.Cases[0], "b", "Actual/365 (Fixed)");

        Assert.Single(check.Mismatches);
    }

    [Fact]
    public void IntegersAndBooleansCompareExactly()
    {
        var file = One("""{ "a": 46310, "b": 46310, "c": true, "d": true, "e": 3 }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", 46310);
        check.Output(file.Cases[0], "b", 46311);
        check.Output(file.Cases[0], "c", true);
        check.Output(file.Cases[0], "d", false);
        check.Output(file.Cases[0], "e", DayOfWeek.Wednesday);

        Assert.Equal(2, check.Mismatches.Count);
    }

    [Fact]
    public void OutputTheTestDoesNotCompareFailsTheFile()
    {
        var file = One("""{ "a": 1.0, "b": 2.0 }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", 1.0);

        Assert.Empty(check.Mismatches);
        var failure = Assert.Throws<FailException>(check.Complete);
        Assert.Contains("› b: not compared", failure.Message, StringComparison.Ordinal);
        Assert.Contains("compared 1 outputs of the 2", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArrayLengthMismatchFails()
    {
        var file = One("""{ "a": [1.0, 2.0, 3.0], "b": [1.0, 2.0] }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", new[] { 1.0, 2.0 });
        check.Output(file.Cases[0], "b", new[] { 1.0, 2.0 });

        Assert.Single(check.Mismatches);
        Assert.Equal(5, check.Compared);
    }

    [Fact]
    public void ArrayElementsCountAsOutputsAndMayThrow()
    {
        var file = One("""{ "a": [[1, 2], [{ "throws": true }, 4]], "b": { "throws": true } }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", new object[] { new[] { 1, 2 }, new Func<object?>[] { () => throw new ArgumentException("x"), () => 4 } });
        check.Mapped(file.Cases[0], "b", () => false, false, "a documented divergence");

        Assert.Empty(check.Mismatches);
        Assert.Equal(5, file.Outputs);
        Assert.Equal(1, check.MappedOutputs);
        check.Complete();
    }

    [Fact]
    public void MappedThrowComparesTheDocumentedValue()
    {
        var file = One("""{ "a": { "throws": true }, "b": { "throws": true } }""");
        var check = new VectorAssert(file);

        check.Mapped(file.Cases[0], "a", true, false, "a documented divergence");
        check.Mapped(file.Cases[0], "b", () => throw new NanoQLException("x"), false, "a documented divergence");

        Assert.Equal(2, check.Mismatches.Count);
    }

    [Fact]
    public void OutputComparedTwiceFails()
    {
        var file = One("""{ "a": 1.0 }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", 1.0);
        check.Output(file.Cases[0], "a", 1.0);

        Assert.Single(check.Mismatches);
        Assert.Contains("› a: compared twice", check.Mismatches[0], StringComparison.Ordinal);
    }

    [Fact]
    public void OutputTheCaseDoesNotHaveFails()
    {
        var file = One("""{ "a": 1.0 }""");
        var check = new VectorAssert(file);

        check.Output(file.Cases[0], "a", 1.0);
        check.Output(file.Cases[0], "b", 1.0);

        Assert.Single(check.Mismatches);
        Assert.Contains("› b: the case has no such output", check.Mismatches[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ToleranceOverrideIsHonouredAndCounted()
    {
        var file = One("""{ "a": { "value": 1.0, "scale": 1.0, "tolerance": 1e-6, "reason": "measured" }, "b": 1.0 }""");
        var check = new VectorAssert(file);

        // 5e-7 is inside the override's 1e-6 and outside the default 1e-10.
        check.Output(file.Cases[0], "a", 1.0 + 5e-7);
        check.Output(file.Cases[0], "b", 1.0 + 5e-7);

        Assert.Equal(1, file.OverriddenOutputs);
        Assert.Single(check.Mismatches);
        Assert.Contains("› b:", check.Mismatches[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AQLNetFileCarriesTheQLNetPinAndNoQuantLibOne()
    {
        var file = VectorFile.Parse(
            """
            { "upstream": "src/QLNet/Instruments/Bonds/PSACurve.cs", "qlnet": "1.13.2", "generator": "test", "seed": 1,
              "cases": [ { "name": "case", "inputs": { "settings": {} }, "expected": { "a": 1.0 } } ] }
            """);

        Assert.Equal("1.13.2", file.QLNet);
        Assert.Null(file.QuantLib);
        Assert.Equal(1, file.Outputs);
    }

    [Theory]
    [InlineData("\"quantlib\": \"1.44\", \"qlnet\": \"1.13.2\",")]
    [InlineData("")]
    public void AFileWithBothPinsOrNeitherIsRefused(string pins)
    {
        string json = $$"""
            { "upstream": null, {{pins}} "generator": "test", "seed": 1,
              "cases": [ { "name": "case", "inputs": { "settings": {} }, "expected": { "a": 1.0 } } ] }
            """;

        var e = Assert.Throws<InvalidOperationException>(() => VectorFile.Parse(json));
        Assert.Contains("one pin field, quantlib or qlnet", e.Message, StringComparison.Ordinal);
    }

    private static VectorFile One(string expected) => VectorFile.Parse(
        $$"""
        { "upstream": null, "quantlib": "1.44", "generator": "test", "seed": 1,
          "cases": [ { "name": "case", "inputs": { "settings": {} }, "expected": {{expected}} } ] }
        """);
}
