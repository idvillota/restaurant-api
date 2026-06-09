namespace Restaurant.Application.Common.Options;

public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAi";

    public string Endpoint { get; set; } = string.Empty;

    // Optional: if not provided, DefaultAzureCredential will be used
    public string? ApiKey { get; set; }

    // Deployment name configured in Azure OpenAI (required when calling the service)
    public string DeploymentName { get; set; } = string.Empty;

    // Approximate max prompt length in characters to avoid hitting service limits
    public int MaxPromptChars { get; set; } = 120_000;
}
