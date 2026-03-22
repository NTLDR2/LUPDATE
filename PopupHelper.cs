// =============================================================================
// PopupHelper.cs — Force Left-Aligned Menu Dropdowns
// =============================================================================
// Ported from RDAS101. This attached behavior forces menu popups to open
// aligned to the left edge of the menu item, regardless of Windows system
// "handedness" / MenuDropAlignment tablet settings.
// =============================================================================

using System.Windows;
using System.Windows.Controls.Primitives;

namespace LUpdate
{
    /// <summary>
    /// Attached behavior that forces a Popup to open to the left edge of its
    /// placement target (left-to-right direction), regardless of the Windows
    /// system "handedness" / MenuDropAlignment setting.
    /// </summary>
    public static class PopupHelper
    {
        public static readonly DependencyProperty ForceLeftAlignProperty =
            DependencyProperty.RegisterAttached(
                "ForceLeftAlign",
                typeof(bool),
                typeof(PopupHelper),
                new PropertyMetadata(false, OnForceLeftAlignChanged));

        public static void SetForceLeftAlign(DependencyObject d, bool value)
            => d.SetValue(ForceLeftAlignProperty, value);

        public static bool GetForceLeftAlign(DependencyObject d)
            => (bool)d.GetValue(ForceLeftAlignProperty);

        private static void OnForceLeftAlignChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Popup popup && (bool)e.NewValue)
                popup.CustomPopupPlacementCallback = PlaceBottomLeft;
        }

        /// <summary>
        /// Places the popup directly below the placement target, aligned to the LEFT edge.
        /// </summary>
        private static CustomPopupPlacement[] PlaceBottomLeft(
            Size popupSize, Size targetSize, Point offset)
        {
            // Negative value shifts the dropdown to the LEFT relative to the
            // left edge of the clicked menu item (File / View / Tools / Help).
            const double horizontalShift = -3;

            return new[]
            {
                // Primary: bottom-left (shifted)
                new CustomPopupPlacement(
                    new Point(horizontalShift, targetSize.Height),
                    PopupPrimaryAxis.Horizontal),
                // Fallback: top-left if off-screen below (shifted)
                new CustomPopupPlacement(
                    new Point(horizontalShift, -popupSize.Height),
                    PopupPrimaryAxis.Horizontal)
            };
        }
    }
}
