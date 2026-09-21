using System.Windows.Controls;

namespace Game_Launcher.Services {
    /// <summary>
    /// Describes one page of the app: what to call it, which icon it gets, and how to build it.
    /// The sidebar is generated from a list of these, so adding a page means adding one entry, not editing the sidebar.
    /// </summary>
    /// <param name="Id"> A short unique name, e.g. "library".</param>
    /// <param name="Title"> The label in the sidebar.</param>
    /// <param name="Glyph"> A Segoe Fluent Icons character shown next to the label.</param>
    /// <param name="Create"> Builds the page. Called once, the first time the page is opened; after that the same page is reused.</param>
    /// <param name="ShowInSidebar"> False for pages reached some other way (Home is reached through the Nexus logo).</param>
    /// <param name="IsEnabled"> Optional switch read from settings; when it returns false the page vanishes from the sidebar.</param>
    public record NavPage(
        string Id,
        string Title,
        string Glyph,
        Func<Page> Create,
        bool ShowInSidebar = true,
        Func<Models.Preferences, bool>? IsEnabled = null);
}
