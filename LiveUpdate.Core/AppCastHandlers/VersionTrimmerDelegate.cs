using System;

namespace LiveUpdate.Engine.AppCastHandlers
{
    /// <summary>
    /// Trim SemVerLike so that it can be used as a .NET style Version
    /// </summary>
    /// <param name="semVerLike">The original version specified</param>
    /// <returns>Down graded version specification</returns>
    public delegate Version VersionTrimmerDelegate(SemVerLike semVerLike);
}
