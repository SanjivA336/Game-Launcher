using System.IO;
using System.Runtime.InteropServices;

namespace Game_Launcher.Helpers {
    /// <summary> Reads where a Windows shortcut (.lnk file) points. Read-only: the shortcut is never changed. </summary>
    public static class ShortcutResolver {

        /// <param name="TargetPath"> The program the shortcut starts.</param>
        /// <param name="Arguments"> The command-line arguments stored in the shortcut (may be empty).</param>
        public record Target(string TargetPath, string Arguments);

        /// <returns> The shortcut's target, or null if the file isn't a readable shortcut.</returns>
        public static Target? Resolve(string lnkPath) {
            object? shell = null;
            object? link = null;
            try {
                // Windows Script Host is the simplest built-in way to read a .lnk without extra libraries
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType is null) {
                    return null;
                }

                shell = Activator.CreateInstance(shellType);
                dynamic dynShell = shell!;
                link = dynShell.CreateShortcut(lnkPath);
                dynamic dynLink = link!;

                string target = dynLink.TargetPath ?? string.Empty;
                string args = dynLink.Arguments ?? string.Empty;
                return string.IsNullOrWhiteSpace(target) ? null : new Target(target, args);
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or IOException or UnauthorizedAccessException) {
                return null;
            }
            finally {
                if (link is not null) Marshal.ReleaseComObject(link);
                if (shell is not null) Marshal.ReleaseComObject(shell);
            }
        }
    }
}
