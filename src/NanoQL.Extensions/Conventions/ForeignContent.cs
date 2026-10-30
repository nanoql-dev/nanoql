// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Xml.Linq;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// Another tool's content kept on a record or a document's root: the attributes and elements in a namespace other
/// than the conventions', each as the XML text it was read as, compared by content.
/// </summary>
/// <remarks>
/// An element is kept with its namespace declarations, so it writes back alone. On a record the elements follow its
/// fields; on a root each element keeps its place among the records (<see cref="ConventionSnapshot"/>).
/// </remarks>
public sealed record ForeignContent
{
    /// <summary>Initializes a new instance of the <see cref="ForeignContent"/> class.</summary>
    /// <param name="attributes">The foreign attributes, in order.</param>
    /// <param name="elements">The foreign elements' XML text, in order.</param>
    /// <exception cref="ArgumentNullException">A sequence, an attribute or an element is null.</exception>
    /// <exception cref="ArgumentException">
    /// An attribute is in no namespace or the conventions' namespace, or an element isn't one well-formed element in
    /// another namespace.
    /// </exception>
    public ForeignContent(IEnumerable<ForeignAttribute> attributes, IEnumerable<string> elements)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(elements);
        Attributes = [.. attributes];
        Elements = [.. elements];
        foreach (ForeignAttribute a in Attributes)
        {
            ArgumentNullException.ThrowIfNull(a, nameof(attributes));
            if (a.Namespace.Length == 0 || a.Namespace == ConventionsXml.Namespace)
            {
                throw new ArgumentException($"{a.LocalName} is not in a foreign namespace", nameof(attributes));
            }
        }

        foreach (string e in Elements)
        {
            ArgumentNullException.ThrowIfNull(e, nameof(elements));
            XNamespace ns;
            try
            {
                ns = XElement.Parse(e).Name.Namespace;
            }
            catch (System.Xml.XmlException x)
            {
                throw new ArgumentException($"not one well-formed element: {x.Message}", nameof(elements), x);
            }

            if (ns == XNamespace.None || ns.NamespaceName == ConventionsXml.Namespace)
            {
                throw new ArgumentException("an element is not in a foreign namespace", nameof(elements));
            }
        }
    }

    /// <summary>Gets no foreign content.</summary>
    public static ForeignContent None { get; } = new([], []);

    /// <summary>Gets the foreign attributes, in order.</summary>
    public ImmutableArray<ForeignAttribute> Attributes { get; }

    /// <summary>Gets the foreign elements' XML text, in order.</summary>
    public ImmutableArray<string> Elements { get; }

    /// <summary>Gets a value indicating whether there is none.</summary>
    public bool IsEmpty => Attributes.IsEmpty && Elements.IsEmpty;

    /// <summary>Whether the two hold the same attributes and elements, in order.</summary>
    /// <param name="other">The other content.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public bool Equals(ForeignContent? other) =>
        other is not null && Attributes.SequenceEqual(other.Attributes) && Elements.SequenceEqual(other.Elements, StringComparer.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (ForeignAttribute a in Attributes)
        {
            hash.Add(a);
        }

        foreach (string e in Elements)
        {
            hash.Add(e, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
