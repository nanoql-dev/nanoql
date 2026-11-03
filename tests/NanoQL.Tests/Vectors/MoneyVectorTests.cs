// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Money vectors, <c>tests/vectors/money.json</c>: single amounts' inspectors, rounding, text and arithmetic with a
/// number; and pairs' sums, differences, ratios and comparisons under each conversion type.
/// </summary>
/// <remarks>
/// Under <see cref="MoneyConversionType.NoConversion"/> a pair is checked through the operators; under the conversion
/// types, through the methods that take the context (<c>Conventions/MoneyConversionTests.cs</c> checks that the methods
/// answer as the operators under no conversion). QuantLib's <c>==</c> and <c>!=</c> throw on a currency mismatch under
/// no conversion, where nanoql's return <see langword="false"/> and <see langword="true"/>: those two outputs map
/// QuantLib's throw to nanoql's values.
/// </remarks>
[Trait("Oracle", "QuantLib")]
public class MoneyVectorTests
{
    private const string U7 = "U7: == and != compare amounts in different currencies as unequal";

    [Fact]
    public void MoneyVector()
    {
        var file = VectorFile.Load("money");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("money", out JsonElement single))
            {
                Money m = MoneyOf(single);
                double factor = c.Double("factor");
                check.Output(c, "currency", m.Currency?.Code);
                check.Output(c, "value", m.Value);
                check.Output(c, "rounded", () => m.Rounded().Value);
                check.Output(c, "text", () => m.ToString());
                Outputs(check, c, "negated", () => -m);
                Outputs(check, c, "timesFactor", () => m * factor);
                Outputs(check, c, "factorTimes", () => factor * m);
                Outputs(check, c, "dividedBy", () => m / factor);
                continue;
            }

            Money m1 = MoneyOf(c.Input("m1")), m2 = MoneyOf(c.Input("m2"));
            if (context.MoneySettings.ConversionType == MoneyConversionType.NoConversion)
            {
                Outputs(check, c, "sum", () => m1 + m2);
                Outputs(check, c, "difference", () => m1 - m2);
                check.Output(c, "ratio", () => m1 / m2);
                check.Mapped(c, "eq", () => m1 == m2, false, U7);
                check.Mapped(c, "ne", () => m1 != m2, true, U7);
                check.Output(c, "lt", () => m1 < m2);
                check.Output(c, "le", () => m1 <= m2);
                check.Output(c, "gt", () => m1 > m2);
                check.Output(c, "ge", () => m1 >= m2);
                continue;
            }

            Outputs(check, c, "sum", () => m1.Add(m2, context));
            Outputs(check, c, "difference", () => m1.Subtract(m2, context));
            check.Output(c, "ratio", () => m1.Divide(m2, context));
            check.Output(c, "eq", () => m1.Equals(m2, context));
            check.Output(c, "ne", () => !m1.Equals(m2, context));
            check.Output(c, "lt", () => m1.LessThan(m2, context));
            check.Output(c, "le", () => m1.LessThanOrEqual(m2, context));
            check.Output(c, "gt", () => m1.GreaterThan(m2, context));
            check.Output(c, "ge", () => m1.GreaterThanOrEqual(m2, context));
        }

        check.Complete();
    }

    /// <summary>An amount as the vectors write it: a currency's code, or null for the empty currency, and a
    /// value.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The amount.</returns>
    internal static Money MoneyOf(JsonElement e)
    {
        JsonElement code = e.GetProperty("currency");
        return new Money(
            VectorCase.Real(e.GetProperty("value")),
            code.ValueKind == JsonValueKind.Null ? null : VectorCurrencies.ByCode(code.GetString()!));
    }

    // A money output's two outputs, "<name>Currency" and "<name>Value", both a throw where the amount throws.
    private static void Outputs(VectorAssert check, VectorCase c, string name, Func<Money> f)
    {
        check.Output(c, name + "Currency", () => f().Currency?.Code);
        check.Output(c, name + "Value", () => f().Value);
    }
}
