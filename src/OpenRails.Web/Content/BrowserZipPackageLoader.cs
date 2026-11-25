using Microsoft.AspNetCore.Components.Forms;

namespace OpenRails.Web.Content;

/// <summary>
/// Bridges a user-selected browser file to the ZIP-backed virtual content store.
/// The compressed archive is copied into a bounded in-memory stream (300 MiB default cap) because
/// browser file streams are not guaranteed to be seekable.
/// </summary>
public static class BrowserZipPackageLoader
{
    public const long DefaultMaxArchiveBytes = 300L * 1024 * 1024;

    public static async Task<ZipContentStore> LoadAsync(
        IBrowserFile file,
        long maxArchiveBytes = DefaultMaxArchiveBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (maxArchiveBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxArchiveBytes));

        if (file.Size <= 0)
            throw new InvalidDataException("The selected package is empty.");

        if (file.Size > maxArchiveBytes)
            throw new InvalidDataException($"The selected package exceeds the {maxArchiveBytes}-byte upload limit.");

        await using var source = file.OpenReadStream(maxArchiveBytes, cancellationToken);
        var buffer = new byte[81920];
        var memory = new MemoryStream(capacity: checked((int)Math.Min(file.Size, int.MaxValue)));

        try
        {
            long totalRead = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                    break;

                totalRead += read;
                if (totalRead > maxArchiveBytes)
                    throw new InvalidDataException($"The selected package exceeds the {maxArchiveBytes}-byte upload limit.");

                await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (totalRead == 0)
                throw new InvalidDataException("The selected package is empty.");

            memory.Position = 0;
            return new ZipContentStore(memory);
        }
        catch
        {
            await memory.DisposeAsync();
            throw;
        }
    }
}
