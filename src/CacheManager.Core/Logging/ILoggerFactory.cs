namespace CacheManager.Core.Logging
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public interface ILoggerFactory
    {
        ILogger CreateLogger(string categoryName);

        ILogger CreateLogger<T>(T instance);
    }

    public class NullLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new NullLogger();

        public ILogger CreateLogger<T>(T instance) => new NullLogger();
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}