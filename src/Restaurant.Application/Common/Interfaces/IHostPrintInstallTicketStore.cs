namespace Restaurant.Application.Common.Interfaces;

public interface IHostPrintInstallTicketStore
{
    void Issue(string token, byte[] setupExe, TimeSpan lifetime);

    bool IsValid(string token);

    bool TryGetSetup(string token, out byte[] setupExe);
}
