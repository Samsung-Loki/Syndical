using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
        
        [Flags]
        private enum Version { V4, V2 }
        
        /// <summary>
        /// Commandline options
        /// </summary>
        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
        private class Options
        {
            [Option('m', "mode", Required = true, HelpText = "Which mode I should use")]
            public Mode Mode { get; set; }
            
            [Option('V', "encrypt-version", SetName = "Decryption", Required = false, HelpText = "Encryption method version", Default = Version.V4)]
            public Version EncryptionVersion { get; set; }
            
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
        public static void Main(string[] args)
        {
            try {
                Parser.Default.ParseArguments<Options>(args).WithParsed(o =>
                {
                    Globals.Logger.Information("[Main] Connecting to FUS endpoint...");
                    var client = new FusClient();
                    switch (o.Mode)
                    {
                        case Mode.Decrypt:
                            Globals.Logger.Information("[Mode] Decryption mode selected");
                            break;
                        case Mode.Download:
                            Globals.Logger.Information("[Mode] Download mode selected");
                            if (string.IsNullOrEmpty(o.FirmwareVersion)) {
                                Globals.Logger.Fatal("[Main] Firmware version is required!");
                                return;
                            }

                            if (!Fetcher.DeviceExists(o.Model, o.Region)) {
                                Globals.Logger.Fatal("[Main] Device does not exist!");
                                return;
                            }

                            Globals.Logger.Information(o.FactoryFirmware
                                ? "[Main] Downloading factory firmware"
                                : "[Main] Downloading home firmware");

                            o.FirmwareVersion = o.FirmwareVersion.NormalizeVersion();
                            var info = client.GetBinaryInfo(o.FirmwareVersion, o.Model, o.Region, o.FactoryFirmware);
                            var status = int.Parse(info.DocumentElement
                                ?.SelectSingleNode("./FUSBody/Results/Status")?.InnerText!);
                            if (status != 200) {
                                Globals.Logger.Fatal("[Main] Firmware does not exist!");
                                return;
                            }
                            var size = long.Parse(info.DocumentElement
                                ?.SelectSingleNode("./FUSBody/Put/BINARY_BYTE_SIZE/Data")?.InnerText!);
                            var filename = info.DocumentElement?.SelectSingleNode("./FUSBody/Put/BINARY_NAME/Data")
                                ?.InnerText;
                            var path = info.DocumentElement?.SelectSingleNode("./FUSBody/Put/MODEL_PATH/Data")
                                ?.InnerText;
                            var filepath = string.IsNullOrEmpty(o.OutputFilename) ? filename : o.OutputFilename;
                            Globals.Logger.Information($"[Main] Firmware size: {size}");
                            Globals.Logger.Information($"[Main] Firmware filename: {filename}");
                            Globals.Logger.Information($"[Main] Firmware path: {path}");
                            if (File.Exists(filepath) && new FileInfo(filepath!).Length == size) {
                                Globals.Logger.Fatal("[Main] Firmware is already downloaded!");
                                return;
                            }
                            
                            client.SendBinaryInit(filename);
                            var res = client.DownloadFile(path + filename);
                            AnsiConsole.Progress()
                                .Start(ctx =>
                                {
                                    using var stream = new FileStream(filepath!, FileMode.OpenOrCreate, FileAccess.Write);
                                    using var data = res.GetResponseStream();
                                    var block = 65536;
                                    long blocksDone = 0;
                                    var realSize = double.Parse(res.Headers["Content-Length"]!);
                                    var blocksTotal = Math.Ceiling(realSize / block);
                                    var task = ctx.AddTask("[green]Downloading firmware[/]", maxValue: blocksTotal);
                                    bool stop = false;
                                    while (!stop) { 
                                        var buf = new byte[block]; 
                                        var count = (int) Math.Min(realSize - blocksDone * block, block); 
                                        if (count < block) stop = true; 
                                        data.Read(buf, 0, count); 
                                        stream.Write(buf, 0, count);
                                        blocksDone++;
                                        task.Increment(1);
                                       
                                    }
                                    stream.Flush();
                                    task.StopTask();
                                });

                            Globals.Logger.Information("[Main] Done!");
                            break;
                        case Mode.Fetch:
                            Globals.Logger.Information("[Mode] Fetch mode selected");
                            Globals.Logger.Information(
                                $"[Main] Latest firmware: {Fetcher.GetFirmwareList(o.Model, o.Region).DocumentElement?.SelectSingleNode("./firmware/version/latest")?.InnerText.NormalizeVersion()}");
                            break;
                    }
                });
            } catch (Exception e) {
                Globals.Logger.Fatal($"{e.Message}");
                File.WriteAllText("stacktrace.log", e.ToString());
            }
        }
    }
}