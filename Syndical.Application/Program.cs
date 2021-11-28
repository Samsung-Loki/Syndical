using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CommandLine;
using Spectre.Console;
using Syndical.Library;

namespace Syndical.Application
{
    /// <summary>
    /// Entrypoint class
    /// </summary>
    public static class Program
    {
        [Flags]
        private enum Mode { Download, Decrypt, DownloadDecrypt, Fetch }

        /// <summary>
        /// Commandline options
        /// </summary>
        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
        private class Options
        {
            [Option('m', "mode", Required = true, HelpText = "Which mode I should use")]
            public Mode Mode { get; set; }

            [Option('v', "firmware-version", Required = false, HelpText = "Firmware version")]
            public string FirmwareVersion { get; set; }

            [Option('i', "input", SetName = "Decryption", Required = false, HelpText = "File to decrypt")]
            public string InputFilename { get; set; }
            
            [Option('o', "output", Required = false, HelpText = "Filename for decrypted/downloaded file")]
            public string OutputFilename { get; set; }
            
            [Option('M', "model", Required = true, HelpText = "Device model")]
            public string Model { get; set; }
            
            [Option('r', "region", Required = true, HelpText = "Device region")]
            public string Region { get; set; }
            
            [Option('f', "factory", Required = false, HelpText = "Download factory firmware (Binary Nature)")]
            public bool FactoryFirmware { get; set; }
            
            [Option('c', "disable-hash-check", Required = false, HelpText = "Disables hash check in Download mode")]
            public bool DisableHashCheck { get; set; }
            
            [Option('d', "disable-resume", Required = false, HelpText = "Disables resume in Download mode")]
            public bool DisableResume { get; set; }
        }
        
        /// <summary>
        /// Entrypoint method
        /// </summary>
        /// <param name="args">Arguments</param>
        [HandleProcessCorruptedStateExceptions]
        public static void Main(string[] args)
        {
            try {
                Parser.Default.ParseArguments<Options>(args).WithParsed(o => {
                    switch (o.Mode)
                    {
                        case Mode.DownloadDecrypt:
                            AnsiConsole.MarkupLine($"[bold]Device:[/] {o.Model}/{o.Region}");
                            if (string.IsNullOrEmpty(o.FirmwareVersion)) {
                                AnsiConsole.MarkupLine("[red]Firmware version required![/]");
                                return;
                            }
                            o.FirmwareVersion = o.FirmwareVersion.NormalizeVersion();
                            AnsiConsole.MarkupLine($"[bold]Firmware:[/] {o.FirmwareVersion}");
                            
                            AnsiConsole.MarkupLine("[yellow]Connecting to FUS server...[/]");
                            var clientDownloadD = new FusClient();
                            var typeDownloadD = o.FactoryFirmware
                                ? FirmwareInfo.FirmwareType.Factory
                                : FirmwareInfo.FirmwareType.Home;
                            
                            AnsiConsole.MarkupLine("[yellow]Verifying firmware version...[/]");
                            if (!Fetcher.FirmwareExists(o.Model, o.Region, o.FirmwareVersion, true)) {
                                AnsiConsole.MarkupLine("[red]Firmware does not exist![/]");
                                return;
                            }
                            
                            AnsiConsole.MarkupLine("[yellow]Fetching firmware information...[/]");
                            var infoD = clientDownloadD.GetFirmwareInformation(o.FirmwareVersion, o.Model,
                                o.Region, typeDownloadD);
                            
                            AnsiConsole.MarkupLine("[yellow]Initializing download...[/]");
                            clientDownloadD.InitializeDownload(infoD);

                            var destD = string.IsNullOrEmpty(o.OutputFilename) ? infoD.FileName
                                .Replace(".enc2", "").Replace(".enc4", "") : o.OutputFilename;
                            var startD = File.Exists(destD) ? new FileInfo(destD).Length : 0;
                            AnsiConsole.MarkupLine($"[bold]Destination:[/] {destD}");
                            if (startD > 0) {
                                AnsiConsole.MarkupLine("[red]Resume is not supported! Delete destrination file first.[/]");
                                return;
                            }

                            AnsiConsole.Progress()
                                .Columns(new TaskDescriptionColumn(),
                                    new ProgressBarColumn(),
                                    new PercentageColumn(),
                                    new DownloadedColumn(),
                                    new TransferSpeedColumn(),
                                    new RemainingTimeColumn(),
                                    new ElapsedTimeColumn())
                                .Start(ctx => {
                                    // Begin download
                                    var res = clientDownloadD.DownloadFirmware(infoD);
                                    AnsiConsole.MarkupLine("[yellow]WANRING: No hash check will be performed.[/]");
                                    // Initialize block/size variables
                                    var block = 0x80 * 50;
                                    var realSize = long.Parse(res.Headers["Content-Length"]!);
                                    if (realSize != infoD.FileSize)
                                        AnsiConsole.MarkupLine(
                                            $"[yellow]Content-Length is different than reported size: {realSize}/{infoD.FileSize}[/]");
                                    // Tasks
                                    var task = ctx.AddTask("[yellow]Downloading & Decrypting firmware[/]", maxValue: realSize);
                                    // Download & Decrypt binary
                                    using var data = res.GetResponseStream();
                                    using var file = new FileStream(destD, FileMode.OpenOrCreate);
                                    using var rj = new RijndaelManaged();
                                    rj.Mode = CipherMode.ECB;
                                    rj.BlockSize = 0x80;
                                    rj.Padding = PaddingMode.PKCS7;
                                    rj.Key = infoD.DecryptionKey;
                                    using var transform = rj.CreateDecryptor();
                                    using var decryptor = new CryptoStream(data, 
                                        transform, CryptoStreamMode.Read);
                                    bool stop = false;
                                    var buf = new byte[block];
                                    long readTotal = 0;
                                    while (!stop) {
                                        var read = decryptor.Read(buf, 0, buf.Length);
                                        if (realSize - readTotal < block) stop = true;
                                        file.Write(buf, 0, read);
                                        task.Increment(read);
                                        readTotal += read;
                                    }
                                    
                                    task.Increment(task.MaxValue - task.Value);
                                    AnsiConsole.MarkupLine($"[green]Firmware download & decryption done![/]");

                                    // Tasks stuff
                                    task.Description = "[green]Downloading & Decrypting firmware[/]";
                                    task.StopTask();
                                });
                            break;
                        case Mode.Decrypt:
                            AnsiConsole.MarkupLine($"[bold]Device:[/] {o.Model}/{o.Region}");
                            if (string.IsNullOrEmpty(o.FirmwareVersion)) {
                                AnsiConsole.MarkupLine("[red]Firmware version required![/]");
                                return;
                            }
                            o.FirmwareVersion = o.FirmwareVersion.NormalizeVersion();
                            AnsiConsole.MarkupLine($"[bold]Firmware:[/] {o.FirmwareVersion}");
                            
                            AnsiConsole.MarkupLine("[yellow]Connecting to FUS server...[/]");
                            var clientDecrypt = new FusClient();
                            var typeDecrypt = o.FactoryFirmware
                                ? FirmwareInfo.FirmwareType.Factory
                                : FirmwareInfo.FirmwareType.Home;
                            
                            AnsiConsole.MarkupLine("[yellow]Verifying firmware version...[/]");
                            if (!Fetcher.FirmwareExists(o.Model, o.Region, o.FirmwareVersion, true)) {
                                AnsiConsole.MarkupLine("[red]Firmware does not exist![/]");
                                return;
                            }
                            
                            AnsiConsole.MarkupLine("[yellow]Fetching firmware information...[/]");
                            var infoDecrypt = clientDecrypt.GetFirmwareInformation(o.FirmwareVersion, o.Model,
                                o.Region, typeDecrypt);
                            
                            var srcDecrypt = string.IsNullOrEmpty(o.InputFilename) ? infoDecrypt.FileName : o.InputFilename;
                            var destDecrypt = string.IsNullOrEmpty(o.OutputFilename) ? infoDecrypt.FileName
                                .Replace(".enc2", "").Replace(".enc4", "") : o.OutputFilename;
                            AnsiConsole.MarkupLine($"[bold]Source:[/] {srcDecrypt}");
                            AnsiConsole.MarkupLine($"[bold]Destination:[/] {destDecrypt}");
                            
                            AnsiConsole.Progress()
                                .Columns(new TaskDescriptionColumn(),
                                    new ProgressBarColumn(),
                                    new PercentageColumn(),
                                    new DownloadedColumn(),
                                    new TransferSpeedColumn(),
                                    new RemainingTimeColumn(),
                                    new ElapsedTimeColumn())
                                .Start(ctx => {
                                    var block = 800;
                                    var realSize = new FileInfo(srcDecrypt).Length;
                                    if (realSize != infoDecrypt.FileSize)
                                        AnsiConsole.MarkupLine(
                                            $"[yellow]File size is different than reported size: {realSize}/{infoDecrypt.FileSize}[/]");
                                    var task = ctx.AddTask("[yellow]Decrypting firmware[/]", maxValue: realSize);
                                    using var srcStream = new FileStream(srcDecrypt, FileMode.Open, FileAccess.Read);
                                    using var destStream = new FileStream(destDecrypt, FileMode.Create, FileAccess.Write);
                                    using var rj = new RijndaelManaged();
                                    rj.Mode = CipherMode.ECB;
                                    rj.BlockSize = 0x80;
                                    rj.Padding = PaddingMode.PKCS7;
                                    rj.Key = infoDecrypt.DecryptionKey;
                                    destStream.Seek(0, SeekOrigin.Begin);
                                    using var transform = rj.CreateDecryptor();
                                    using var decryptor = new CryptoStream(srcStream, 
                                        transform, CryptoStreamMode.Read);
                                    bool stop = false;
                                    var buf = new byte[block];
                                    long readTotal = 0;
                                    while (!stop) {
                                        int read = decryptor.Read(buf, 0, buf.Length);
                                        if (realSize - readTotal < block) stop = true;
                                        destStream.Write(buf, 0, read);
                                        task.Increment(read);
                                        readTotal += read;
                                    }
                                    
                                    task.Increment(task.MaxValue - task.Value);
                                    task.Description = "[green]Decrypting firmware[/]";
                                    task.StopTask();
                                });
                            break;
                        case Mode.Download:
                            AnsiConsole.MarkupLine($"[bold]Device:[/] {o.Model}/{o.Region}");
                            if (string.IsNullOrEmpty(o.FirmwareVersion)) {
                                AnsiConsole.MarkupLine("[red]Firmware version required![/]");
                                return;
                            }
                            o.FirmwareVersion = o.FirmwareVersion.NormalizeVersion();
                            AnsiConsole.MarkupLine($"[bold]Firmware:[/] {o.FirmwareVersion}");
                            
                            AnsiConsole.MarkupLine("[yellow]Connecting to FUS server...[/]");
                            var clientDownload = new FusClient();
                            var typeDownload = o.FactoryFirmware
                                ? FirmwareInfo.FirmwareType.Factory
                                : FirmwareInfo.FirmwareType.Home;
                            
                            AnsiConsole.MarkupLine("[yellow]Verifying firmware version...[/]");
                            if (!Fetcher.FirmwareExists(o.Model, o.Region, o.FirmwareVersion, true)) {
                                AnsiConsole.MarkupLine("[red]Firmware does not exist![/]");
                                return;
                            }
                            
                            AnsiConsole.MarkupLine("[yellow]Fetching firmware information...[/]");
                            var info = clientDownload.GetFirmwareInformation(o.FirmwareVersion, o.Model,
                                o.Region, typeDownload);
                            
                            AnsiConsole.MarkupLine("[yellow]Initializing download...[/]");
                            clientDownload.InitializeDownload(info);

                            var dest = string.IsNullOrEmpty(o.OutputFilename) ? info.FileName : o.OutputFilename;
                            var start = File.Exists(dest) ? new FileInfo(dest).Length : 0;
                            if (start > 0 && o.DisableResume) {
                                AnsiConsole.MarkupLine("[red]Resume was disabled! Delete destrination file first.[/]");
                                return;
                            }
                            
                            AnsiConsole.MarkupLine($"[bold]Destination:[/] {dest}");

                            AnsiConsole.Progress()
                                .Columns(new TaskDescriptionColumn(),
                                    new ProgressBarColumn(),
                                    new PercentageColumn(),
                                    new DownloadedColumn(),
                                    new TransferSpeedColumn(),
                                    new RemainingTimeColumn(),
                                    new ElapsedTimeColumn())
                                .Start(ctx => {
                                    // Begin download
                                    var res = clientDownload.DownloadFirmware(info, start);
                                    // Initialize block/size variables
                                    var block = 128;
                                    var realSize = long.Parse(res.Headers["Content-Length"]!);
                                    AnsiConsole.MarkupLine($"[yellow]Download from {start} to {realSize}[/]");
                                    if (realSize != info.FileSize)
                                        AnsiConsole.MarkupLine(
                                            $"[yellow]Content-Length is different than reported size: {realSize}/{info.FileSize}[/]");
                                    // Tasks
                                    var task = ctx.AddTask("[yellow]Downloading firmware[/]", maxValue: realSize);
                                    var hash = ctx.AddTask("[cyan]Verifying hash[/]", false, realSize)
                                        .IsIndeterminate();
                                    if (o.DisableHashCheck) {
                                        hash.IsIndeterminate(false);
                                        hash.Increment(realSize);
                                        hash.Description = "[green]Hash check disabled[/]";
                                    }
                                    task.Increment(start);
                                    // Download binary
                                    if (start < realSize) { // Resume
                                        if (start > 0) AnsiConsole.MarkupLine("[yellow]WANRING: Resume mode might work not as expected.[/]");
                                        using var data = res.GetResponseStream();
                                        using var file = new FileStream(dest, FileMode.OpenOrCreate);
                                        bool stop = false;
                                        file.Seek(start, SeekOrigin.Begin);
                                        var buf = new byte[block];
                                        var readTotal = start;
                                        while (!stop) {
                                            var read = data.Read(buf, 0, buf.Length);
                                            if (realSize - readTotal < block) {
                                                stop = true;
                                                read = (int)(realSize - readTotal); // We get more than we expect
                                            }
                                            file.Write(buf, 0, read);
                                            task.Increment(read);
                                            readTotal += read;
                                        }
                                        AnsiConsole.MarkupLine($"[green]Firmware download done![/]");
                                    } else if (start > realSize) { // Overflow check
                                        throw new InvalidOperationException(
                                            "Overflow! File size is bigger than expected.");
                                    } else { // Skip downloading
                                        AnsiConsole.MarkupLine($"[yellow]Skipping firmware download...[/]");
                                    }

                                    // Tasks stuff
                                    task.Description = "[green]Downloading firmware[/]";
                                    task.StopTask();
                                    
                                    // Hash check task stuff
                                    if (o.DisableHashCheck) return;
                                    hash.StartTask();
                                    hash.IsIndeterminate(false);
                                    hash.Description = "[yellow]Verifying hash[/]";
                                    
                                    // Verify hash
                                    using var crc = new Crc32();
                                    using (Stream file = new FileStream(dest, FileMode.Open, FileAccess.Read)) {
                                        bool stop = false;
                                        var buf = new byte[block];
                                        long readTotal = 0;
                                        while (!stop) {
                                            int read = file.Read(buf, 0, buf.Length);
                                            if (realSize - readTotal < block) stop = true;
                                            if (stop) crc.TransformFinalBlock(buf, 0, read);
                                            else crc.TransformBlock(buf, 0, read, buf, 0);
                                            hash.Increment(read);
                                            readTotal += read;
                                        }
                                        AnsiConsole.MarkupLine($"[green]Firmware verification done![/]");
                                    }
                                    
                                    // Debug
                                    AnsiConsole.MarkupLine($"[yelloe]Got {BitConverter.ToString(crc.Hash)}/, expected {BitConverter.ToString(info.CrcChecksum)}[/]");

                                    // Hash check
                                    hash.Description = crc.Hash!.SequenceEqual(info.CrcChecksum)
                                        ? "[green]Hash is valid![/]"
                                        : "[red]Hash mismatch![/]";
                                    hash.StopTask();
                                });
                            break;
                        case Mode.Fetch:
                            AnsiConsole.MarkupLine($"[bold]Device:[/] {o.Model}/{o.Region}");
                            AnsiConsole.MarkupLine("[yellow]Connecting to FUS server...[/]");
                            var client = new FusClient();
                            var type = o.FactoryFirmware
                                ? FirmwareInfo.FirmwareType.Factory
                                : FirmwareInfo.FirmwareType.Home;
                            AnsiConsole.Progress()
                                .Columns(new TaskDescriptionColumn(), 
                                    new ProgressBarColumn(), 
                                    new PercentageColumn(),
                                    new ElapsedTimeColumn(), 
                                    new SpinnerColumn(Spinner.Known.Arc))
                                .Start(ctx => {
                                    var task = ctx.AddTask("[cyan]Fetching firmware list[/]").IsIndeterminate();
                                    var list = Fetcher.GetDeviceFirmwares(o.Model, o.Region);
                                    task.IsIndeterminate(false);
                                    task.Description = "[green]Fetching firmware information[/]";
                                    task.MaxValue = list.Old.Count + 1;
                                    var info = new List<FirmwareInfo> { client.GetFirmwareInformation(list.Latest.NormalizedVersion, o.Model, o.Region, type) };
                                    task.Increment(1);
                                    foreach (var fw in list.Old) {
                                        try {
                                            info.Add(client.GetFirmwareInformation(fw.NormalizedVersion, o.Model,
                                                o.Region, type));
                                        } catch {
                                            AnsiConsole.MarkupLine($"[yellow]Unable to fetch firmware for {fw.NormalizedVersion}[/]");
                                            info.Add(new FirmwareInfo { Version = fw.NormalizedVersion, OsVersion = "/UNKNOWN/", FileSize = 0 });
                                        }
                                        task.Increment(1);
                                    }
                                    task.StopTask();
                                    var table = new Table();
                                    table.AddColumn("Version");
                                    table.AddColumn("Android");
                                    table.AddColumn("Size");
                                    table.AddColumn("Latest");
                                    foreach (var fw in info)
                                        table.AddRow(fw.Version, fw.OsVersion, fw.FileSize.ToString(), 
                                            (fw.Version == list.Latest.NormalizedVersion).ToString());
                                    AnsiConsole.Write(table);
                                });
                            break;
                    }
                });
            } catch (Exception e) {
                AnsiConsole.MarkupLine($"[red]Exception occured:[/] {e.Message}");
                File.WriteAllText("stacktrace.log", e.ToString());
            }
        }
    }
}