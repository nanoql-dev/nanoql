// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace NanoQL.Bind.Reading;

/// <summary>
/// Resolves an assembly's IL tokens, which a <see cref="MetadataLoadContext"/> doesn't: strings, fields, and methods as
/// the context's members, matched by declaring type and parameter types, read from the file's own metadata.
/// </summary>
internal sealed class TokenResolver : IDisposable
{
    private readonly PEReader pe;
    private readonly MetadataReader md;
    private readonly MetadataSet set;
    private readonly Assembly assembly;

    /// <summary>Initializes a new instance of the <see cref="TokenResolver"/> class over one of the inputs.</summary>
    /// <param name="set">The inputs, whose types the methods resolve to.</param>
    /// <param name="assembly">The assembly whose IL the tokens come from.</param>
    public TokenResolver(MetadataSet set, Assembly assembly)
    {
        this.set = set;
        this.assembly = assembly;
        pe = new PEReader(File.OpenRead(assembly.Location));
        md = pe.GetMetadataReader();
    }

    /// <summary>A user string.</summary>
    /// <param name="token">The <c>ldstr</c> operand.</param>
    /// <returns>The string.</returns>
    public string String(int token) => md.GetUserString(MetadataTokens.UserStringHandle(token & 0xFFFFFF));

    /// <summary>A field's name.</summary>
    /// <param name="token">A field's token.</param>
    /// <returns>Its name.</returns>
    public string FieldName(int token)
    {
        EntityHandle h = MetadataTokens.EntityHandle(token);
        return h.Kind == HandleKind.FieldDefinition
            ? md.GetString(md.GetFieldDefinition((FieldDefinitionHandle)h).Name)
            : md.GetString(md.GetMemberReference((MemberReferenceHandle)h).Name);
    }

    /// <summary>A method's shape from its token: the context's member when it resolves, its parameter count, whether it is an instance method, and whether it returns nothing.</summary>
    /// <param name="token">A method's token.</param>
    /// <returns>The shape.</returns>
    public (MethodBase? Member, int Parameters, bool Instance, bool Void) Method(int token)
    {
        EntityHandle h = MetadataTokens.EntityHandle(token);
        string declaring;
        string name;
        MethodSignature<string> sig;
        switch (h.Kind)
        {
            case HandleKind.MethodDefinition:
            {
                MethodDefinition d = md.GetMethodDefinition((MethodDefinitionHandle)h);
                declaring = TypeName(d.GetDeclaringType());
                name = md.GetString(d.Name);
                sig = d.DecodeSignature(new Names(this), null);
                break;
            }

            case HandleKind.MemberReference:
            {
                MemberReference r = md.GetMemberReference((MemberReferenceHandle)h);
                declaring = Parent(r.Parent);
                name = md.GetString(r.Name);
                sig = r.DecodeMethodSignature(new Names(this), null);
                break;
            }

            case HandleKind.MethodSpecification:
                // A generic method's instantiation: its method's definition or reference.
                return Method(MetadataTokens.GetToken(md.GetMethodSpecification((MethodSpecificationHandle)h).Method));

            default:
                return (null, 0, false, false);
        }

        Type? type = assembly.GetType(declaring) ?? set.Assemblies.Select(a => a.Assembly.GetType(declaring)).FirstOrDefault(t => t != null) ?? SystemType(declaring);
        MethodBase? member = null;
        if (type != null)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            IEnumerable<MethodBase> candidates = name == ".ctor" ? type.GetConstructors(all) : type.GetMethods(all).Where(m => m.Name == name);
            member = candidates.FirstOrDefault(m => m.GetParameters().Select(p => p.ParameterType.KeyName()).SequenceEqual(sig.ParameterTypes));
        }

        return (member, sig.ParameterTypes.Length, sig.Header.IsInstance, sig.ReturnType == "System.Void");
    }

    /// <summary>A method's IL, read from the file by its token.</summary>
    /// <param name="m">One of this assembly's methods.</param>
    /// <returns>Its body's bytes.</returns>
    public byte[] Body(MethodBase m)
    {
        MethodDefinition d = md.GetMethodDefinition((MethodDefinitionHandle)MetadataTokens.EntityHandle(m.MetadataToken));
        return pe.GetMethodBody(d.RelativeVirtualAddress).GetILBytes()!;
    }

    /// <inheritdoc/>
    public void Dispose() => pe.Dispose();

    private Type? SystemType(string name)
    {
        try
        {
            return set.SystemType(name);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private string Parent(EntityHandle parent) => parent.Kind switch
    {
        HandleKind.TypeReference => TypeName((TypeReferenceHandle)parent),
        HandleKind.TypeDefinition => TypeName((TypeDefinitionHandle)parent),
        // A generic type's instance (Span<double>): its definition, whose members the call names (System.Span`1).
        HandleKind.TypeSpecification => md.GetTypeSpecification((TypeSpecificationHandle)parent).DecodeSignature(new Names(this), null) is var s
            && s.IndexOf('<', StringComparison.Ordinal) is var lt and > 0 ? s[..lt] : "(generic)",
        _ => "?",
    };

    private string TypeName(TypeDefinitionHandle h)
    {
        TypeDefinition t = md.GetTypeDefinition(h);
        TypeDefinitionHandle outer = t.GetDeclaringType();
        string name = md.GetString(t.Name);
        return outer.IsNil ? Join(md.GetString(t.Namespace), name) : TypeName(outer) + "+" + name;
    }

    private string TypeName(TypeReferenceHandle h)
    {
        TypeReference t = md.GetTypeReference(h);
        string name = md.GetString(t.Name);
        return t.ResolutionScope.Kind == HandleKind.TypeReference
            ? TypeName((TypeReferenceHandle)t.ResolutionScope) + "+" + name
            : Join(md.GetString(t.Namespace), name);
    }

    private static string Join(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;

    // Signature types named as TypeTests.KeyName names the context's types.
    private sealed class Names(TokenResolver r) : ISignatureTypeProvider<string, object?>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Boolean => "System.Boolean", PrimitiveTypeCode.Char => "System.Char", PrimitiveTypeCode.SByte => "System.SByte",
            PrimitiveTypeCode.Byte => "System.Byte", PrimitiveTypeCode.Int16 => "System.Int16", PrimitiveTypeCode.UInt16 => "System.UInt16",
            PrimitiveTypeCode.Int32 => "System.Int32", PrimitiveTypeCode.UInt32 => "System.UInt32", PrimitiveTypeCode.Int64 => "System.Int64",
            PrimitiveTypeCode.UInt64 => "System.UInt64", PrimitiveTypeCode.Single => "System.Single", PrimitiveTypeCode.Double => "System.Double",
            PrimitiveTypeCode.String => "System.String", PrimitiveTypeCode.Object => "System.Object", PrimitiveTypeCode.IntPtr => "System.IntPtr",
            PrimitiveTypeCode.UIntPtr => "System.UIntPtr", PrimitiveTypeCode.Void => "System.Void", PrimitiveTypeCode.TypedReference => "System.TypedReference",
            _ => typeCode.ToString(),
        };

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => r.TypeName(handle);

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => r.TypeName(handle);

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        public string GetSZArrayType(string elementType) => elementType + "[]";

        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";

        public string GetByReferenceType(string elementType) => elementType + "&";

        public string GetPointerType(string elementType) => elementType + "*";

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";

        public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;

        public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;

        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

        public string GetPinnedType(string elementType) => elementType;
    }
}
