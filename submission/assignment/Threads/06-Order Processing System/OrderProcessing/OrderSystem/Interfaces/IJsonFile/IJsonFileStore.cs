namespace OrderSystem.Interfaces.IJsonFile
{
    public interface IJsonFileStore
    {
        Task<T> ReadAsync<T>(string fileName, CancellationToken ct = default) where T : new();
        Task WriteAsync<T>(string fileName, T value, CancellationToken ct = default);
        Task<T> UpdateAsync<T>(string fileName, Func<T, T> update, CancellationToken ct = default) where T : new();
        T Read<T>(string fileName) where T : new();
        void Write<T>(string fileName, T value);
    }
}
