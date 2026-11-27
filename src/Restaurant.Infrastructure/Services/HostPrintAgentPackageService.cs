using System.IO.Compression;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Application.Features.KitchenPrinters;

namespace Restaurant.Infrastructure.Services;

public sealed class HostPrintAgentPackageService : IHostPrintAgentPackageService
{
    private static readonly HashSet<string> PayloadSkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "config.production.json",
        "config.development.json",
        "config.development.local.json",
        "config.production.local.json",
        "HostImprimir.bat",
        "HostImprimirDev.bat",
        "InstalarHostPrint.bat",
        "copy-state.json",
        ".gitkeep",
    };

    private readonly ICurrentTenantContext _tenant;
    private readonly IKitchenPrinterService _kitchenPrinters;
    private readonly IHostPrintInstallTicketStore _tickets;
    private readonly GeneratedFileStorageOptions _generatedFiles;
    private readonly HostPrintAgentOptions _agent;
    private readonly IHostEnvironment _environment;

    public HostPrintAgentPackageService(
        ICurrentTenantContext tenant,
        IKitchenPrinterService kitchenPrinters,
        IHostPrintInstallTicketStore tickets,
        IOptions<GeneratedFileStorageOptions> generatedFiles,
        IOptions<HostPrintAgentOptions> agent,
        IHostEnvironment environment)
    {
        _tenant = tenant;
        _kitchenPrinters = kitchenPrinters;
        _tickets = tickets;
        _generatedFiles = generatedFiles.Value;
        _agent = agent.Value;
        _environment = environment;
    }

    public async Task<HostPrintAgentPackage> BuildSetupAsync(
        HostPrintAgentPackageRequest request,
        string apiUrl,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay un local seleccionado.");

        var stations = await _kitchenPrinters.ListStationsAsync(cancellationToken);
        var knownCodes = stations
            .Where(s => s.IsActive)
            .Select(s => s.Code.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var printers = HostPrintAgentConfigBuilder.MapPrinters(request.Printers, knownCodes);
        var connectionString = _generatedFiles.AzureBlob.ConnectionString?.Trim() ?? string.Empty;
        var json = HostPrintAgentConfigBuilder.ToJson(new HostPrintAgentConfigSnapshot
        {
            ApiUrl = apiUrl,
            WorkRoot = _agent.WorkRoot,
            ConnectionString = connectionString,
            ContainerName = string.IsNullOrWhiteSpace(_generatedFiles.AzureBlob.ContainerName)
                ? "generated-files"
                : _generatedFiles.AzureBlob.ContainerName,
            TenantId = tenantId,
            Printers = printers,
            SumatraPath = _agent.SumatraPath,
        });

        var setupPath = ResolveSetupPath();
        if (!File.Exists(setupPath))
            throw new InvalidOperationException(
                "Falta HostPrintSetup.exe. Publique el instalador en HostPrintAgent/setup.");

        var token = HostPrintSetupStamp.NewToken();
        var stamped = HostPrintSetupStamp.Append(
            File.ReadAllBytes(setupPath),
            new HostPrintSetupEnvelope
            {
                Token = token,
                ApiUrl = apiUrl,
                InstallDirectory = _agent.InstallDirectory,
                ConfigJson = json,
            });
        _tickets.Issue(token, stamped, TimeSpan.FromHours(2));

        return new HostPrintAgentPackage
        {
            Content = stamped,
            FileName = "HostPrint-Setup.exe",
            Token = token,
            IncludesRuntime = Directory.Exists(ResolvePayloadDirectory())
                && Directory.EnumerateFiles(ResolvePayloadDirectory()).Any(IsRuntimeFile),
            ContentType = "application/octet-stream",
        };
    }

    public HostPrintAgentPackage BuildRuntime(string token)
    {
        if (!_tickets.IsValid(token))
            throw new InvalidOperationException("El instalador expiró. Descargue de nuevo desde HOST.");

        var payloadDirectory = ResolvePayloadDirectory();
        if (!Directory.Exists(payloadDirectory))
            throw new InvalidOperationException("No hay binarios de Host Print en el API.");

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var added = 0;
            foreach (var file in Directory.EnumerateFiles(payloadDirectory))
            {
                var name = Path.GetFileName(file);
                if (PayloadSkipNames.Contains(name)
                    || name.EndsWith(".local.json", StringComparison.OrdinalIgnoreCase)
                    || IsDevPayload(name))
                    continue;

                zip.CreateEntryFromFile(file, name, CompressionLevel.NoCompression);
                added++;
            }

            if (added == 0)
                throw new InvalidOperationException("No hay binarios de Host Print en el API.");
        }

        return new HostPrintAgentPackage
        {
            Content = buffer.ToArray(),
            FileName = "HostPrint-runtime.zip",
            IncludesRuntime = true,
            ContentType = "application/zip",
        };
    }

    private string ResolvePayloadDirectory()
    {
        var configured = _agent.PayloadDirectory;
        if (string.IsNullOrWhiteSpace(configured))
            return Path.Combine(_environment.ContentRootPath, "HostPrintAgent", "payload");

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.ContentRootPath, configured);
    }

    private string ResolveSetupPath()
    {
        var configured = _agent.SetupPath;
        if (string.IsNullOrWhiteSpace(configured))
            return Path.Combine(_environment.ContentRootPath, "HostPrintAgent", "setup", "HostPrintSetup.exe");

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.ContentRootPath, configured);
    }

    private static bool IsRuntimeFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("HostPrintService.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("SumatraPDF.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDevPayload(string name) =>
        name.Contains("development", StringComparison.OrdinalIgnoreCase)
        || name.Contains("HostPrintDev", StringComparison.OrdinalIgnoreCase)
        || name.Contains("ImprimirDev", StringComparison.OrdinalIgnoreCase);
}
