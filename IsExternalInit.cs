// Polyfill for C# 9.0 init-only properties in .NET Framework 4.8
// Required because .NET Framework does not include System.Runtime.CompilerServices.IsExternalInit by default

using System;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Reserved to be used by the compiler for tracking metadata.
    /// This class should not be used by developers in source code.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
