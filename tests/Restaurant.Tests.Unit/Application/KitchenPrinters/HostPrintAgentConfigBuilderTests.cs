using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Application.Features.KitchenPrinters;
using Restaurant.Domain.Common;
using Restaurant.Infrastructure.Services;

namespace Restaurant.Tests.Unit.Application.KitchenPrinters;

public sealed class HostPrintAgentConfigBuilderTests
{
    private static readonly HashSet<string> Stations = new(StringComparer.OrdinalIgnoreCase)
    {
        "DEFAULT",
        "PARRILLA",
        "CAJA",
    };

    [Fact]
    public void MapPrinters_joins_stations_and_receipts()
    {
        var map = HostPrintAgentConfigBuilder.MapPrinters(
            [
                new HostPrintAgentPrinterDto
                {
                    WindowsPrinterName = " EPSON TM-T20 ",
                    StationCodes = ["parrilla", "DEFAULT"],
                },
                new HostPrintAgentPrinterDto
                {
                    WindowsPrinterName = "CAJA",
                    StationCodes = ["CAJA", "RECEIPT"],
                },
            ],
            Stations);

        Assert.Equal("PARRILLA,DEFAULT", map["EPSON TM-T20"]);
        Assert.Equal("CAJA,RECEIPT", map["CAJA"]);
    }

    [Fact]
    public void MapPrinters_rejects_unknown_station()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HostPrintAgentConfigBuilder.MapPrinters(
                [
                    new HostPrintAgentPrinterDto
                    {
                        WindowsPrinterName = "POS-80",
                        StationCodes = ["BARRA"],
                    },
                ],
                Stations));

        Assert.Contains("BARRA", ex.Message);
    }

    [Fact]
    public void MapPrinters_rejects_duplicate_station()
    {
        Assert.Throws<InvalidOperationException>(() =>
            HostPrintAgentConfigBuilder.MapPrinters(
                [
                    new HostPrintAgentPrinterDto
                    {
                        WindowsPrinterName = "A",
                        StationCodes = ["PARRILLA"],
                    },
                    new HostPrintAgentPrinterDto
                    {
                        WindowsPrinterName = "B",
                        StationCodes = ["PARRILLA"],
                    },
                ],
                Stations));
    }

    [Fact]
    public void ToJson_includes_tenant_and_printers_without_camel_case()
    {
        var tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var json = HostPrintAgentConfigBuilder.ToJson(new HostPrintAgentConfigSnapshot
        {
            ApiUrl = "https://api.example",
            WorkRoot = @"C:\Hinova\printFiles",
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "generated-files",
            TenantId = tenantId,
            Printers = new Dictionary<string, string> { ["POS-80"] = "DEFAULT,RECEIPT" },
        });

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(tenantId.ToString(), doc.RootElement.GetProperty("Copy").GetProperty("TenantId").GetString());
        Assert.Equal("UseDevelopmentStorage=true", doc.RootElement.GetProperty("Copy").GetProperty("ConnectionString").GetString());
        Assert.Equal("DEFAULT,RECEIPT", doc.RootElement.GetProperty("Print").GetProperty("Printers").GetProperty("POS-80").GetString());
        Assert.False(doc.RootElement.GetProperty("Print").GetProperty("Simulate").GetBoolean());
    }

    [Fact]
    public void ToJson_requires_connection_string()
    {
        Assert.Throws<InvalidOperationException>(() =>
            HostPrintAgentConfigBuilder.ToJson(new HostPrintAgentConfigSnapshot
            {
                ApiUrl = "https://api.example",
                WorkRoot = @"C:\Hinova\printFiles",
                ConnectionString = "  ",
                ContainerName = "generated-files",
                TenantId = Guid.NewGuid(),
                Printers = new Dictionary<string, string> { ["A"] = "DEFAULT" },
            }));
    }

    [Fact]
    public void Setup_stamp_round_trips_config()
    {
        var stamped = HostPrintSetupStamp.Append(
            "MZ-setup"u8.ToArray(),
            new HostPrintSetupEnvelope
            {
                Token = "abc",
                ApiUrl = "http://localhost:5228",
                InstallDirectory = @"C:\Hinova\HostPrint",
                ConfigJson = "{\"Copy\":{}}",
            });

        var envelope = HostPrintSetupStamp.Read(stamped);
        Assert.Equal("abc", envelope.Token);
        Assert.Equal("http://localhost:5228", envelope.ApiUrl);
        Assert.Contains("Copy", envelope.ConfigJson);
    }

    [Fact]
    public async Task Package_setup_embeds_config_and_runtime_skips_payload_secrets()
    {
        var tenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var root = Path.Combine(Path.GetTempPath(), "hpg-setup-" + Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(root, "payload");
        var setupDir = Path.Combine(root, "setup");
        Directory.CreateDirectory(payload);
        Directory.CreateDirectory(setupDir);
        await File.WriteAllTextAsync(Path.Combine(payload, "HostPrintService.exe"), "fake-exe");
        await File.WriteAllTextAsync(Path.Combine(payload, "config.production.json"), "secret-should-not-ship");
        await File.WriteAllBytesAsync(Path.Combine(setupDir, "HostPrintSetup.exe"), "MZ-setup"u8.ToArray());

        var kitchen = new Mock<IKitchenPrinterService>();
        kitchen.Setup(s => s.ListStationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PrinterStationDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Cocina",
                    Code = KitchenPrinterDefaults.DefaultStationCode,
                    IsActive = true,
                },
            ]);

        var tenant = new Mock<ICurrentTenantContext>();
        tenant.SetupProperty(t => t.TenantId, tenantId);
        var tickets = new HostPrintInstallTicketStore();

        var service = new HostPrintAgentPackageService(
            tenant.Object,
            kitchen.Object,
            tickets,
            Options.Create(new GeneratedFileStorageOptions
            {
                AzureBlob = new AzureBlobGeneratedFileStorageOptions
                {
                    ConnectionString = "UseDevelopmentStorage=true",
                    ContainerName = "generated-files",
                },
            }),
            Options.Create(new HostPrintAgentOptions
            {
                PayloadDirectory = payload,
                SetupPath = Path.Combine(setupDir, "HostPrintSetup.exe"),
            }),
            new TestHostEnvironment());

        try
        {
            var setup = await service.BuildSetupAsync(
                new HostPrintAgentPackageRequest
                {
                    Printers =
                    [
                        new HostPrintAgentPrinterDto
                        {
                            WindowsPrinterName = "POS-80",
                            StationCodes = ["DEFAULT", "RECEIPT"],
                        },
                    ],
                },
                "http://localhost:5228");

            Assert.Equal("HostPrint-Setup.exe", setup.FileName);
            var envelope = HostPrintSetupStamp.Read(setup.Content);
            Assert.Contains(tenantId.ToString(), envelope.ConfigJson);
            Assert.Contains("POS-80", envelope.ConfigJson);
            Assert.DoesNotContain("secret-should-not-ship", envelope.ConfigJson);

            var runtime = service.BuildRuntime(envelope.Token);
            using var zip = new ZipArchive(new MemoryStream(runtime.Content), ZipArchiveMode.Read);
            var names = zip.Entries.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("HostPrintService.exe", names);
            Assert.DoesNotContain("config.production.json", names);
            var exe = Encoding.UTF8.GetString(ReadEntry(zip, "HostPrintService.exe"));
            Assert.Equal("fake-exe", exe);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] ReadEntry(ZipArchive zip, string name)
    {
        var entry = zip.Entries.Single(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "local";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
