using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;

namespace FactorioModTool
{
    struct Settings
    {
        public const string settings_path = "./factoriomodtool.settings"; // Factorio Mod Tool settings path. Created after using factoriomodtool --setup.

        public string exePath; // Factorio executable path. This is also a data path, which is
                               // TODO: used to determine Factorio version.
        public string readWritePath; // Factorio read/write data path, which is a path to: player-data.json, mods and saves.

        public string modsPath; // Mods path, evaluated at runtime.
        public string savesPath; // Saves path, evaluated at runtime.

        public Settings(string exePath, string readWritePath)
        {
            this.exePath = exePath;
            this.readWritePath = readWritePath;

            modsPath = "";
            savesPath = "";
        }
    }
    struct ArgsContainer
    {
        public int n_args; // Number of parsed options. Does not include option arguments.

        public string[] toEnable; // Contains IDs of mods to be enabled.
        public string[] toDisable; // Contains IDs of mods to be disabled.
        public string[] toRemove; // Contains IDs of mods to be removed.

        public string[] toDownload; // Contains IDs of mods to download.

        public bool runSetupTool; // Specifies if setup run should be run.

        public bool help; // Force help screen print.

        public bool noDependencyCheck; // Specifies if dependency mods should not be downloaded.
        public bool noCompatibilityCheck; // Specifies if no compatibility check between enabled mods should be run.
        public bool forceChecks; // Force compatibility and/or dependency checks.

        public bool silent; // Specifies if there should be no console output.
    }   
    struct ServiceCrdntls
    {
        public string username;
        public string token;

        public ServiceCrdntls(string username, string token)
        {
            this.username = username;
            this.token = token;
        }
    }

    static class EndStats
    {
        public static int errorsRecorded;
    }

    class Program
    {
        static List<string> modIds;
        static Settings settings;
        static ArgsContainer parsedArgs;
        static ServiceCrdntls crdntls;

        static void Main(string[] args)
        {
            parsedArgs = ParseArgs(args);

            if (parsedArgs.n_args == 0 || parsedArgs.help)
            {
                ConsoleHelper.PrintUsage();
            }

            if(parsedArgs.runSetupTool)
            {
                SetupTool();
            }

            if(parsedArgs.n_args != 0)
            {
                settings = FileHelper.ReadSettings();

                try
                {
                    settings.modsPath = settings.readWritePath.TrimEnd("player-data.json".ToCharArray()) + "mods";
                }
                catch(NullReferenceException e)
                {
                    ConsoleHelper.ThrowError(ErrorType.NoPath, "readWritePath", skip: false);
                }
                modIds = FetchIds(settings);

                if (parsedArgs.toRemove.Length != 0)
                {
                    Remove();
                }
                if (parsedArgs.toDownload.Length != 0)
                {
                    crdntls = GetCrdntls();
                    Download();
                }
                if (parsedArgs.toDisable.Length != 0)
                {
                    Disable();
                }
                if (parsedArgs.toEnable.Length != 0)
                {
                    Enable();
                }

                Console.WriteLine($"Done with {EndStats.errorsRecorded} errors.");
                parsedArgs.toDownload.ToList().ForEach(x => Console.WriteLine(x));
                //if (EndStats.errorsRecorded != 0) Console.Write("Error codes: "); EndStats.errorCodes.ToList().ForEach(x => Console.Write($"{x} ")); Console.Write('\n');
            }
        }
        
        static void Enable()
        {

        }
        static void Disable()
        {

        }
        static void Download()
        {
            string[] toDownload = parsedArgs.toDownload;

            using (WebClient wc = new WebClient())
            {
                foreach (string mod_download in toDownload)
                {
                    string modid = mod_download;
                    if (modid.StartsWith("https://mods.factorio.com/mod/"))
                        modid = modid.TrimStart("https://mods.factorio.com/mod/".ToCharArray());

                    DownloadMod(modid, wc);
                }
            }
        }
        static void Remove()
        {
            string[] toRemove = parsedArgs.toRemove;

            foreach(string mod_remove in toRemove)
            {
                Console.WriteLine($"Removing mod {mod_remove}...");

                if(!modIds.Contains(mod_remove))
                {
                    ConsoleHelper.ThrowError(ErrorType.LocalModDoesNotExist, mod_remove);
                    continue;
                }

                //File.de

                modIds.Remove(mod_remove);
            }
        }

        static void DownloadMod(string mod_download, WebClient wc)
        {
            Console.WriteLine($"Downloading mod {mod_download}...");

            if(crdntls.username == "" || crdntls.token == "")
            {
                ConsoleHelper.ThrowError(ErrorType.NoService);
                return;
            }

            if (modIds.Contains(mod_download))
            {
                ConsoleHelper.ThrowError(ErrorType.LocalModExists, mod_download);
                return;
            }

            WebRequest request = WebRequest.Create($"https://mods.factorio.com/api/mods/{mod_download}/full");
            WebResponse response;

            try
            {
                response = request.GetResponse();
            }
            catch (WebException e)
            {
                ConsoleHelper.ThrowError(ErrorType.NoSuchMod, mod_download);
                return;
            }

            JsonDocument api_json = JsonDocument.Parse(response.GetResponseStream());

            response.Close();

            JsonElement release_to_download = api_json.RootElement.GetProperty("releases").EnumerateArray().Last();

            string download_url = release_to_download.GetProperty("download_url").GetString();
            string zip_name = settings.modsPath + "/" + release_to_download.GetProperty("file_name").GetString();

            ManualResetEvent ma = new ManualResetEvent(false);

            long prevBytes = -1;

            wc.DownloadProgressChanged += ProgressChanged;
            wc.DownloadFileCompleted += DownloadCompleted;
            ma.Reset();
            wc.DownloadFileAsync(new Uri($"https://mods.factorio.com/{download_url}?username={crdntls.username}&token={crdntls.token}"), zip_name);
            ma.WaitOne();

            void ProgressChanged(object sender, DownloadProgressChangedEventArgs args)
            {
                long curBytes = args.BytesReceived;
                int curPercentage = args.ProgressPercentage;

                if (prevBytes / 1024 / 1024 != curBytes / 1024 / 1024)
                {
                    Console.WriteLine($"Downloaded {args.BytesReceived / 1024 / 1024} MB / {args.TotalBytesToReceive / 1024 / 1024} MB; {args.ProgressPercentage}%");
                }

                prevBytes = curBytes;
            }

            void DownloadCompleted(object sender, AsyncCompletedEventArgs e)
            {
                ma.Set();
            }

            Console.WriteLine($"Finished downloading {mod_download}, {prevBytes / 1024 / 1024} MB.");
        }
        
        static void DownloadMod(string mod_download)
        {
            using(WebClient wc = new WebClient())
                DownloadMod(mod_download, wc);
        }

        static ServiceCrdntls GetCrdntls()
        {
            string readWritePath = settings.readWritePath;

            FileStream player_data_file = File.Open(readWritePath, FileMode.Open, FileAccess.Read);
            JsonDocument player_data = JsonDocument.Parse(player_data_file);
            player_data_file.Close();

            try
            {
                return new ServiceCrdntls(player_data.RootElement.GetProperty("service-username").GetString(), player_data.RootElement.GetProperty("service-token").GetString());
            }
            catch(KeyNotFoundException e)
            {
                ConsoleHelper.ThrowError(ErrorType.NoService, skip: false);
                return new ServiceCrdntls();
            }
        }

        // Parses args, return whole container of juicy args stuff.
        // (See ArgsContainer struct for more info)
        static ArgsContainer ParseArgs(string[] args)
        {
            int n_args = 0;

            List<string> toEnable = new List<string>();
            List<string> toDisable = new List<string>();
            List<string> toDownload = new List<string>();
            List<string> toRemove = new List<string>();

            ArgsContainer _parsedArgs = new ArgsContainer();

            for (int n = 0; n < args.Length; n++)
            {
                try
                {
                    switch (args[n])
                    {
                        case "--setup":
                            _parsedArgs.runSetupTool = true;
                            break;
                        case "-h": case "--help":
                            _parsedArgs.help = true;
                            break;
                        case "-s": case "--silent":
                            _parsedArgs.silent = true;
                            break;
                        case "-f": case "--force-checks":
                            _parsedArgs.forceChecks = true;
                            break;
                        case "-c": case "--no-compatibility":
                            _parsedArgs.noCompatibilityCheck = true;
                            break;
                        case "-D": case "--no-dependency":
                            _parsedArgs.noDependencyCheck = true;
                            break;
                        case "-e": case "--enable":
                            toEnable.Add(args[++n]);
                            break;
                        case "-d": case "--disable":
                            toDisable.Add(args[++n]);
                            break;
                        case "-i": case "--install": case "--download":
                            toDownload.Add(args[++n]);
                            break;
                        case "-r": case "--uninstall": case "--remove":
                            toRemove.Add(args[++n]);
                            break;
                        case "--redownload":
                            toRemove.Add(args[++n]);
                            toDownload.Add(args[n]);
                            break;
                        default:
                            ConsoleHelper.ThrowError(ErrorType.WrongOption, args[n]);
                            n_args--;
                            break;
                    }
                }
                catch(IndexOutOfRangeException e)
                {
                    ConsoleHelper.ThrowError(ErrorType.NoArgument, args[--n]);
                    n_args--;
                }

                n_args++;
            }

            _parsedArgs.n_args = n_args;

            _parsedArgs.toEnable = toEnable.ToArray();
            _parsedArgs.toDisable = toDisable.ToArray();
            _parsedArgs.toDownload = toDownload.ToArray();
            _parsedArgs.toRemove = toRemove.ToArray();

            return _parsedArgs;
        }
        
        // Setup tool to create path settings.
        static void SetupTool()
        {
            Console.Clear();

            Console.WriteLine("Factorio Mod Tool - Setup Tool\n");

            string exePath = "", readWritePath = "";
            
            while(exePath == "")
            {
                exePath = GetExePath();
                Console.Clear();
            }

            while(readWritePath == "")
            {
                readWritePath = GetReadWritePath();
                Console.Clear();
            }

            Console.WriteLine("Writing paths to a settings file...");
            FileHelper.WriteSettings(exePath, readWritePath);

            string GetExePath()
            {
                Console.WriteLine("Please, specify Factorio executable path (path to factorio.exe, ends with Factorio/bin/x64/factorio.exe)");

                string _exePath = Console.ReadLine();

                if (!File.Exists(_exePath) || Path.GetFileName(_exePath) != "factorio.exe")
                {
                    ConsoleHelper.ThrowError(ErrorType.WrongPath, _exePath, "factorio.exe");
                    return "";
                }

                return _exePath;
            }
            string GetReadWritePath()
            {
                Console.WriteLine("Please, specify Factorio read write path (path to player-data.json)");
                Console.WriteLine("Most commonly it is located in %Appdata%/Factorio/player-data.json or in Factorio root folder.");

                string _readWritePath = Console.ReadLine();

                if (!File.Exists(_readWritePath) || Path.GetFileName(_readWritePath) != "player-data.json")
                {
                    ConsoleHelper.ThrowError(ErrorType.WrongPath, _readWritePath, "player-data.json");
                    return "";
                }

                return _readWritePath;
            }
        }

        static List<string> FetchIds(Settings settings)
        {
            int n_fetched_ids = 0;

            Console.WriteLine("Fetching installed mod ids...");

            List<string> modIds = new List<string>();

            string[] mods = Directory.GetFiles(settings.modsPath, "*.zip");

            foreach(string modPath in mods)
            {
                Console.WriteLine($"Fetching {Path.GetFileName(modPath)}...");

                ZipArchive mod;

                try
                {
                    mod = ZipFile.Open(modPath, ZipArchiveMode.Read, System.Text.Encoding.UTF8);
                }
                catch
                {
                    ConsoleHelper.ThrowError(ErrorType.BadMod, modPath);
                    continue;
                }
                

                foreach(ZipArchiveEntry entry in mod.Entries)
                {
                    if(entry.Name == "info.json")
                    {
                        JsonDocument info = JsonDocument.Parse(entry.Open());
                        modIds.Add(info.RootElement.GetProperty("name").GetString());

                        n_fetched_ids++;

                        break;
                    }
                }

                mod.Dispose();
            }

            Console.WriteLine($"Done. Total fetched mods: {n_fetched_ids}");

            return modIds;
        }
    }

    // Various stuff to help with console I/O.
    static class ConsoleHelper
    {
        // Uh oh
        // There must have been other (BETTER) ways of doing that, I must just happen to be very ignorant lol
        // But I mean it works, soo....
        public static void PrintUsage()
        {
            Console.WriteLine("Usage: factoriomodtool [--setup] [-h] [-s] [-f] [-c] [-D] [-e MOD_ID] [-d MOD_ID] [--redownload MOD_ID] [-i MOD_ID] [-r MOD_ID]\nOptions:\n -h, --help\tPrint this screen.\n\n --setup\tLaunch setup tool.\n\n -s, --silent\tDo not print progress into console.\n\n -e, --enable MOD_ID\tEnable mod with mod id MOD_ID.\n -d, --disable MOD_ID\tDisable mod with mod id MOD_ID.\n --redownload MOD_ID\tRedownload mod with mod id MOD_ID.\n -i, --install, --download MOD_ID\tDownload a mod from Factorio mod portal\n\t\t\t\t\t\twith given mod id or mod portal URI.\n\t\t\t\t\t\t(won't work if you specify a non mod-portal URI)\nExample:\n --download Krastorio2\n --download https://mods.factorio.com/mod/Krastorio2\n\n -r, --uninstall, --remove MOD_ID\tRemove downloaded MOD_ID.\n\n -c, --no-compatibility\tDo not test for compatibility of enabled mods.\n -D, --no-dependency\tDo not download dependency mods.\n -f, --force-checks\tForce compatibility and/or dependency checks even if mod list isn't changed.\n");
        }

        // A bit obfuscated way of handling errors.
        public static void ThrowError(ErrorType error = ErrorType.NoError, string msg0 = "", string msg1 = "", bool skip = true)
        {
            Console.WriteLine($"ERROR: {error}");
            if (error != ErrorType.NoError) EndStats.errorsRecorded++;
            switch(error)
            {
                case ErrorType.WrongOption:
                    Console.WriteLine($"There is no such option as {msg0}.");
                    break;
                case ErrorType.NoArgument:
                    Console.WriteLine($"Argument expected for option {msg0}, got none at the end of option list.");
                    break;
                case ErrorType.NoSuchMod:
                    Console.WriteLine($"There is no such mod as {msg0}.");
                    break;
                case ErrorType.NoPath:
                    Console.WriteLine($"Required path {msg0} is not specified. Please, launch setup tool: factoriomodtool --setup");
                    break;
                case ErrorType.NoSetup:
                    Console.WriteLine($"Settings file is missing. Please, launch setup tool: factoriomodtool --setup");
                    break;
                case ErrorType.WrongPath:
                    Console.WriteLine($"Specified path {msg0} does not end with expected {msg1}.");
                    break;
                case ErrorType.LocalModExists:
                    Console.WriteLine($"Download request was called for mod {msg0}, but it is already downloaded.");
                    break;
                case ErrorType.LocalModDoesNotExist:
                    Console.WriteLine($"Enable/disable/remove request was called for mod {msg0}, but it wasn't already downloaded.");
                    break;
                case ErrorType.NoService:
                    Console.WriteLine($"Service username and token are absent. Please, open Factorio and log in into your Factorio account.");
                    break;
                case ErrorType.BadMod:
                    Console.WriteLine($"Bad/corrupted mod {msg0} detected.");
                    break;
                case ErrorType.NotImplemented:
                    Console.WriteLine($"Functionality {msg0} not implemented yet.");
                    break;
                default:
                    Console.WriteLine($"This seems to be a wrong error call. Nevermind.\nError parameters: {msg0}; {msg1}; {skip}");
                    break;
            }

            if(skip)
            {
                Console.WriteLine("Skipping.");
            }
            else
            {
                Console.WriteLine("Fatal error. Exiting.");
                Console.WriteLine($"{EndStats.errorsRecorded} errors recorded.");
                Environment.Exit((int)error);
            }
        }
    }
    // Various stuff to help with file I/O.
    static class FileHelper
    {
        public static void WriteSettings(Settings settings)
        {
            FileStream file_settings = File.Open(Settings.settings_path, FileMode.Create, FileAccess.Write);
            StreamWriter file_stream_settings = new StreamWriter(file_settings);

            file_stream_settings.Write($"exePath\n{settings.exePath}\nreadWritePath\n{settings.readWritePath}");

            file_stream_settings.Close();
            file_settings.Close();
        }
        public static void WriteSettings(string exePath, string readWritePath)
        {
            WriteSettings(new Settings(exePath, readWritePath));
        }

        public static Settings ReadSettings()
        {
            Settings settings = new Settings();

            FileStream file_settings;
            StreamReader file_stream_settings;

            try
            {
                file_settings = File.Open(Settings.settings_path, FileMode.Open, FileAccess.Read);
                file_stream_settings = new StreamReader(file_settings);
            }
            catch (FileNotFoundException e)
            {
                ConsoleHelper.ThrowError(ErrorType.NoSetup, "", "", false);
                return new Settings();
            }

            while (!file_stream_settings.EndOfStream)
            {

                switch (file_stream_settings.ReadLine())
                {
                    case "exePath":
                        settings.exePath = file_stream_settings.ReadLine();
                        break;
                    case "readWritePath":
                        settings.readWritePath = file_stream_settings.ReadLine();
                        break;
                }
            }

            file_stream_settings.Close();
            file_settings.Close();

            return settings;
        }
    }

    enum ErrorType
    {
        NoError,
        WrongOption,
        NoArgument,
        NoSuchURI,
        NoSuchMod,
        NoPath,
        NoSetup,
        LocalModExists,
        LocalModDoesNotExist,
        NoService,
        BadMod,
        WrongPath,
        NotImplemented
    }
}
