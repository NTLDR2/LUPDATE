using LiveUpdate.Engine.AppCastHandlers;
using LiveUpdate.Engine.Configurations;
using System.Text.Json.Serialization;

namespace LiveUpdate.Engine
{
#if !NETFRAMEWORK && !NETSTANDARD
    [JsonSerializable(typeof(AppCast))]
    [JsonSerializable(typeof(AppCastItem))]
    [JsonSerializable(typeof(SavedConfigurationData))]
    [JsonSerializable(typeof(SemVerLike))]
    internal partial class SourceGenerationContext : JsonSerializerContext { }
#endif
}
