// =============================================================================
// AssetManifestInstaller.cs — Per-File Granular Update Installer for LUPDATE
// =============================================================================
//
// PURPOSE:
//   Downloads and installs individual files specified in an AppCast item's
//   asset manifest. This enables granular/delta updates where only changed
//   files are downloaded, saving bandwidth and time.
//
// HOW IT WORKS:
//   1. Receives a list of UpdateAsset objects (from AppCastItem.Assets)
//   2. For each asset:
//      a. Downloads the file from its URL to a temp location
//      b. Verifies SHA256 hash (if provided)
//      c. Copies to the target directory at the asset's RelativePath
//      d. Reports progress
//   3. On any failure, can fall back to the ZIP bundle approach
//
// INTEGRATION:
//   Called by SparkleUpdater when AppCastItem.HasAssetManifest is true.
//   The standard single-file download pipeline is bypassed in favor of
//   this multi-file downloader.
//
// FALLBACK:
//   If any asset download fails, the caller can fall back to the
//   <enclosure> URL (typically a full ZIP) as a recovery mechanism.
//
// DEPENDENCIES:
//   - System.Net.WebClient (built into .NET Framework)
//   - System.Security.Cryptography (for SHA256 verification)
//
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LiveUpdate.Engine.Interfaces;

namespace LiveUpdate.Engine.Installers
{
    /// <summary>
    /// Downloads and installs individual assets from an asset manifest.
    /// Supports SHA256 hash verification and per-file progress reporting.
    /// </summary>
    public class AssetManifestInstaller
    {
        // =====================================================================
        // FIELDS
        // =====================================================================

        private readonly ILogger _logger;

        // =====================================================================
        // CONSTRUCTOR
        // =====================================================================

        /// <summary>
        /// Creates a new AssetManifestInstaller.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostic output. Can be null.</param>
        public AssetManifestInstaller(ILogger logger = null)
        {
            _logger = logger;
        }

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        /// <summary>
        /// Downloads all assets in the manifest to the target directory.
        /// Each asset is downloaded to a temp file, verified, then moved
        /// to its final location within targetDir.
        ///
        /// Progress is reported per-file and overall via the progress callback.
        /// </summary>
        /// <param name="assets">
        ///   List of UpdateAsset objects describing files to download.
        ///   Each has a RelativePath (where to put it) and DownloadUrl (where to get it).
        /// </param>
        /// <param name="targetDir">
        ///   The application's install directory. Assets are placed at
        ///   targetDir + asset.RelativePath.
        ///   Example: "C:\Program Files\SPYSCALP"
        /// </param>
        /// <param name="progress">
        ///   Optional progress reporter. Receives AssetDownloadProgress updates
        ///   for UI display. Can be null to skip progress reporting.
        /// </param>
        /// <param name="cancellationToken">
        ///   Token to cancel the download. Remaining assets are skipped on cancellation.
        /// </param>
        /// <returns>
        ///   True if ALL assets were downloaded and installed successfully.
        ///   False if any asset failed (partial update — caller should consider
        ///   falling back to the ZIP bundle).
        /// </returns>
        public async Task<bool> DownloadAndInstallAsync(
            List<UpdateAsset> assets,
            string targetDir,
            IProgress<AssetDownloadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (assets == null || assets.Count == 0)
            {
                _logger?.PrintMessage("AssetManifestInstaller: No assets to download");
                return true;
            }

            _logger?.PrintMessage("AssetManifestInstaller: Starting download of {0} assets to {1}",
                assets.Count.ToString(), targetDir);

            int completed = 0;
            int failed = 0;

            foreach (var asset in assets)
            {
                // Check for cancellation before each asset
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger?.PrintMessage("AssetManifestInstaller: Download cancelled by user");
                    return false;
                }

                // Report progress: starting this asset
                ReportProgress(progress, assets.Count, completed, asset.RelativePath, 0.0,
                    string.Format("Downloading {0} ({1} of {2})...",
                        asset.RelativePath, completed + 1, assets.Count));

                bool success = await DownloadSingleAssetAsync(asset, targetDir, progress, assets.Count, completed);

                if (success)
                {
                    completed++;
                    _logger?.PrintMessage("AssetManifestInstaller: [{0}/{1}] OK: {2}",
                        completed.ToString(), assets.Count.ToString(), asset.RelativePath);
                }
                else
                {
                    failed++;
                    _logger?.PrintMessage("AssetManifestInstaller: [{0}/{1}] FAILED: {2}",
                        (completed + failed).ToString(), assets.Count.ToString(), asset.RelativePath);
                    // Continue downloading remaining assets — caller decides on partial failure
                }
            }

            // Final progress report
            ReportProgress(progress, assets.Count, completed, "", 1.0,
                failed == 0 ? "All assets downloaded successfully" : string.Format("{0} assets failed", failed));

            _logger?.PrintMessage("AssetManifestInstaller: Complete. {0} succeeded, {1} failed out of {2}",
                completed.ToString(), failed.ToString(), assets.Count.ToString());

            return failed == 0;
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        /// <summary>
        /// Downloads a single asset to its target location.
        /// 
        /// Flow:
        ///   1. Download to temp file
        ///   2. Verify SHA256 hash (if provided)
        ///   3. Create target subdirectory if needed
        ///   4. Move temp file to final location (overwriting)
        /// </summary>
        private async Task<bool> DownloadSingleAssetAsync(
            UpdateAsset asset,
            string targetDir,
            IProgress<AssetDownloadProgress> progress,
            int totalAssets,
            int completedSoFar)
        {
            // Validate asset data
            if (string.IsNullOrWhiteSpace(asset.DownloadUrl))
            {
                _logger?.PrintMessage("AssetManifestInstaller: ERROR — asset has no download URL: {0}",
                    asset.RelativePath ?? "(null)");
                return false;
            }

            if (string.IsNullOrWhiteSpace(asset.RelativePath))
            {
                _logger?.PrintMessage("AssetManifestInstaller: ERROR — asset has no relative path");
                return false;
            }

            // Build file paths
            // Normalize forward slashes in RelativePath to OS path separators
            string normalizedPath = asset.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            string finalPath = Path.Combine(targetDir, normalizedPath);
            string tempPath = Path.Combine(Path.GetTempPath(), "lupdate_asset_" + Guid.NewGuid().ToString("N"));

            try
            {
                // ---------------------------------------------------------
                // Step 1: Download to temp file
                // ---------------------------------------------------------
                _logger?.PrintMessage("AssetManifestInstaller: Downloading {0}", asset.DownloadUrl);

                using (var client = new WebClient())
                {
                    // Wire up progress reporting for this individual file
                    client.DownloadProgressChanged += (sender, e) =>
                    {
                        double fileProgress = e.ProgressPercentage / 100.0;
                        ReportProgress(progress, totalAssets, completedSoFar, asset.RelativePath, fileProgress,
                            string.Format("Downloading {0} ({1} of {2})... {3}%",
                                asset.RelativePath, completedSoFar + 1, totalAssets, e.ProgressPercentage));
                    };

                    await client.DownloadFileTaskAsync(new Uri(asset.DownloadUrl), tempPath);
                }

                // ---------------------------------------------------------
                // Step 2: Verify SHA256 hash (if provided)
                // ---------------------------------------------------------
                if (!string.IsNullOrWhiteSpace(asset.Hash))
                {
                    string actualHash = ComputeSHA256(tempPath);
                    if (!string.Equals(actualHash, asset.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.PrintMessage("AssetManifestInstaller: HASH MISMATCH for {0}", asset.RelativePath);
                        _logger?.PrintMessage("  Expected: {0}", asset.Hash);
                        _logger?.PrintMessage("  Actual:   {0}", actualHash);
                        // Clean up temp file
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                        return false;
                    }
                    _logger?.PrintMessage("AssetManifestInstaller: Hash verified for {0}", asset.RelativePath);
                }

                // ---------------------------------------------------------
                // Step 3: Ensure target directory exists
                // ---------------------------------------------------------
                string finalDir = Path.GetDirectoryName(finalPath);
                if (!string.IsNullOrEmpty(finalDir) && !Directory.Exists(finalDir))
                {
                    Directory.CreateDirectory(finalDir);
                }

                // ---------------------------------------------------------
                // Step 4: Move temp file to final location (overwrite)
                // ---------------------------------------------------------
                // File.Move doesn't support overwrite in .NET Framework,
                // so we copy + delete instead.
                File.Copy(tempPath, finalPath, overwrite: true);
                File.Delete(tempPath);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.PrintMessage("AssetManifestInstaller: ERROR downloading {0}: {1}",
                    asset.RelativePath, ex.Message);

                // Clean up temp file on failure
                try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                catch { /* ignore cleanup errors */ }

                return false;
            }
        }

        /// <summary>
        /// Computes the SHA256 hash of a file and returns it as a lowercase hex string.
        /// Used for verifying downloaded assets against the hash in the asset manifest.
        /// </summary>
        private static string ComputeSHA256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha256.ComputeHash(stream);
                // Convert to lowercase hex string (e.g., "a1b2c3d4...")
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Helper to report progress if a progress reporter is available.
        /// Calculates overall progress from completed assets + current file progress.
        /// </summary>
        private static void ReportProgress(
            IProgress<AssetDownloadProgress> progress,
            int totalAssets,
            int completedAssets,
            string currentAssetPath,
            double currentAssetProgress,
            string statusMessage)
        {
            if (progress == null) return;

            double overall = totalAssets > 0
                ? (completedAssets + currentAssetProgress) / totalAssets
                : 0.0;

            progress.Report(new AssetDownloadProgress
            {
                TotalAssets = totalAssets,
                CompletedAssets = completedAssets,
                CurrentAssetPath = currentAssetPath,
                CurrentAssetProgress = currentAssetProgress,
                OverallProgress = overall,
                StatusMessage = statusMessage
            });
        }
    }
}
