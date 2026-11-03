// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>
/// What a rendered repro carries beside its calls: for C#, the bound engine the shared emitter's adapters read, a
/// generator's draws as their loop, a matrix from rows, and the outcome written in Excel's terms; for Python, the same
/// outcome over the Python layer. Each writes a result as a grid of cells: a double's bits, a text, a boolean, an
/// object's type, an error's code (#N/A 42, #NUM! 36), as the add-in's output conversion spills it.
/// </summary>
internal static class XlReproText
{
    /// <summary>The C# program's helpers, after its calls.</summary>
    public const string CSharpHelpers = """

        public static partial class Repro
        {
            sealed class ElemNA;

            sealed record ElemError(int Code);

            sealed class ElemThrows;

            // A call the add-in refuses: a range where one value goes, which its loop mask doesn't loop over, or a value its
            // conversions refuse. Dynamic, so the calls after it compile; none runs.
            static dynamic Refused(string message) => throw new ArgumentException(message);

            // A looped call's element: its value, or a throw as Excel shows a failed element, an error in its place.
            static object Elem(Func<object> f)
            {
                try
                {
                    return f();
                }
                catch (Exception)
                {
                    return new ElemThrows();
                }
            }

            static NanoQL.Numerics.Matrix Mat(double[,] a)
            {
                var m = new NanoQL.Numerics.Matrix(a.GetLength(0), a.GetLength(1));
                for (int i = 0; i < a.GetLength(0); i++) for (int j = 0; j < a.GetLength(1); j++) m[i, j] = a[i, j];
                return m;
            }

            static string Bits(double d) => BitConverter.DoubleToInt64Bits(d).ToString("X16");

            static string Cell(object o, int shift) => o switch
            {
                null => "{\"n\": true}",
                ElemNA => "{\"e\": 42}",
                ElemError e => $"{{\"e\": {e.Code}}}",
                ElemThrows => "{\"t\": true}",
                double d when !double.IsFinite(d) => "{\"e\": 36}",
                double d => $"{{\"d\": \"{Bits(d)}\"}}",
                int i => $"{{\"d\": \"{Bits(i)}\"}}",
                long l => $"{{\"d\": \"{Bits(l)}\"}}",
                bool b => $"{{\"b\": {(b ? "true" : "false")}}}",
                string s => $"{{\"s\": {System.Text.Json.JsonSerializer.Serialize(s)}}}",
                NanoQL.Time.Date dt => $"{{\"d\": \"{Bits(dt.SerialNumber - shift)}\"}}",
                NanoQL.Time.Period p => $"{{\"s\": {System.Text.Json.JsonSerializer.Serialize(NanoQL.Time.PeriodFunctions.ShortPeriod(p))}}}",
                Enum e => $"{{\"s\": {System.Text.Json.JsonSerializer.Serialize(e.ToString())}}}",
                System.Numerics.Complex c => $"{{\"c\": [\"{Bits(c.Real)}\", \"{Bits(c.Imaginary)}\"]}}",
                _ => $"{{\"h\": \"{o.GetType().Name.Split('`')[0]}\"}}",
            };

            static string Grid(List<List<string>> rows)
            {
                int cols = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
                var cells = rows.SelectMany(r => r.Concat(Enumerable.Repeat("{\"e\": 42}", cols - r.Count)));
                return $"{{\"rows\": {rows.Count}, \"cols\": {cols}, \"cells\": [{string.Join(", ", cells)}]}}";
            }

            // The outcome as the add-in spills it: a matrix by rows, a tuple across a row (a sequence item down a column of
            // its own, short columns padded #N/A), a map as two columns, a collection of tuples a row per element, a jagged
            // array's rows padded #N/A, any other collection down a column.
            static string Show(object o, int shift)
            {
                switch (o)
                {
                    case NanoQL.Numerics.Matrix m:
                        return Grid(Enumerable.Range(0, m.Rows).Select(i => Enumerable.Range(0, m.Columns).Select(j => Cell(m[i, j], shift)).ToList()).ToList());
                    case double[,] a:
                        return Grid(Enumerable.Range(0, a.GetLength(0)).Select(i => Enumerable.Range(0, a.GetLength(1)).Select(j => Cell(a[i, j], shift)).ToList()).ToList());
                    case object[,] g:
                        return Grid(Enumerable.Range(0, g.GetLength(0)).Select(i => Enumerable.Range(0, g.GetLength(1)).Select(j => Cell(g[i, j], shift)).ToList()).ToList());
                    case System.Runtime.CompilerServices.ITuple t:
                    {
                        var cols = Enumerable.Range(0, t.Length).Select(k => t[k] is System.Collections.IEnumerable e && t[k] is not string
                            ? e.Cast<object>().Select(x => Cell(x, shift)).ToList() : new List<string> { Cell(t[k], shift) }).ToList();
                        int n = cols.Max(c => c.Count);
                        return Grid(Enumerable.Range(0, n).Select(i => cols.Select(c => i < c.Count ? c[i] : "{\"e\": 42}").ToList()).ToList());
                    }

                    case System.Collections.IEnumerable e when o is not string:
                    {
                        var items = e.Cast<object>().ToList();
                        if (items.Count > 0 && items[0] is not null && items[0].GetType().IsGenericType && items[0].GetType().GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                        {
                            return Grid(items.Select(kv => new List<string> { Cell(kv.GetType().GetProperty("Key")!.GetValue(kv), shift), Cell(kv.GetType().GetProperty("Value")!.GetValue(kv), shift) }).ToList());
                        }

                        if (items.Count > 0 && items.Any(x => x is System.Runtime.CompilerServices.ITuple))
                        {
                            int w = items.OfType<System.Runtime.CompilerServices.ITuple>().Max(x => x.Length);
                            return Grid(items.Select(x => x is System.Runtime.CompilerServices.ITuple tu
                                ? Enumerable.Range(0, tu.Length).Select(k => Cell(tu[k], shift)).ToList()
                                : Enumerable.Repeat("{\"e\": 42}", w).ToList()).ToList());
                        }

                        if (items.Count > 0 && items.All(x => x is IEnumerable<double>))
                        {
                            return Grid(items.Select(r => ((IEnumerable<double>)r).Select(x => Cell(x, shift)).ToList()).ToList());
                        }

                        return Grid(items.Select(x => new List<string> { Cell(x, shift) }).ToList());
                    }

                    default:
                        return Grid([[Cell(o, shift)]]);
                }
            }
        }

        // A bound engine's inputs, which the shared emitter's adapters read, as the add-in's runtime holds them.
        public sealed class BoundEngine(string set, object instrument, IReadOnlyDictionary<string, object> inputs)
        {
            public string Set { get; } = set;

            public object Instrument { get; } = instrument;

            public T Take<T>(string name) =>
                inputs.TryGetValue(name, out object v) ? (T)v : throw new ArgumentException($"engine: '{name}' is neither bound nor supplied", "engine");

            public T TakeOr<T>(string name, T fallback) => inputs.TryGetValue(name, out object v) ? (T)v : fallback;

            public void Open(string name)
            {
                if (inputs.ContainsKey(name)) throw new ArgumentException($"engine: '{name}' is bound, and the member supplies it", "engine");
            }

            public void NoInstrument()
            {
                if (Instrument != null) throw new ArgumentException("engine: bound to an instrument (BindOn), and the member supplies one; bind it with Bind", "engine");
            }

            public object NeedInstrument() =>
                Instrument ?? throw new ArgumentException("engine: this member takes an engine bound to its instrument; bind it with BindOn", "engine");

            public ArgumentException NoCalculation(string member) =>
                new($"engine: {Set[..Set.LastIndexOf('#')]}, bound with these inputs, has no calculation {member} can take", "engine");

            public ArgumentException Refuse(string reason) => new($"engine: {Set[..Set.LastIndexOf('#')]}: {reason}", "engine");
        }

        public sealed class DrawsResult(object values, double[] weights, object next)
        {
            public object Values { get; } = values;

            public double[] Weights { get; } = weights;

            public object Next { get; } = next;
        }

        // A generator's draws from a copy of it, the generator itself never drawn from.
        public static class Draws
        {
            public static DrawsResult Of(NanoQL.Numerics.IRng g, int n)
            {
                var c = (NanoQL.Numerics.IRng)g.Clone();
                var v = new double[n];
                var w = new double[n];
                for (int i = 0; i < n; i++)
                {
                    var s = c.Next();
                    v[i] = s.Value;
                    w[i] = s.Weight;
                }

                return new DrawsResult(v, w, c);
            }

            // A sequence generator's: a draw per row.
            public static DrawsResult OfSequences(NanoQL.Numerics.IRsg g, int n)
            {
                var c = (NanoQL.Numerics.IRsg)g.Clone();
                int d = c.Dimension;
                var v = new double[n, d];
                var w = new double[n];
                for (int i = 0; i < n; i++)
                {
                    var s = c.NextSequence();
                    var sp = s.Value.Span;
                    for (int j = 0; j < d; j++) v[i, j] = sp[j];
                    w[i] = s.Weight;
                }

                return new DrawsResult(v, w, c);
            }
        }
        """;

    /// <summary>The Python script's head, before its calls: nanoql's Python layer and the outcome's grid.</summary>
    public const string PythonHead = """
        # nqlxlRepro: a cell's nanoql calls as Python, over nanoql's Python package and NumPy; it prints the cell's
        # outcome as a grid of cells, each a double's bits, a text, a boolean, an object's type or an error's code.
        import datetime, enum, json, os, struct, tempfile
        import numpy as np
        import nanoql
        import nanoql.experimental

        _temps = []
        def _tmp(text):
            fd, p = tempfile.mkstemp(suffix=".xml", prefix="nqlxl-repro-")
            with os.fdopen(fd, "w", encoding="utf-8") as f:
                f.write(text)
            _temps.append(p)
            return p

        def _d(serial):
            return datetime.date(1899, 12, 30) + datetime.timedelta(days=serial)

        def _e(enum, name):
            return next(m for m in enum if m.name.lower() == name.lower())

        def _bits(x):
            return struct.pack(">d", float(x)).hex().upper()

        class _NA: pass
        class _Throws: pass
        class _Err:
            def __init__(self, code):
                self.code = code

        def _elem(f):
            try:
                return f()
            except Exception:
                return _Throws()

        def _raise(message):
            raise ValueError(message)

        def _cell(o, shift):
            if isinstance(o, _NA):
                return {"e": 42}
            if isinstance(o, _Err):
                return {"e": o.code}
            if isinstance(o, _Throws):
                return {"t": True}
            if o is None:
                return {"n": True}
            if isinstance(o, enum.Enum):
                return {"s": o.name}
            if isinstance(o, (bool, np.bool_)):
                return {"b": bool(o)}
            if isinstance(o, (float, np.floating)) and not np.isfinite(o):
                return {"e": 36}
            if isinstance(o, (int, float, np.integer, np.floating)):
                return {"d": _bits(o)}
            if isinstance(o, complex):
                return {"c": [_bits(o.real), _bits(o.imag)]}
            if isinstance(o, str):
                return {"s": o}
            if isinstance(o, datetime.date):
                return {"d": _bits((o - datetime.date(1899, 12, 30)).days - shift)}
            if hasattr(o, "name") and hasattr(o, "value") and type(o).__module__.startswith("nanoql"):
                return {"s": o.name}
            return {"h": type(o).__name__.split("_")[0] if type(o).__name__.startswith(("TimeSeries", "PiecewiseYieldCurve")) else type(o).__name__}

        def _grid(rows):
            cols = max((len(r) for r in rows), default=0)
            return {"rows": len(rows), "cols": cols, "cells": [c for r in rows for c in r + [{"e": 42}] * (cols - len(r))]}

        def _show(o, shift):
            if isinstance(o, np.ndarray):
                a = o.reshape(-1, 1) if o.ndim == 1 else o
                return _grid([[_cell(x, shift) for x in row] for row in a.tolist()])
            if isinstance(o, tuple):
                cols = [[_cell(x, shift) for x in (e.tolist() if isinstance(e, np.ndarray) else e)] if isinstance(e, (list, np.ndarray)) else [_cell(e, shift)] for e in o]
                n = max(len(c) for c in cols)
                return _grid([[c[i] if i < len(c) else {"e": 42} for c in cols] for i in range(n)])
            if isinstance(o, dict):
                return _grid([[_cell(k, shift), _cell(v, shift)] for k, v in o.items()])
            if isinstance(o, list):
                if o and any(isinstance(x, tuple) for x in o) and all(isinstance(x, tuple) or x is None for x in o):
                    w = max(len(x) for x in o if isinstance(x, tuple))
                    return _grid([[_cell(e, shift) for e in x] if isinstance(x, tuple) else [{"e": 42}] * w for x in o])
                if o and all(isinstance(x, (list, np.ndarray)) for x in o):
                    return _grid([[_cell(x, shift) for x in (r.tolist() if isinstance(r, np.ndarray) else r)] for r in o])
                return _grid([[_cell(x, shift)] for x in o])
            return _grid([[_cell(o, shift)]])
        """;
}
