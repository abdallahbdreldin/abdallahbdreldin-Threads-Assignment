namespace OrderSystem.Exceptions
{
    public sealed class DataFileCorruptedException : Exception
    {
        public DataFileCorruptedException(string fileName, Exception innerException)
            : base($"Data file '{fileName}' contains invalid JSON.", innerException)
        {
            FileName = fileName;
        }

        public string FileName { get; }
    }
}
