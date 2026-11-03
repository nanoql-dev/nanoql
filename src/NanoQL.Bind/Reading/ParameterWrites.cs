// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Reflection.Emit;

namespace NanoQL.Bind.Reading;

/// <summary>
/// Which <c>Matrix</c> and <c>Vector</c> parameters a member writes into, read from IL, never by running it: a parameter
/// is written when the member, an override or an implementation of it calls a mutating member on it (a setter, a
/// compound assignment, a <c>void</c> method), stores through a <c>ref</c> or a <c>Span</c> one of its members returns,
/// or passes it, or such a span, to a member that writes it. A member with no body anywhere in the inputs (abstract, an
/// interface's) writes the parameters its documentation says it writes (a least-squares problem's TargetAndValue: "The
/// target, written"). The walk follows the stack by each opcode's own stack behaviour, and starts each block after an
/// unconditional branch empty, as C#'s statements leave it.
/// </summary>
internal sealed class ParameterWrites : IDisposable
{
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);

    private readonly MetadataSet set;
    private readonly ExceptionDocs docs;
    private readonly Type matrix;
    private readonly Type vector;
    private readonly Dictionary<Assembly, TokenResolver> resolvers = [];
    private readonly Dictionary<(MethodBase, int), bool> memo = [];
    private readonly HashSet<(MethodBase, int)> walking = [];
    private Dictionary<string, List<MethodBase>>? implementations;

    /// <summary>Initializes a new instance of the <see cref="ParameterWrites"/> class.</summary>
    /// <param name="set">The inputs.</param>
    /// <param name="matrix"><c>NanoQL.Numerics.Matrix</c>.</param>
    /// <param name="vector"><c>NanoQL.Numerics.Vector</c>.</param>
    /// <param name="docs">The inputs' documentation.</param>
    public ParameterWrites(MetadataSet set, Type matrix, Type vector, ExceptionDocs docs)
    {
        this.set = set;
        this.docs = docs;
        this.matrix = matrix;
        this.vector = vector;
    }

    /// <summary>Whether a member writes into one of its parameters.</summary>
    /// <param name="m">The member.</param>
    /// <param name="parameter">The parameter's index, from 0, the instance not counted.</param>
    /// <returns>Whether it, an override or an implementation writes the parameter.</returns>
    public bool Writes(MethodBase m, int parameter)
    {
        if (memo.TryGetValue((m, parameter), out bool known))
        {
            return known;
        }

        if (!walking.Add((m, parameter)))
        {
            return false;
        }

        List<MethodBase> impls = m is MethodInfo mi && (mi.IsVirtual || mi.IsAbstract) ? Implementations(mi) : [];
        bool bodyless = m.IsAbstract || m.DeclaringType!.IsInterface;
        bool written = (!bodyless && WritesInBody(m, parameter))
            || impls.Any(i => i != m && WritesInBody(i, parameter))
            || (bodyless && docs.SaysWritten(m, m.GetParameters()[parameter].Name!) && impls.All(i => i.IsAbstract || i.DeclaringType!.IsInterface));
        walking.Remove((m, parameter));
        memo[(m, parameter)] = written;
        return written;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (TokenResolver r in resolvers.Values)
        {
            r.Dispose();
        }
    }

    // A span's members resolve on its open definition, System.Span`1.
    private bool IsArrayType(Type t) => t == matrix || t == vector || t.IsConstructionOf("System.Span`1") || t.FullName == "System.Span`1";

    // A Matrix's, a Vector's or a span's member that changes the object it is called on (a span's CopyTo writes its
    // argument, which the walk checks apart).
    private bool Mutates(MethodBase m)
    {
        if (m.IsStatic || m is not MethodInfo mi || !IsArrayType(m.DeclaringType!))
        {
            return false;
        }

        return mi.Name.StartsWith("set_", StringComparison.Ordinal)
            || (mi.Name.StartsWith("op_", StringComparison.Ordinal) && mi.Name.EndsWith("Assignment", StringComparison.Ordinal))
            || (mi.ReturnType.FullName == "System.Void" && mi.Name is not ("CopyTo" or "Deconstruct"));
    }

    // A member whose result is a view of its receiver: a ref or a span, through which a store writes the receiver.
    private static bool Views(MethodBase m) =>
        m is MethodInfo mi && !mi.IsStatic && (mi.ReturnType.IsByRef || mi.ReturnType.IsConstructionOf("System.Span`1"));

    // The walk over one body: each stack entry carries the parameters it may be, as a mask.
    private bool WritesInBody(MethodBase m, int parameter)
    {
        if (!set.Ours(m.DeclaringType!) || m.IsAbstract || m.DeclaringType!.IsInterface)
        {
            return false;
        }

        byte[] il;
        try
        {
            il = Resolver(m.DeclaringType!.Assembly).Body(m);
        }
        catch (Exception e) when (e is BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            return false;
        }

        int first = m.IsStatic ? 0 : 1;
        long target = 1L << (parameter + first);
        var stack = new List<long>();
        var locals = new Dictionary<int, long>();
        TokenResolver tokens = Resolver(m.DeclaringType!.Assembly);
        foreach (IlInstruction ins in IlInstruction.Decode(il))
        {
            OpCode op = Codes[ins.Op];
            int n = LocalOrArg(op, ins);
            // An argument or a local, by value or by address: a span's members are called through its address.
            if (op.Name!.StartsWith("ldarg", StringComparison.Ordinal))
            {
                stack.Add(n < 64 ? 1L << n : 0);
                continue;
            }

            if (op.Name.StartsWith("ldloc", StringComparison.Ordinal))
            {
                stack.Add(locals.GetValueOrDefault(n));
                continue;
            }

            if (op.Name.StartsWith("stloc", StringComparison.Ordinal))
            {
                locals[n] = Pop(stack, 1)[0];
                continue;
            }

            if (op == OpCodes.Dup)
            {
                stack.Add(stack.Count > 0 ? stack[^1] : 0);
                continue;
            }

            if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj)
            {
                (MethodBase? callee, int count, bool instance, bool isVoid) = Method(tokens, ins.Operand);
                bool receiver = instance && op != OpCodes.Newobj;
                long[] args = Pop(stack, count + (receiver ? 1 : 0));
                long view = 0;
                if (callee != null)
                {
                    if (receiver && (args[0] & target) != 0 && Mutates(callee))
                    {
                        return true;
                    }

                    // A copy into a span the walk follows: a span's CopyTo, or MemoryExtensions' from an array.
                    if (callee.Name is "CopyTo" or "TryCopyTo" && args.Length > 1 && (args[^1] & target) != 0
                        && callee.GetParameters() is { Length: > 0 } cps && cps[^1].ParameterType.IsConstructionOf("System.Span`1"))
                    {
                        return true;
                    }

                    if (receiver && Views(callee))
                    {
                        view = args[0];
                    }

                    ParameterInfo[] ps = callee.GetParameters();
                    for (int j = receiver ? 1 : 0; j < args.Length; j++)
                    {
                        int k = j - (receiver ? 1 : 0);
                        if ((args[j] & target) != 0 && k < ps.Length && IsArrayType(ps[k].ParameterType) && Writes(callee, k))
                        {
                            return true;
                        }
                    }
                }

                if (op == OpCodes.Newobj || !isVoid)
                {
                    stack.Add(view);
                }

                continue;
            }

            // A store through a ref a view gave: the address under the value.
            if (op.Name.StartsWith("stind", StringComparison.Ordinal) || op == OpCodes.Stobj)
            {
                if ((Pop(stack, 2)[0] & target) != 0)
                {
                    return true;
                }

                continue;
            }

            int pops = Pops(op);
            if (pops < 0)
            {
                stack.Clear();
            }
            else
            {
                Pop(stack, pops);
            }

            for (int k = 0; k < Pushes(op); k++)
            {
                stack.Add(0);
            }

            if (op.FlowControl is FlowControl.Branch or FlowControl.Return or FlowControl.Throw)
            {
                stack.Clear();
            }
        }

        return false;
    }

    private static long[] Pop(List<long> stack, int n)
    {
        var popped = new long[n];
        for (int k = n - 1; k >= 0; k--)
        {
            if (stack.Count > 0)
            {
                popped[k] = stack[^1];
                stack.RemoveAt(stack.Count - 1);
            }
        }

        return popped;
    }

    // An ldarg's, ldloc's or stloc's index, short forms included.
    private static int LocalOrArg(OpCode op, IlInstruction ins)
    {
        string name = op.Name!;
        int dot = name.LastIndexOf('.');
        return dot > 0 && int.TryParse(name[(dot + 1)..], out int k) ? k : ins.Operand;
    }

    private static int Pops(OpCode op) => op.StackBehaviourPop switch
    {
        StackBehaviour.Pop0 => 0,
        StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
        StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi or StackBehaviour.Popi_popi8 or StackBehaviour.Popi_popr4
            or StackBehaviour.Popi_popr8 or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
        StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popi8 or StackBehaviour.Popref_popi_popr4
            or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref or StackBehaviour.Popref_popi_pop1 => 3,
        _ => -1,
    };

    private static int Pushes(OpCode op) => op.StackBehaviourPush switch
    {
        StackBehaviour.Push0 => 0,
        StackBehaviour.Push1_push1 => 2,
        StackBehaviour.Varpush => 0,
        _ => 1,
    };

    // A call's target, through a generic method's instantiation.
    private static (MethodBase? Member, int Parameters, bool Instance, bool Void) Method(TokenResolver tokens, int token) => tokens.Method(token);

    private TokenResolver Resolver(Assembly a)
    {
        if (!resolvers.TryGetValue(a, out TokenResolver? r))
        {
            r = resolvers[a] = new TokenResolver(set, a);
        }

        return r;
    }

    // The overrides and interface implementations of a member across the inputs.
    private List<MethodBase> Implementations(MethodInfo m)
    {
        // Those of the member's own class and the classes below it (an interface's: its implementers), not its siblings',
        // in the assemblies of the member's own prefix: a user's override doesn't change nanoql's functions.
        return [.. AllImplementations(m).Where(i => set.PrefixOf(i.DeclaringType!) == set.PrefixOf(m.DeclaringType!))];
    }

    /// <summary>Whether an override in another prefix's assembly writes a parameter the member itself doesn't.</summary>
    /// <param name="m">The member.</param>
    /// <param name="parameter">The parameter's index, from 0, the instance not counted.</param>
    /// <returns>The first such override's prefix, or null.</returns>
    public string? WrittenElsewhere(MethodBase m, int parameter)
    {
        if (m is not MethodInfo mi || !(mi.IsVirtual || mi.IsAbstract) || Writes(m, parameter))
        {
            return null;
        }

        MethodBase? other = AllImplementations(mi).FirstOrDefault(i => set.PrefixOf(i.DeclaringType!) != set.PrefixOf(m.DeclaringType!) && WritesInBody(i, parameter));
        return other == null ? null : set.PrefixOf(other.DeclaringType!);
    }

    private List<MethodBase> AllImplementations(MethodInfo m)
    {
        implementations ??= Index();
        return [.. implementations.GetValueOrDefault(Key(BaseDefinition(m)), []).Where(i => m.DeclaringType!.IsAssignableFrom(i.DeclaringType!))];
    }

    private Dictionary<string, List<MethodBase>> Index()
    {
        var index = new Dictionary<string, List<MethodBase>>(StringComparer.Ordinal);
        void Add(MethodInfo baseDefinition, MethodBase m)
        {
            string key = Key(baseDefinition);
            if (!index.TryGetValue(key, out List<MethodBase>? list))
            {
                list = index[key] = [];
            }

            list.Add(m);
        }

        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach ((_, Assembly a) in set.Assemblies)
        {
            foreach (Type t in a.GetTypes())
            {
                foreach (MethodInfo m in t.GetMethods(all).Where(m => m.IsVirtual))
                {
                    Add(BaseDefinition(m), m);
                }

                foreach (Type i in t.IsInterface ? [] : t.GetInterfaces())
                {
                    foreach (MethodInfo im in i.GetMethods())
                    {
                        string[] sig = [.. im.GetParameters().Select(p => p.ParameterType.KeyName())];
                        MethodInfo? impl = t.GetMethods(all).FirstOrDefault(c => (c.Name == im.Name || c.Name.EndsWith("." + im.Name, StringComparison.Ordinal))
                            && c.GetParameters().Select(p => p.ParameterType.KeyName()).SequenceEqual(sig));
                        if (impl != null)
                        {
                            Add(im, impl);
                        }
                    }
                }
            }
        }

        return index;
    }

    // The declaration that introduced a virtual member's slot (MetadataLoadContext has no GetBaseDefinition): up the
    // base types while the member overrides, to the one declared new.
    private static MethodInfo BaseDefinition(MethodInfo m)
    {
        MethodInfo current = m;
        while (current.IsVirtual && (current.Attributes & MethodAttributes.NewSlot) == 0 && current.DeclaringType!.BaseType is { } baseType)
        {
            string[] sig = [.. current.GetParameters().Select(p => p.ParameterType.KeyName())];
            MethodInfo? overridden = null;
            for (Type? t = baseType; t != null && overridden == null; t = t.BaseType)
            {
                overridden = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(c => c.IsVirtual && c.Name == current.Name && c.GetParameters().Select(p => p.ParameterType.KeyName()).SequenceEqual(sig));
            }

            if (overridden == null)
            {
                break;
            }

            current = overridden;
        }

        return current;
    }

    private static string Key(MethodInfo m) =>
        (m.DeclaringType!.IsConstructedGenericType ? m.DeclaringType.GetGenericTypeDefinition() : m.DeclaringType).FullName + "::" + m.Name + "("
        + string.Join(",", m.GetParameters().Select(p => p.ParameterType.KeyName())) + ")";
}
