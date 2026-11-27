using Restaurant.Application.Features.KitchenPrinters;

namespace Restaurant.Application.Common.Interfaces;

public interface IHostPrintAgentPackageService
{
    Task<HostPrintAgentPackage> BuildSetupAsync(
        HostPrintAgentPackageRequest request,
        string apiUrl,
        CancellationToken cancellationToken = default);

    HostPrintAgentPackage BuildRuntime(string token);
}
