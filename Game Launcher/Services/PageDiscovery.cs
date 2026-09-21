using System.Diagnostics;
using System.Reflection;

namespace Game_Launcher.Services {
    /// <summary> Finds pages that were added by implementing <see cref="INexusPage"/>. </summary>
    public static class PageDiscovery {

        /// <summary> Every usable <see cref="INexusPage"/> in the assembly, as sidebar entries, in their requested order. </summary>
        /// <param name="assembly"> Where to look (the app itself, in normal use).</param>
        /// <param name="takenIds"> Ids that are already used (the built-in pages); a page reusing one is skipped.</param>
        public static IReadOnlyList<NavPage> Find(Assembly assembly, IEnumerable<string> takenIds) {
            var taken = new HashSet<string>(takenIds, StringComparer.OrdinalIgnoreCase);
            var found = new List<(int Order, NavPage Page)>();

            foreach (var type in SafeTypes(assembly).Where(t => t is { IsClass: true, IsAbstract: false } && typeof(INexusPage).IsAssignableFrom(t))) {
                try {
                    var instance = (INexusPage)Activator.CreateInstance(type)!;

                    if (string.IsNullOrWhiteSpace(instance.Id) || !taken.Add(instance.Id)) {
                        Debug.WriteLine($"Page {type.Name} skipped: its Id \"{instance.Id}\" is empty or already used.");
                        continue;
                    }

                    found.Add((instance.Order, new NavPage(instance.Id, instance.Title, instance.Glyph, instance.CreatePage)));
                }
                catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException or InvalidCastException) {
                    // One broken page must never stop Nexus from starting
                    Debug.WriteLine($"Page {type.Name} skipped: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            return found.OrderBy(f => f.Order).ThenBy(f => f.Page.Title, StringComparer.OrdinalIgnoreCase).Select(f => f.Page).ToList();
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly) {
            try {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex) {
                return ex.Types.OfType<Type>(); // some types couldn't load; use the ones that did
            }
        }
    }
}
