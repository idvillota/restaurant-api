using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Restaurant.Application.Features.KitchenPrinters;

public sealed class HostPrintSetupEnvelope
{
    public string Token { get; set; } = "";
    public string ApiUrl { get; set; } = "";
    public string InstallDirectory { get; set; } = @"C:\Hinova\HostPrint";
    public string ConfigJson { get; set; } = "";
}

/// <summary>Pega el config al final del Setup.exe. El instalador lo lee; el usuario no ve JSON.</summary>
public static class HostPrintSetupStamp
{
    private static readonly byte[] Magic = "HSTPNT01"u8.ToArray();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static byte[] Append(byte[] setupExe, HostPrintSetupEnvelope envelope)
    {
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
        var stamped = new byte[setupExe.Length + json.Length + Magic.Length + sizeof(int)];
        Buffer.BlockCopy(setupExe, 0, stamped, 0, setupExe.Length);
        Buffer.BlockCopy(json, 0, stamped, setupExe.Length, json.Length);
        Buffer.BlockCopy(Magic, 0, stamped, setupExe.Length + json.Length, Magic.Length);
        BitConverter.TryWriteBytes(stamped.AsSpan(stamped.Length - sizeof(int)), json.Length);
        return stamped;
    }

    public static HostPrintSetupEnvelope Read(byte[] setupExe)
    {
        if (setupExe.Length < Magic.Length + sizeof(int) + 2)
            throw new InvalidOperationException("El instalador no trae configuración de HOST.");

        var jsonLength = BitConverter.ToInt32(setupExe, setupExe.Length - sizeof(int));
        var magicOffset = setupExe.Length - sizeof(int) - Magic.Length;
        if (jsonLength <= 0 || jsonLength > setupExe.Length - Magic.Length - sizeof(int))
            throw new InvalidOperationException("El instalador no trae configuración de HOST.");

        if (!setupExe.AsSpan(magicOffset, Magic.Length).SequenceEqual(Magic))
            throw new InvalidOperationException("El instalador no trae configuración de HOST.");

        var json = Encoding.UTF8.GetString(setupExe, magicOffset - jsonLength, jsonLength);
        var envelope = JsonSerializer.Deserialize<HostPrintSetupEnvelope>(json, JsonOptions)
            ?? throw new InvalidOperationException("El instalador no trae configuración de HOST.");
        if (string.IsNullOrWhiteSpace(envelope.Token) || string.IsNullOrWhiteSpace(envelope.ConfigJson))
            throw new InvalidOperationException("El instalador no trae configuración de HOST.");

        return envelope;
    }
}
