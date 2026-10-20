using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderSystem.Exceptions;

namespace OrderSystem
{
    public sealed class JsonFileStore
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.OrdinalIgnoreCase);

        public static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

        public string DataDirectory { get; }

        public JsonFileStore(string? dataDirectory = null)
        {
            DataDirectory = dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "Data");
        }

        public async Task<T> ReadAsync<T>(string fileName, CancellationToken ct = default) where T : new()
        {
            string path = GetPath(fileName);
            SemaphoreSlim gate = GetLock(path);
            await gate.WaitAsync(ct);
            try
            {
                if (!File.Exists(path)) return new T();

                string json = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
                try
                {
                    return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? new T();
                }
                catch (JsonException ex)
                {
                    throw new DataFileCorruptedException(fileName, ex);
                }
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task WriteAsync<T>(string fileName, T value, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(value);
            string path = GetPath(fileName);
            SemaphoreSlim gate = GetLock(path);
            await gate.WaitAsync(ct);
            string? temporaryPath = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string json = JsonSerializer.Serialize(value, value.GetType(), SerializerOptions);
                temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
                ct.ThrowIfCancellationRequested();
                File.Move(temporaryPath, path, overwrite: true);
                temporaryPath = null;
            }
            finally
            {
                if (temporaryPath is not null && File.Exists(temporaryPath)) File.Delete(temporaryPath);
                gate.Release();
            }
        }

        public async Task<T> UpdateAsync<T>(string fileName, Func<T, T> update, CancellationToken ct = default) where T : new()
        {
            ArgumentNullException.ThrowIfNull(update);
            string path = GetPath(fileName);
            SemaphoreSlim gate = GetLock(path);
            await gate.WaitAsync(ct);
            string? temporaryPath = null;
            try
            {
                T current = await ReadUnlockedAsync<T>(path, fileName, ct);
                T updated = update(current);
                ArgumentNullException.ThrowIfNull(updated);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string json = JsonSerializer.Serialize(updated, updated.GetType(), SerializerOptions);
                temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false), ct);
                ct.ThrowIfCancellationRequested();
                File.Move(temporaryPath, path, overwrite: true);
                temporaryPath = null;
                return updated;
            }
            finally
            {
                if (temporaryPath is not null && File.Exists(temporaryPath)) File.Delete(temporaryPath);
                gate.Release();
            }
        }

        public T Read<T>(string fileName) where T : new()
        {
            string path = GetPath(fileName);
            SemaphoreSlim gate = GetLock(path);
            gate.Wait();
            try
            {
                if (!File.Exists(path)) return new T();
                string json = File.ReadAllText(path, Encoding.UTF8);
                try
                {
                    return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? new T();
                }
                catch (JsonException ex)
                {
                    throw new DataFileCorruptedException(fileName, ex);
                }
            }
            finally
            {
                gate.Release();
            }
        }

        public void Write<T>(string fileName, T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            string path = GetPath(fileName);
            SemaphoreSlim gate = GetLock(path);
            gate.Wait();
            string? temporaryPath = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string json = JsonSerializer.Serialize(value, value.GetType(), SerializerOptions);
                temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temporaryPath, path, overwrite: true);
                temporaryPath = null;
            }
            finally
            {
                if (temporaryPath is not null && File.Exists(temporaryPath)) File.Delete(temporaryPath);
                gate.Release();
            }
        }

        private string GetPath(string fileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            if (Path.IsPathRooted(fileName) || fileName != Path.GetFileName(fileName))
                throw new ArgumentException("A data file name must be a simple file name.", nameof(fileName));
            return Path.Combine(DataDirectory, fileName);
        }

        private static SemaphoreSlim GetLock(string path) => FileLocks.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1));

        private static async Task<T> ReadUnlockedAsync<T>(string path, string fileName, CancellationToken ct) where T : new()
        {
            if (!File.Exists(path)) return new T();
            string json = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
            try
            {
                return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? new T();
            }
            catch (JsonException ex)
            {
                throw new DataFileCorruptedException(fileName, ex);
            }
        }

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
