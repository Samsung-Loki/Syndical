using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
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
            
            [Option('V', "encrypt-version", SetName = "Decryption", Required = false, 
                HelpText = "Encryption method version", Default = FirmwareInfo.DecryptVersion.V4)]
            public FirmwareInfo.DecryptVersion EncryptionVersion { get; set; }
            
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
            
            [Option('f', "factory", Required = false, HelpText = "Download factory firmware (BINARY_NATURE = 1)")]
            public bool FactoryFirmware { get; set; }
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
                                    var task = ctx.AddTask("[yellow]Downloading firmware[/]", maxValue: realSize);
                                    using var srcStream = new FileStream(srcDecrypt, FileMode.Open, FileAccess.Read);
                                    using var destStream = new FileStream(destDecrypt, FileMode.OpenOrCreate, FileAccess.Write);
                                    using var rj = new RijndaelManaged();
                                    rj.Mode = CipherMode.ECB;
                                    rj.Padding = PaddingMode.PKCS7;
                                    using var decryptor = new CryptoStream(srcStream, rj.CreateDecryptor(), CryptoStreamMode.Read);
                                    bool stop = false;
                                    var buf = new byte[block];
                                    long readTotal = 0;
                                    while (!stop)
                                    {
                                        int read = decryptor.Read(buf, 0, buf.Length);
                                        if (realSize - readTotal < block) stop = true;
                                        destStream.Write(buf, 0, read);
                                        task.Increment(read);
                                        readTotal += read;
                                    }

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

                            AnsiConsole.Progress()
                                .Columns(new TaskDescriptionColumn(),
                                    new ProgressBarColumn(),
                                    new PercentageColumn(),
                                    new DownloadedColumn(),
                                    new TransferSpeedColumn(),
                                    new RemainingTimeColumn(),
                                    new ElapsedTimeColumn())
                                .Start(ctx => {
                                    var res = clientDownload.DownloadFirmware(info, start);
                                    var block = 800;
                                    var realSize = long.Parse(res.Headers["Content-Length"]!);
                                    var toWrite = realSize - start;
                                    AnsiConsole.MarkupLine($"[yellow]Download from {start} to {realSize}[/]");
                                    if (realSize != info.FileSize)
                                        AnsiConsole.MarkupLine(
                                            $"[yellow]Content-Length is different than reported size: {realSize}/{info.FileSize}[/]");
                                    var task = ctx.AddTask("[yellow]Downloading firmware[/]", maxValue: realSize);
                                    var hash = ctx.AddTask("[cyan]Verifying hash[/]", false, realSize)
                                        .IsIndeterminate();
                                    task.Increment(start);
                                    if (start < realSize) {
                                        using var data = res.GetResponseStream();
                                        using var file = new FileStream(dest, FileMode.OpenOrCreate);
                                        bool stop = false;
                                        file.Seek(start, SeekOrigin.Begin);
                                        var buf = new byte[block];
                                        long readTotal = 0;
                                        while (!stop)
                                        {
                                            int read = data.Read(buf, 0, buf.Length);
                                            if (toWrite - readTotal < block) stop = true;
                                            file.Write(buf, 0, read);
                                            task.Increment(read);
                                            readTotal += read;
                                        }
                                    } else if (start > realSize) {
                                        throw new InvalidOperationException(
                                            "Overflow! File size is bigger than expected.");
                                    } else {
                                        AnsiConsole.MarkupLine($"[yellow]Skipping firmware download...[/]");
                                    }

                                    task.Description = "[green]Downloading firmware[/]";
                                    task.StopTask();
                                    hash.StartTask();
                                    hash.IsIndeterminate(false);
                                    hash.Description = "[yellow]Verifying hash[/]";
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
                                    }

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