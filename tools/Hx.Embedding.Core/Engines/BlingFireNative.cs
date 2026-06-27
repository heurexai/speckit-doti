using System.Runtime.InteropServices;

namespace Hx.Embedding.Engines;

/// <summary>
/// Minimal, analyzer-clean P/Invoke into BlingFire's native tokenizer (<c>blingfiretokdll</c>). We hand-roll
/// these three imports — each with <see cref="DefaultDllImportSearchPathsAttribute"/> to satisfy CA5392 — rather
/// than compile BlingFireNuget's <c>contentFiles</c> wrapper source, which omits the secure-search-path
/// attribute and would fail the security-analyzer gate. The native dll ships as a <c>Content</c> item copied to
/// output (flowing transitively to consumers). Signatures verified against the package's
/// <c>BlingFireUtils.cs</c>.
/// </summary>
internal static class BlingFireNative
{
    private const string Dll = "blingfiretokdll";
    // SafeDirectories = LOAD_LIBRARY_SEARCH_DEFAULT_DIRS (application directory + System32 + user dirs); the
    // native dll is copied into the application/test output dir, so it resolves here. AssemblyDirectory is
    // rejected by CA5393 as an unsafe (potentially writable) search path.
    private const DllImportSearchPath SearchPaths = DllImportSearchPath.SafeDirectories;

    [DllImport(Dll)]
    [DefaultDllImportSearchPaths(SearchPaths)]
    internal static extern ulong LoadModel([MarshalAs(UnmanagedType.LPArray)] byte[] modelName);

    [DllImport(Dll)]
    [DefaultDllImportSearchPaths(SearchPaths)]
    internal static extern int FreeModel(ulong model);

    [DllImport(Dll)]
    [DefaultDllImportSearchPaths(SearchPaths)]
    internal static extern int TextToIds(
        ulong model,
        [MarshalAs(UnmanagedType.LPArray)] byte[] inUtf8Str,
        int inUtf8StrLen,
        int[] tokenIds,
        int maxBuffSize,
        int unkId);
}
