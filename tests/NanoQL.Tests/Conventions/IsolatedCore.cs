// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.Loader;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A fresh copy of the core and of this test assembly in a collectible load context of their own, so a fact can see
/// how the core's statics initialize on their first read, which in the suite's own process another test may already
/// have made.
/// </summary>
public sealed class IsolatedCore : AssemblyLoadContext
{
    private static readonly string CorePath = typeof(ConventionProvider).Assembly.Location;
    private static readonly string TestsPath = typeof(IsolatedCore).Assembly.Location;

    /// <summary>Initializes a new instance of the <see cref="IsolatedCore"/> class.</summary>
    public IsolatedCore()
        : base("isolated core", isCollectible: true)
    {
    }

    /// <summary>
    /// Runs a public static method of this test assembly, by its type's full name and its name, in a fresh copy of the
    /// core, and returns its result, whose type must be the runtime's own.
    /// </summary>
    /// <typeparam name="T">The result's type.</typeparam>
    /// <param name="type">The full name of the method's type.</param>
    /// <param name="method">The method's name.</param>
    /// <returns>The result.</returns>
    public static T Run<T>(string type, string method)
    {
        var context = new IsolatedCore();
        try
        {
            Assembly tests = context.LoadFromAssemblyPath(TestsPath);
            MethodInfo m = tests.GetType(type, true)!.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!;
            return (T)m.Invoke(null, null)!;
        }
        finally
        {
            context.Unload();
        }
    }

    /// <inheritdoc/>
    protected override Assembly? Load(AssemblyName assemblyName) => assemblyName.Name switch
    {
        "NanoQL" => LoadFromAssemblyPath(CorePath),
        "NanoQL.Tests" => LoadFromAssemblyPath(TestsPath),
        _ => null,
    };
}
