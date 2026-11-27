using System.Diagnostics;
using System.IO.Compression;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using HostPrintSetup;

const string ServiceName = "HostPrint";
var setupPath = Environment.ProcessPath
    ?? throw new InvalidOperationException("No se encontró el instalador.");

try
{
    Console.WriteLine("HOST Print");
    Console.WriteLine("Se instalará el servicio con arranque automático.");
    var envelope = ReadEnvelope(File.ReadAllBytes(setupPath));
    var dest = envelope.InstallDirectory;
    Directory.CreateDirectory(dest);

    Console.WriteLine("Descargando el programa...");
    var zipPath = Path.Combine(Path.GetTempPath(), "HostPrint-runtime.zip");
    using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
    {
        var url = $"{envelope.ApiUrl.TrimEnd('/')}/api/kitchen/printers/pc-agent/runtime?token={Uri.EscapeDataString(envelope.Token)}";
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"No se pudieron bajar los binarios ({(int)response.StatusCode}). {body}");
        }

        await using var file = File.Create(zipPath);
        await response.Content.CopyToAsync(file);
    }

    Console.WriteLine("Copiando a " + dest);
    StopService();
    ZipFile.ExtractToDirectory(zipPath, dest, overwriteFiles: true);
    File.WriteAllText(Path.Combine(dest, "config.production.json"), envelope.ConfigJson);
    TryDelete(Path.Combine(dest, "config.development.json"));
    TryDelete(Path.Combine(dest, "config.development.local.json"));
    TryDelete(zipPath);
    ResetWorkRoot(envelope.ConfigJson);
    TryDeleteDirectory(@"C:\Hinova\HostPrintDev");

    var exe = Path.Combine(dest, "HostPrintService.exe");
    if (!File.Exists(exe))
        throw new InvalidOperationException("Falta HostPrintService.exe en el paquete.");

    InstallService(exe);
    StartService();
    Console.WriteLine("Listo. HOST Print quedó como servicio y arranca con Windows.");
}
catch (Exception ex)
{
    Console.WriteLine("Error: " + ex.Message);
    Environment.ExitCode = 1;
}

Console.WriteLine("Pulse una tecla para cerrar.");
try
{
    Console.ReadKey(true);
}
catch (InvalidOperationException)
{
    // Sin consola interactiva (tarea programada).
}

static HostPrintSetupEnvelope ReadEnvelope(byte[] setupExe)
{
    var magic = "HSTPNT01"u8.ToArray();
    if (setupExe.Length < magic.Length + sizeof(int) + 2)
        throw new InvalidOperationException("Descargue el instalador otra vez desde HOST.");

    var jsonLength = BitConverter.ToInt32(setupExe, setupExe.Length - sizeof(int));
    var magicOffset = setupExe.Length - sizeof(int) - magic.Length;
    if (jsonLength <= 0 || jsonLength > setupExe.Length - magic.Length - sizeof(int))
        throw new InvalidOperationException("Descargue el instalador otra vez desde HOST.");
    if (!setupExe.AsSpan(magicOffset, magic.Length).SequenceEqual(magic))
        throw new InvalidOperationException("Descargue el instalador otra vez desde HOST.");

    var json = Encoding.UTF8.GetString(setupExe, magicOffset - jsonLength, jsonLength);
    var envelope = JsonSerializer.Deserialize(json, SetupJsonContext.Default.HostPrintSetupEnvelope)
        ?? throw new InvalidOperationException("Descargue el instalador otra vez desde HOST.");

    if (string.IsNullOrWhiteSpace(envelope.Token) || string.IsNullOrWhiteSpace(envelope.ConfigJson))
        throw new InvalidOperationException("Descargue el instalador otra vez desde HOST.");

    return envelope;
}

static void InstallService(string exePath)
{
    RunSc($"stop {ServiceName}");
    RunSc($"delete {ServiceName}");
    Thread.Sleep(500);
    RunSc($"create {ServiceName} binPath= \"{exePath}\" start= auto DisplayName= \"HOST Print\"");
    RunSc($"failure {ServiceName} reset= 86400 actions= restart/60000");
}

static void StartService()
{
    RunSc($"start {ServiceName}");
    using var controller = new ServiceController(ServiceName);
    controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
}

static void StopService()
{
    try
    {
        using var controller = new ServiceController(ServiceName);
        if (controller.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
            return;

        RunSc($"stop {ServiceName}");
        controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
    }
    catch (InvalidOperationException)
    {
        // Todavía no existe el servicio.
    }
}

static void RunSc(string arguments)
{
    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "sc.exe",
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    });
    process?.WaitForExit(TimeSpan.FromSeconds(30));
}

static void ResetWorkRoot(string configJson)
{
    string? workRoot = null;
    using (var doc = JsonDocument.Parse(configJson))
    {
        if (doc.RootElement.TryGetProperty("WorkRoot", out var value))
            workRoot = value.GetString();
    }

    if (string.IsNullOrWhiteSpace(workRoot))
        workRoot = @"C:\Hinova\printFiles";

    Directory.CreateDirectory(workRoot);
    TryDelete(Path.Combine(workRoot, "copy-state.json"));
    foreach (var folder in new[] { "toPrint", "Printed", "error" })
        ClearDirectory(Path.Combine(workRoot, folder));
}

static void ClearDirectory(string path)
{
    Directory.CreateDirectory(path);
    foreach (var file in Directory.EnumerateFiles(path))
        TryDelete(file);
    foreach (var directory in Directory.EnumerateDirectories(path))
        TryDeleteDirectory(directory);
}

static void TryDeleteDirectory(string path)
{
    try
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
    catch (IOException)
    {
        // No impide dejar el servicio instalado.
    }
}

static void TryDelete(string path)
{
    try
    {
        if (File.Exists(path))
            File.Delete(path);
    }
    catch (IOException)
    {
        // El ZIP temporal no bloquea la instalación.
    }
}
