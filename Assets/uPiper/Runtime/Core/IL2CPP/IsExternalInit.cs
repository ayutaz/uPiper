// Polyfill for C# 9.0 'init' accessor and 'record struct' support.
// Unity 6 ships a C# compiler that supports these features, but the runtime
// lacks the IsExternalInit type. This shim satisfies the compiler requirement.
// See: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-9.0/init

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
