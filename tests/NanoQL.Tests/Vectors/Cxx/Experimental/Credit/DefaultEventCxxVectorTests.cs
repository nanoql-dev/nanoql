// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's default type, key and event vectors,
/// <c>tests/vectors/cxx/experimental/credit/defaultevent.json</c>: the types and their matches, the keys and their
/// equality, and the events' inspectors, recoveries, matches, occurrence and equality, with the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DefaultEventCxxVectorTests
{
    [Fact]
    public void DefaultEventCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/defaultevent");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            switch (c.Name)
            {
                case "types":
                    {
                        JsonElement[] types = [.. c.Input("types").EnumerateArray()];
                        JsonElement[] failures = [.. c.Input("failures").EnumerateArray()];
                        JsonElement[] all = [.. types, .. c.Input("incoherent").EnumerateArray(), .. failures];
                        JsonElement[] valid = [.. types, .. failures];
                        int[] atomics = [.. c.Input("atomics").EnumerateArray().Select(x => x.GetInt32())];
                        int[] restructurings = [.. c.Input("restructurings").EnumerateArray().Select(x => x.GetInt32())];
                        check.Output(c, "inspect", Each(all, t =>
                        {
                            DefaultType x = PortfolioInputs.TypeOf(t);
                            return new object[] { (int)x.AtomicType, (int)x.RestructuringType, x.IsRestructuring };
                        }));
                        check.Output(c, "containsDefaultType", Each(
                            valid.SelectMany(t => atomics.Select(a => (t, a))),
                            p => PortfolioInputs.TypeOf(p.t).ContainsDefaultType((AtomicDefaultType)p.a)));
                        check.Output(c, "containsRestructuringType", Each(
                            valid.SelectMany(t => restructurings.Select(r => (t, r))),
                            p => PortfolioInputs.TypeOf(p.t).ContainsRestructuringType((RestructuringType)p.r)));
                        check.Output(c, "equals", Each(
                            valid.SelectMany(t1 => valid.Select(t2 => (t1, t2))),
                            p => PortfolioInputs.TypeOf(p.t1) == PortfolioInputs.TypeOf(p.t2)));
                        check.Output(c, "failureToPay", Each(failures, t =>
                        {
                            var f = (FailureToPay)PortfolioInputs.TypeOf(t);
                            return new object[] { f.GracePeriod.Length, (int)f.GracePeriod.Units, f.AmountRequired };
                        }));
                        break;
                    }

                case "keys":
                    {
                        JsonElement[] keys = [.. c.Input("keys").EnumerateArray()];
                        JsonElement[] all = [.. keys, .. c.Input("refused").EnumerateArray()];
                        check.Output(c, "inspect", Each(all, k =>
                        {
                            DefaultProbKey key = PortfolioInputs.KeyOf(k);
                            return new object[] { PortfolioInputs.KeySummary(key), key.Size };
                        }));
                        check.Output(c, "equals", Each(
                            keys.SelectMany(k1 => keys.Select(k2 => (k1, k2))),
                            p => PortfolioInputs.KeyOf(p.k1) == PortfolioInputs.KeyOf(p.k2)));
                        break;
                    }

                case "refused events":
                    check.Output(c, "inspect", Each(c.Input("events").EnumerateArray(), e => Inspect(PortfolioInputs.EventOf(e))));
                    break;

                default:
                    CheckEvent(check, c, context);
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckEvent(VectorAssert check, VectorCase c, EvaluationContext context)
    {
        JsonElement spec = c.Input("event");
        DefaultEvent e = PortfolioInputs.EventOf(spec);
        var seniorities = c.Input("seniorities").EnumerateArray().Select(x => (Seniority)x.GetInt32()).ToArray();
        check.Output(c, "inspect", Inspect(e));
        check.Output(c, "recoveryRate", Each(seniorities, s => e.RecoveryRate(s)));
        if (e.Settlement is { } settlement)
        {
            check.Output(c, "settlementRecoveryRate", Each(seniorities, s => settlement.RecoveryRate(s)));
        }

        check.Output(c, "matchesEventType", Each(
            c.Input("matchTypes").EnumerateArray(), t => e.MatchesEventType(PortfolioInputs.TypeOf(t), context)));
        check.Output(c, "matchesDefaultKey", Each(
            c.Input("matchKeys").EnumerateArray(), k => e.MatchesDefaultKey(PortfolioInputs.KeyOf(k), context)));
        Date[] occurrence = [.. c.Input("occurrence").EnumerateArray().Select(x => new Date(x.GetInt32()))];
        check.Output(c, "hasOccurred", Each(
            occurrence.SelectMany(d => new[] { false, true }.Select(inc => (d, inc))),
            p => e.HasOccurred(context, p.d, p.inc)));
        check.Output(c, "equals", Each(c.Input("others").EnumerateArray(), o => e == PortfolioInputs.EventOf(o)));
        if (e is FailureToPayEvent f)
        {
            check.Output(c, "amountDefaulted", f.AmountDefaulted);
        }
    }

    private static object?[] Inspect(DefaultEvent x) =>
    [
        x.Date, x.IsRestructuring, x.IsDefault, x.HasSettled, x.Settlement?.Date, (int)x.EventSeniority, x.Currency?.Code,
        PortfolioInputs.TypeSummary(x.DefaultType),
    ];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
