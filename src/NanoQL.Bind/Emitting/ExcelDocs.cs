// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// The worksheet functions' help text: each function's description and each argument's, read from the XML documentation
/// the build writes beside an assembly, by the compiler's documentation id; an <c>&lt;inheritdoc/&gt;</c> resolved to
/// its nearest base's, as C# resolves it. Read in the Excel back end alone: nothing enters the shared model or
/// <c>surface.json</c>.
/// </summary>
internal sealed class ExcelDocs
{
    /// <summary>
    /// The longest text: Excel-DNA's registration cuts a function's description and the last argument's to 253
    /// characters, and registration fails past 255 (<c>XlRegistration.cs</c>), so 253 keeps every text whole.
    /// </summary>
    public const int Limit = 253;

    private readonly Dictionary<string, Dictionary<string, XElement>> byAssembly = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the texts cut to <see cref="Limit"/>.</summary>
    public int Cut { get; private set; }

    /// <summary>Gets the summaries taken through <c>&lt;inheritdoc/&gt;</c>.</summary>
    public int Inherited { get; private set; }

    /// <summary>A member's summary and its parameters' texts, or nothing where the member has no documentation.</summary>
    /// <param name="m">The member, or null for the bindings' own functions.</param>
    /// <returns>The summary, or null, and the parameters' texts by name.</returns>
    public (string? Summary, IReadOnlyDictionary<string, string> Params) Of(MemberInfo? m)
    {
        var none = (default(string), (IReadOnlyDictionary<string, string>)new Dictionary<string, string>());
        if (m == null)
        {
            return none;
        }

        XElement? e = Find(m);

        // An <inheritdoc/> is its base's: the cref it names, an override's base method, an interface's member of the
        // same signature, a constructor's base constructor of the same parameters; the nearest first.
        for (int depth = 0; e != null && e.Element("summary") == null && e.Element("inheritdoc") is XElement inh && depth < 8; depth++)
        {
            Inherited++;
            e = inh.Attribute("cref") is XAttribute cref ? FindId(m.Module.Assembly.Location, cref.Value) : Base(m, out m);
        }

        if (e == null)
        {
            return none;
        }

        string? summary = e.Element("summary") is XElement s ? Text(s) : null;
        var ps = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement p in e.Elements("param"))
        {
            if (p.Attribute("name") is XAttribute n)
            {
                ps[n.Value] = Text(p);
            }
        }

        return (string.IsNullOrWhiteSpace(summary) ? null : summary, ps);
    }

    /// <summary>A text cut to Excel's limit at a word, with an ellipsis.</summary>
    /// <param name="s">The text.</param>
    /// <returns>The text, whole or cut.</returns>
    public string Fit(string s)
    {
        if (s.Length <= Limit)
        {
            return s;
        }

        Cut++;
        int at = s.LastIndexOf(' ', Limit - 2);
        return s[..(at > 0 ? at : Limit - 1)] + "…";
    }

    // The documentation's markup as plain text: a cref by its last name, a paramref and a langword by their names.
    private static string Text(XElement e)
    {
        var sb = new StringBuilder();
        foreach (XNode n in e.Nodes())
        {
            switch (n)
            {
                case XText t:
                    sb.Append(t.Value);
                    break;
                case XElement x when x.Name.LocalName is "see" or "seealso":
                    sb.Append(x.Attribute("langword")?.Value ?? (x.Attribute("cref") is XAttribute c ? CrefName(c.Value) : Text(x)));
                    break;
                case XElement x when x.Name.LocalName is "paramref" or "typeparamref":
                    sb.Append(x.Attribute("name")?.Value);
                    break;
                case XElement x:
                    sb.Append(' ').Append(Text(x)).Append(' ');
                    break;
            }
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static string CrefName(string cref)
    {
        string s = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        int paren = s.IndexOf('(', StringComparison.Ordinal);
        if (paren >= 0)
        {
            s = s[..paren];
        }

        s = Regex.Replace(s, @"`+\d+", string.Empty).Replace("#ctor", string.Empty, StringComparison.Ordinal).TrimEnd('.');
        string[] parts = s.Split('.');
        return parts.Length >= 2 && char.IsUpper(parts[^1].FirstOrDefault()) && char.IsUpper(parts[^2].FirstOrDefault()) && cref.StartsWith("M:", StringComparison.Ordinal)
            ? parts[^2] + "." + parts[^1]
            : parts[^1];
    }

    // A getter's or an init accessor's documentation is its property's; a closed generic type's member is documented on
    // the generic definition's member, which has the same metadata token.
    private static IEnumerable<MemberInfo> Candidates(MemberInfo m)
    {
        Type? t = m.DeclaringType;
        if (t != null && t.IsConstructedGenericType)
        {
            Type def = t.GetGenericTypeDefinition();
            MemberInfo? d = def.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .FirstOrDefault(x => x.MetadataToken == m.MetadataToken);
            if (d != null)
            {
                m = d;
                t = def;
            }
        }

        yield return m;
        if (m is MethodInfo mi && mi.IsSpecialName && t != null && (mi.Name.StartsWith("get_", StringComparison.Ordinal) || mi.Name.StartsWith("set_", StringComparison.Ordinal)))
        {
            PropertyInfo? p = t.GetProperty(mi.Name[4..], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (p != null)
            {
                yield return p;
            }
        }
    }

    private XElement? FindId(string assemblyPath, string id)
    {
        Load(assemblyPath).TryGetValue(id, out XElement? e);
        if (e == null)
        {
            foreach (Dictionary<string, XElement> d in byAssembly.Values)
            {
                if (d.TryGetValue(id, out e))
                {
                    break;
                }
            }
        }

        return e;
    }

    private XElement? Base(MemberInfo m, out MemberInfo found)
    {
        found = m;
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        Type? t = m.DeclaringType;
        if (t == null)
        {
            return null;
        }

        static string Sig(MethodBase x) => string.Join(",", x.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));

        // C#'s <inheritdoc/> takes the nearest base's text: the base classes nearest first, then the interfaces.
        var bases = new List<Type>();
        for (Type? b = t.BaseType; b != null; b = b.BaseType)
        {
            bases.Add(b);
        }

        bases.AddRange(t.GetInterfaces());
        foreach (Type b in bases.Select(x => x.IsConstructedGenericType ? x.GetGenericTypeDefinition() : x))
        {
            MemberInfo? hit = m switch
            {
                ConstructorInfo c => b.GetConstructors(all).FirstOrDefault(x => Sig(x) == Sig(c)),
                PropertyInfo p => b.GetProperties(all).FirstOrDefault(x => x.Name == p.Name || x.Name.EndsWith("." + p.Name, StringComparison.Ordinal)),
                MethodInfo mi => b.GetMethods(all).FirstOrDefault(x => (x.Name == mi.Name || x.Name.EndsWith("." + mi.Name, StringComparison.Ordinal)) && Sig(x) == Sig(mi)),
                _ => null,
            };
            if (hit != null && Find(hit) is XElement e)
            {
                found = hit;
                return e;
            }
        }

        return null;
    }

    private XElement? Find(MemberInfo m)
    {
        Dictionary<string, XElement> docs = Load(m.Module.Assembly.Location);
        foreach (MemberInfo x in Candidates(m))
        {
            if (docs.TryGetValue(ExceptionDocs.DocId(x), out XElement? e))
            {
                return e;
            }
        }

        return null;
    }

    private Dictionary<string, XElement> Load(string assemblyPath)
    {
        if (byAssembly.TryGetValue(assemblyPath, out Dictionary<string, XElement>? d))
        {
            return d;
        }

        d = new Dictionary<string, XElement>(StringComparer.Ordinal);
        string path = Path.ChangeExtension(assemblyPath, ".xml");
        if (File.Exists(path))
        {
            foreach (XElement e in XDocument.Load(path).Descendants("member"))
            {
                if (e.Attribute("name") is XAttribute n)
                {
                    d[n.Value] = e;
                }
            }
        }

        byAssembly[assemblyPath] = d;
        return d;
    }
}
