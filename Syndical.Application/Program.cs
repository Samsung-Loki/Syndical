using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.ExceptionServices;
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
        private enum Mode { Download, Decrypt, Fetch }

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
            
            [Option('b', "bypass-file-check", Required = false, HelpText = "Bypasses file size check in Decryption mode")]
            public bool BypassFileCheck { get; set; }
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
                    // Goodbye old shit!
                    switch (o.Mode)
                    {
                        case Mode.Decrypt:
                            break;
                        case Mode.Download:
                            break;
                        case Mode.Fetch:
                            AnsiConsole.MarkupLine($"[bold]Device:[/] {o.Model}/{o.Region}");
                            AnsiConsole.MarkupLine("[yellow]Connecting to FUS server...[/]");
                            var client = new FusClient();
                            var type = o.FactoryFirmware
                                ? FirmwareInfo.FirmwareType.Factory
                                : FirmwareInfo.FirmwareType.Home;
                            AnsiConsole.Progress()
                                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(),
                                    new ElapsedTimeColumn(), new SpinnerColumn(Spinner.Known.Arc))
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