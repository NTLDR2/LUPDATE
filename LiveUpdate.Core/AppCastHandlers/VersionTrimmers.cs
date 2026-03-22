using System;

namespace LiveUpdate.Engine.AppCastHandlers
{
    /// <summary>
    /// Implementations of VersionTrimmer
    /// </summary>
    public static class VersionTrimmers
    {
        /// <summary>
        /// Remove pre-build and build specification
        /// </summary>
        /// <param name="semVerLike"></param>
        /// <returns></returns>
        public static Version DefaultVersionTrimmer(SemVerLike semVerLike)
        {
            return string.IsNullOrWhiteSpace(semVerLike.Version)
                ? new Version(0, 0, 0, 0)
                : new Version(semVerLike.Version);
        }
    }
}
