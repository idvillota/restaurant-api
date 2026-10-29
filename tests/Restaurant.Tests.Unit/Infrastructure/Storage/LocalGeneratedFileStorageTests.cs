using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Options;
using Restaurant.Infrastructure.Storage;

namespace Restaurant.Tests.Unit.Infrastructure.Storage;

public sealed class LocalGeneratedFileStorageTests
{
    [Fact]
    public async Task OpenReadAsync_rejects_parent_directory_segments()
    {
        var root = Path.Combine(Path.GetTempPath(), "host-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new LocalGeneratedFileStorage(
                Options.Create(new GeneratedFileStorageOptions
                {
                    Local = new LocalGeneratedFileStorageOptions { RootPath = root },
                }),
                new TestHostEnvironment());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                storage.OpenReadAsync("../secret.xml"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "local";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
