using System.Reflection;

namespace NivalisMods.ModCompanion.Api;

/// <summary>An encoded PNG/JPG owned by Companion. No loose file is required.</summary>
public sealed class ModIcon
{
    internal byte[] Data { get; }
    private ModIcon(byte[] data) => Data = data;

    public static ModIcon FromBytes(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("Icon data cannot be empty.", nameof(data));
        return new ModIcon((byte[])data.Clone());
    }

    public static ModIcon FromResource(Assembly assembly, string resourceName)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new ArgumentException($"Embedded icon resource '{resourceName}' was not found.", nameof(resourceName));
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return FromBytes(buffer.ToArray());
    }
}
