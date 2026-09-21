using System.Windows.Controls;

namespace Game_Launcher.Services {
    /// <summary>
    /// The one thing you implement to add your own page to Nexus (for example when you fork it).
    /// Put a class that implements this anywhere in the project: Nexus finds it at startup and adds a sidebar entry for it,
    /// so there is no list to edit. See "Adding your own page" in the README.
    /// </summary>
    /// <remarks> The class needs a public parameterless constructor. Keep it cheap: the page itself is only built when it is first opened. </remarks>
    public interface INexusPage {
        /// <summary> A short unique name, e.g. "notes". Must differ from the built-in ones (home, library, deals). </summary>
        string Id { get; }

        /// <summary> The label in the sidebar. </summary>
        string Title { get; }

        /// <summary> A Segoe Fluent Icons character for the sidebar, e.g. "\uE70B". </summary>
        string Glyph { get; }

        /// <summary> Where it goes among added pages: lower numbers come first. Built-in pages always come before added ones. </summary>
        int Order { get; }

        /// <summary> Builds the page. Called once, the first time it is opened. </summary>
        Page CreatePage();
    }
}
