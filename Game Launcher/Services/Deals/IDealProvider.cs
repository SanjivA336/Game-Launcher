using Game_Launcher.Models;

namespace Game_Launcher.Services.Deals {
    /// <summary>
    /// One source of deals. Each website sits behind its own adapter, so one changing its format (or being down)
    /// only takes out its own deals, and adding a source means adding one class.
    /// </summary>
    public interface IDealProvider {
        /// <summary> What the source is called in messages ("Steam", "Epic Games Store"...). </summary>
        string Name { get; }

        /// <summary> Gets the source's current deals. Throws if the source can't be reached or its answer can't be understood. </summary>
        Task<IReadOnlyList<Deal>> FetchAsync(CancellationToken ct);
    }
}
