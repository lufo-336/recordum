namespace Recordum.Plugins.Contracts.Manifest;

public sealed record ManifestValidationResult(bool IsValid, IReadOnlyList<string> Errors);

public interface IManifestValidator
{
    ManifestValidationResult Validate(PluginManifest manifest);
}
