using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Game_Launcher.Helpers {
    /// <summary>
    /// Makes the mouse wheel scroll the page no matter what is under the pointer.
    ///
    /// The problem: a list, a text box or a nested scroll area has a scroll viewer of its own, and that viewer swallows the wheel
    /// even when it has nothing to scroll (a list that shows all its items, a single-line text box, or a list already at its end).
    /// So with the pointer over the covers in the cover picker, the page didn't move; only over the scroll bar did it work.
    ///
    /// The fix, applied once for every scroll viewer in the app: when a viewer can't scroll any further in the wheel's direction,
    /// it passes the wheel on to the nearest area around it that CAN scroll.
    /// </summary>
    public static class ScrollForwarding {
        private static bool _registered;

        /// <summary> Turns this on for the whole app. Safe to call more than once. </summary>
        public static void Register() {
            if (_registered) {
                return;
            }
            _registered = true;
            EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewMouseWheel));
            EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnComboBoxWheel));
        }

        // A dropdown that has the keyboard focus changes its selection when the wheel turns over it (so scrolling past the Sort box could
        // silently re-sort the library). Only an open dropdown should use the wheel, to scroll its own list; otherwise the page scrolls.
        private static void OnComboBoxWheel(object sender, MouseWheelEventArgs e) {
            if (e.Handled || sender is not ComboBox combo || combo.IsDropDownOpen) {
                return;
            }

            // The dropdown's own wheel code runs even on a handled event, but only while it holds the keyboard focus, so let go of it
            if (combo.IsKeyboardFocusWithin) {
                Keyboard.ClearFocus();
            }

            e.Handled = true;
            Forward(NextViewerThatCanScroll(combo, e.Delta), e);
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (e.Handled || sender is not ScrollViewer viewer) {
                return;
            }

            // The wheel tunnels down from the OUTERMOST viewer, so every viewer around the pointer gets asked. Only the innermost one
            // under the pointer decides; the outer ones must stay out of the way, or a page that is already at its top would take
            // a wheel-up meant for a list inside it that can still scroll up.
            if (InnermostViewerUnder(e.OriginalSource as DependencyObject) != viewer || CanScroll(viewer, e.Delta)) {
                return;
            }

            // It can't scroll this way: hand the wheel to the nearest viewer around it that can
            var target = NextViewerThatCanScroll(viewer, e.Delta);
            if (target is null) {
                return;
            }

            e.Handled = true;
            Forward(target, e);
        }

        /// <summary> Offers the wheel turn to a viewer as if the pointer were over it (nothing happens when there isn't one). </summary>
        private static void Forward(ScrollViewer? target, MouseWheelEventArgs e) {
            target?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = target,
            });
        }

        /// <summary> Whether the viewer would actually move for this wheel turn (positive delta = wheel up = towards the top). </summary>
        private static bool CanScroll(ScrollViewer viewer, int delta) {
            if (viewer.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled) {
                return false;
            }
            return delta > 0 ? viewer.VerticalOffset > 0 : viewer.VerticalOffset < viewer.ScrollableHeight;
        }

        private static ScrollViewer? InnermostViewerUnder(DependencyObject? source) {
            for (var current = source; current is not null; current = ParentOf(current)) {
                if (current is ScrollViewer viewer) {
                    return viewer;
                }
            }
            return null;
        }

        private static ScrollViewer? NextViewerThatCanScroll(DependencyObject from, int delta) {
            for (var current = ParentOf(from); current is not null; current = ParentOf(current)) {
                if (current is ScrollViewer viewer && CanScroll(viewer, delta)) {
                    return viewer;
                }
            }
            return null;
        }

        // (text inside a control isn't a visual, so it is followed up through the logical tree instead)
        private static DependencyObject? ParentOf(DependencyObject child) {
            return child is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child);
        }
    }
}
