using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiveUpdate.Engine.AppCastHandlers;
using LiveUpdate.Engine.AssemblyAccessors;
using LiveUpdate.Engine.Configurations;
using LiveUpdate.Engine.Downloaders;
using LiveUpdate.Engine.Enums;
using LiveUpdate.Engine.Interfaces;
using LiveUpdate.Engine.SignatureVerifiers;

namespace LiveUpdate.Engine
{
    /// <summary>
    /// LiveUpdateChecker provides a CHECK-ONLY API for applications that simply want to
    /// know if an update is available, without downloading or installing anything.
    /// 
    /// Use this class in your own applications (e.g., SPYSCALP) to check for updates.
    /// When an update is detected, you can launch LUPDATE.exe to handle the download
    /// and installation.
    /// 
    /// Usage:
    ///   var checker = new LiveUpdateChecker("https://example.com/appcast.xml");
    ///   var result = await checker.CheckForUpdatesAsync();
    ///   if (result.Status == UpdateStatus.UpdateAvailable)
    ///   {
    ///       // Launch LUPDATE.exe or show notification
    ///   }
    /// </summary>
    public class LiveUpdateChecker : IDisposable
    {
        private readonly string _appCastUrl;
        private readonly ISignatureVerifier _signatureVerifier;
        private readonly string _referenceAssembly;
        private ILogger _logWriter;
        private IAppCastDataDownloader _appCastDataDownloader;
        private AppCastHelper _appCastHelper;
        private IAppCastGenerator _appCastGenerator;
        private Configuration _configuration;
        private bool _disposed;

        /// <summary>
        /// Creates a new LiveUpdateChecker with unsafe signature verification (for development/testing).
        /// </summary>
        /// <param name="appCastUrl">URL to the appcast XML or JSON file</param>
        public LiveUpdateChecker(string appCastUrl)
            : this(appCastUrl, new Ed25519Checker(SecurityMode.Unsafe))
        { }

        /// <summary>
        /// Creates a new LiveUpdateChecker with a specified signature verifier.
        /// </summary>
        /// <param name="appCastUrl">URL to the appcast XML or JSON file</param>
        /// <param name="signatureVerifier">Signature verifier for appcast validation</param>
        public LiveUpdateChecker(string appCastUrl, ISignatureVerifier signatureVerifier)
            : this(appCastUrl, signatureVerifier, null)
        { }

        /// <summary>
        /// Creates a new LiveUpdateChecker with full configuration.
        /// </summary>
        /// <param name="appCastUrl">URL to the appcast XML or JSON file</param>
        /// <param name="signatureVerifier">Signature verifier for appcast validation</param>
        /// <param name="referenceAssembly">Path to the assembly whose version to check against</param>
        public LiveUpdateChecker(string appCastUrl, ISignatureVerifier signatureVerifier, string referenceAssembly)
        {
            _appCastUrl = appCastUrl ?? throw new ArgumentNullException(nameof(appCastUrl));
            _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
            _referenceAssembly = referenceAssembly;
            _logWriter = new LogWriter();
        }

        /// <summary>
        /// Gets or sets the logger for diagnostic messages.
        /// </summary>
        public ILogger LogWriter
        {
            get => _logWriter;
            set => _logWriter = value;
        }

        /// <summary>
        /// Gets or sets the appcast data downloader. If not set, uses WebRequestAppCastDataDownloader.
        /// </summary>
        public IAppCastDataDownloader AppCastDataDownloader
        {
            get
            {
                if (_appCastDataDownloader == null)
                {
                    _appCastDataDownloader = new WebRequestAppCastDataDownloader(_logWriter);
                }
                return _appCastDataDownloader;
            }
            set => _appCastDataDownloader = value;
        }

        /// <summary>
        /// Gets or sets the appcast helper for parsing and filtering updates.
        /// </summary>
        public AppCastHelper AppCastHelper
        {
            get
            {
                if (_appCastHelper == null)
                {
                    _appCastHelper = new AppCastHelper();
                }
                return _appCastHelper;
            }
            set => _appCastHelper = value;
        }

        /// <summary>
        /// Gets or sets the appcast generator for deserializing appcast data.
        /// </summary>
        public IAppCastGenerator AppCastGenerator
        {
            get
            {
                if (_appCastGenerator == null)
                {
                    _appCastGenerator = new XMLAppCastGenerator(_logWriter);
                }
                return _appCastGenerator;
            }
            set => _appCastGenerator = value;
        }

        /// <summary>
        /// Gets or sets the configuration object for tracking last-checked time, skipped versions, etc.
        /// </summary>
        public Configuration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    _configuration = new RegistryConfiguration(new AssemblyDiagnosticsAccessor(_referenceAssembly));
                }
                return _configuration;
            }
            set => _configuration = value;
        }

        /// <summary>
        /// A cache of the most recently downloaded appcast, for convenience.
        /// </summary>
        public AppCast AppCastCache { get; private set; }

        /// <summary>
        /// Checks for updates asynchronously. This is the main public API.
        /// Returns information about whether an update is available and what version it is.
        /// Does NOT download or install anything.
        /// </summary>
        /// <param name="ignoreSkippedVersions">If true, shows updates the user previously skipped</param>
        /// <returns>UpdateInfo with status and available updates</returns>
        public async Task<UpdateInfo> CheckForUpdatesAsync(bool ignoreSkippedVersions = false)
        {
            List<AppCastItem> updates = null;

            _logWriter?.PrintMessage("LiveUpdateChecker: Downloading and checking appcast from {0}", _appCastUrl);

            // Set up the appcast helper
            AppCastHelper.SetupAppCastHelper(AppCastDataDownloader, _appCastUrl,
                Configuration.InstalledVersion, _signatureVerifier, _logWriter);

            try
            {
                _logWriter?.PrintMessage("LiveUpdateChecker: Starting appcast download...");
                var appCastStr = await AppCastHelper.DownloadAppCast();
                if (appCastStr != null && !string.IsNullOrWhiteSpace(appCastStr))
                {
                    _logWriter?.PrintMessage("LiveUpdateChecker: Appcast downloaded successfully. Parsing...");
                    var appCast = AppCastCache = await AppCastGenerator.DeserializeAppCastAsync(appCastStr);
                    _logWriter?.PrintMessage("LiveUpdateChecker: Appcast parsed. Getting available updates...");
                    updates = AppCastHelper.FilterUpdates(appCast.Items);
                }
            }
            catch (Exception e)
            {
                _logWriter?.PrintMessage("LiveUpdateChecker: Failed to read/parse appcast: {0}", e.Message);
                return new UpdateInfo(UpdateStatus.CouldNotDetermine);
            }

            if (updates == null)
            {
                _logWriter?.PrintMessage("LiveUpdateChecker: No version information found in appcast");
                return new UpdateInfo(UpdateStatus.CouldNotDetermine);
            }

            // Touch the last check timestamp
            Configuration.TouchCheckTime();

            if (updates.Count == 0)
            {
                _logWriter?.PrintMessage("LiveUpdateChecker: Installed version is latest ({0})",
                    Configuration.InstalledVersion ?? "[unknown]");
                return new UpdateInfo(UpdateStatus.UpdateNotAvailable, updates);
            }

            _logWriter?.PrintMessage("LiveUpdateChecker: Update available! Latest version: {0}",
                updates[0].Version ?? "[Unknown]");

            // Check if user previously skipped this version
            if (!ignoreSkippedVersions && (updates[0].Version?.Equals(Configuration.LastVersionSkipped) ?? false))
            {
                _logWriter?.PrintMessage("LiveUpdateChecker: Latest version was previously skipped by user");
                return new UpdateInfo(UpdateStatus.UserSkipped, updates);
            }

            return new UpdateInfo(UpdateStatus.UpdateAvailable, updates);
        }

        /// <summary>
        /// Simple synchronous check — wraps the async version.
        /// Use CheckForUpdatesAsync when possible.
        /// </summary>
        public UpdateInfo CheckForUpdates(bool ignoreSkippedVersions = false)
        {
            return Task.Run(() => CheckForUpdatesAsync(ignoreSkippedVersions)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
