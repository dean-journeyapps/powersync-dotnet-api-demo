namespace PowerSync.Domain.Interfaces
{
    /// <summary>
    /// A factory for creating database persisters.
    /// </summary>
    public interface IPersisterFactory
    {
        /// <summary>
        /// Creates a persister for a given connection URI.
        /// </summary>
        /// <param name="uri">The connection URI</param>
        /// <returns>A task containing the created persister</returns>
        Task<IPersister> CreatePersisterAsync(string uri);
    }
}