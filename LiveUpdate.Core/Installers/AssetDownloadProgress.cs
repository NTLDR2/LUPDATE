// =============================================================================
// AssetDownloadProgress.cs — Progress Tracking for Asset Manifest Downloads
// =============================================================================
//
// PURPOSE:
//   Provides structured progress information when downloading individual
//   assets from an asset manifest. Used by AssetManifestInstaller to report
//   per-file and overall progress to the UI layer.
//
// USAGE:
//   AssetManifestInstaller reports progress via IProgress<AssetDownloadProgress>.
//   The UI layer (e.g., DownloadProgressWindow) can use this to show:
//     - Which file is currently being downloaded
//     - Per-file download percentage
//     - Overall progress across all assets
//
// =============================================================================

namespace LiveUpdate.Engine.Installers
{
    /// <summary>
    /// Progress information for asset manifest downloads.
    /// Reported by <see cref="AssetManifestInstaller"/> during multi-file downloads.
    /// </summary>
    public class AssetDownloadProgress
    {
        /// <summary>
        /// Total number of assets to download in this manifest.
        /// </summary>
        public int TotalAssets { get; set; }

        /// <summary>
        /// Number of assets that have been fully downloaded and verified.
        /// </summary>
        public int CompletedAssets { get; set; }

        /// <summary>
        /// Relative path of the asset currently being downloaded.
        /// Example: "SPYSCALP.exe" or "lib/Plugin.dll"
        /// </summary>
        public string CurrentAssetPath { get; set; }

        /// <summary>
        /// Download progress for the current asset (0.0 to 1.0).
        /// Updated as bytes are received for the current file.
        /// </summary>
        public double CurrentAssetProgress { get; set; }

        /// <summary>
        /// Overall progress across all assets (0.0 to 1.0).
        /// Calculated as: (CompletedAssets + CurrentAssetProgress) / TotalAssets
        /// </summary>
        public double OverallProgress { get; set; }

        /// <summary>
        /// Human-readable status message for display.
        /// Example: "Downloading SPYSCALP.exe (2 of 5)..."
        /// </summary>
        public string StatusMessage { get; set; }
    }
}
