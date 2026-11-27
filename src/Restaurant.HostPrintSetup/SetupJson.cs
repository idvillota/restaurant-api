using System.Text.Json.Serialization;

namespace HostPrintSetup;

internal sealed class HostPrintSetupEnvelope
{
    public string Token { get; set; } = "";
    public string ApiUrl { get; set; } = "";
    public string InstallDirectory { get; set; } = @"C:\Hinova\HostPrint";
    public string ConfigJson { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HostPrintSetupEnvelope))]
internal partial class SetupJsonContext : JsonSerializerContext;
