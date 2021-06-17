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
    // Used for Factorio authentication.
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
    // Contains info about the mod.
    struct Mod
    {
        public string name { get; set; }
        public bool enabled { get; set; }

        public Mod(string name, bool enabled)
        {
            this.name = name;
            this.enabled = enabled;
        }

        public static bool operator ==(Mod mod0, Mod mod1)
        {
            return mod0.name == mod1.name;
        }
        public static bool operator !=(Mod mod0, Mod mod1)
        {
            return mod0.name != mod1.name;
        }

        public static implicit operator Mod(string mod_id)
        {
            return new Mod(mod_id, false);
        }
    }
    // Only used for proper mod-list.json serialization/deserialization.
    struct Mods
    {
        public Mod[] mods { get; set; }

        public Mods(List<Mod> mods)
        {
            this.mods = mods.ToArray();
        }
    }

    static class EndStats
    {
        public static int errorsRecorded;
    }

    class Program
    {
        static List<Mod> mods;
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
                settings = SettingsHelper.ReadSettings();

                try
                {
                    settings.modsPath = settings.readWritePath.TrimEnd("player-data.json".ToCharArray()) + "mods/";
                }
                catch(NullReferenceException e)
                {
                    ConsoleHelper.ThrowError(ErrorType.NoPath, "readWritePath", skip: false);
                }
                mods = FetchMods(settings);

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

                ModLister.WriteModList(settings.modsPath, mods);

                Console.WriteLine($"Done with {EndStats.errorsRecorded} errors.");
                parsedArgs.toDownload.ToList().ForEach(x => Console.WriteLine(x));
                //if (EndStats.errorsRecorded != 0) Console.Write("Error codes: "); EndStats.errorCodes.ToList().ForEach(x => Console.Write($"{x} ")); Console.Write('\n');
            }
        }
        
        // Pieces of code thrown out of Main method to make it look prettier.
        static void Enable()
        {
            Mod[] mods_array = mods.ToArray();

            foreach (string mod_enable in parsedArgs.toEnable)
            {
                Console.WriteLine($"Enabling {mod_enable}...");

                for(int i=0;i<mods_array.Length;i++)
                {
                    if (mods_array[i] == mod_enable)
                        mods_array[i].enabled = true;
                }
            }

            mods = mods_array.ToList();
        }
        static void Disable()
        {
            Mod[] mods_array = mods.ToArray();

            foreach (string mod_enable in parsedArgs.toDisable)
            {
                Console.WriteLine($"Disabling {mod_enable}...");

                for (int i = 0; i < mods_array.Length; i++)
                {
                    if (mods_array[i] == mod_enable)
                        mods_array[i].enabled = false;
                }
            }

            mods = mods_array.ToList();
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

                    if(DownloadMod(modid, wc))
                        mods.Add(mod_download);
                }
            }
        }
        static void Remove()
        {
            string[] toRemove = parsedArgs.toRemove;

            foreach(string mod_remove in toRemove)
            {
                if(!mods.Contains(mod_remove))
                {
                    ConsoleHelper.ThrowError(ErrorType.LocalModDoesNotExist, mod_remove);
                    continue;
                }

                RemoveMod(mod_remove);
                mods.Remove(mod_remove);
            }
        }

        static bool DownloadMod(string mod_download, WebClient wc)
        {
            Console.WriteLine($"Downloading mod {mod_download}...");

            if(crdntls.username == "" || crdntls.token == "")
            {
                ConsoleHelper.ThrowError(ErrorType.NoService);
                return false;
            }

            if (mods.Contains(mod_download))
            {
                ConsoleHelper.ThrowError(ErrorType.LocalModExists, mod_download);
                return false;
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
                return false;
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

            return true;
        }
        static bool DownloadMod(string mod_download)
        {
            using(WebClient wc = new WebClient())
                return DownloadMod(mod_download, wc);
        }
        static void RemoveMod(string mod_remove)
        {
            Console.WriteLine($"Removing mod {mod_remove}...");
            File.Delete(settings.modsPath + mod_remove);
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
            SettingsHelper.WriteSettings(exePath, readWritePath);

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

        static List<Mod> FetchMods(Settings settings)
        {
            int n_fetched_ids = 0;

            Console.WriteLine("Fetching installed mods...");

            List<Mod> mods = new List<Mod>();

            mods.Add(new Mod("base", true)); // adding base mod to prevent oopsies

            string[] mod_zips = Directory.GetFiles(settings.modsPath, "*.zip");

            foreach(string modPath in mod_zips)
            {
                Console.WriteLine($"Fetching {Path.GetFileName(modPath)}...");

                ZipArchive mod_zip;

                try
                {
                    mod_zip = ZipFile.Open(modPath, ZipArchiveMode.Read, System.Text.Encoding.UTF8);
                }
                catch
                {
                    ConsoleHelper.ThrowError(ErrorType.BadMod, modPath);
                    continue;
                }
                

                foreach(ZipArchiveEntry entry in mod_zip.Entries)
                {
                    if(entry.Name == "info.json")
                    {
                        JsonDocument info = JsonDocument.Parse(entry.Open());
                        mods.Add(info.RootElement.GetProperty("name").GetString());

                        n_fetched_ids++;

                        break;
                    }
                }

                mod_zip.Dispose();
            }

            Console.WriteLine($"Done. Total fetched mods: {n_fetched_ids}");

            return ModLister.ReadModList(settings.modsPath, mods.ToArray());
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
            Console.WriteLine("Usage: factoriomodtool [--setup] [-h] [-e MOD_ID] [-d MOD_ID] [--redownload MOD_ID] [-i MOD_ID] [-r MOD_ID]\nOptions:\n -h, --help\tPrint this screen.\n\n --setup\tLaunch setup tool.\n\n -e, --enable MOD_ID\tEnable mod with mod id MOD_ID.\n -d, --disable MOD_ID\tDisable mod with mod id MOD_ID.\n --redownload MOD_ID\tRedownload mod with mod id MOD_ID.\n -i, --install, --download MOD_ID\tDownload a mod from Factorio mod portal\n\t\t\t\t\t\twith given mod id or mod portal URI.\n\t\t\t\t\t\t(won't work if you specify a non mod-portal URI)\nExample:\n --download Krastorio2\n --download https://mods.factorio.com/mod/Krastorio2\n\n -r, --uninstall, --remove MOD_ID\tRemove downloaded MOD_ID.");
            //Console.WriteLine("Usage: factoriomodtool [--setup] [-h] [-s] [-f] [-c] [-D] [-e MOD_ID] [-d MOD_ID] [--redownload MOD_ID] [-i MOD_ID] [-r MOD_ID]\nOptions:\n -h, --help\tPrint this screen.\n\n --setup\tLaunch setup tool.\n\n -s, --silent\tDo not print progress into console.\n\n -e, --enable MOD_ID\tEnable mod with mod id MOD_ID.\n -d, --disable MOD_ID\tDisable mod with mod id MOD_ID.\n --redownload MOD_ID\tRedownload mod with mod id MOD_ID.\n -i, --install, --download MOD_ID\tDownload a mod from Factorio mod portal\n\t\t\t\t\t\twith given mod id or mod portal URI.\n\t\t\t\t\t\t(won't work if you specify a non mod-portal URI)\nExample:\n --download Krastorio2\n --download https://mods.factorio.com/mod/Krastorio2\n\n -r, --uninstall, --remove MOD_ID\tRemove downloaded MOD_ID.\n\n -c, --no-compatibility\tDo not test for compatibility of enabled mods.\n -D, --no-dependency\tDo not download dependency mods.\n -f, --force-checks\tForce compatibility and/or dependency checks even if mod list isn't changed.\n");
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
                    Console.WriteLine($"Download request was called for mod {msg0}, but it is already fetched.");
                    break;
                case ErrorType.LocalModDoesNotExist:
                    Console.WriteLine($"An operation was requested for mod {msg0}, but it wasn't already fetched.");
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
                case ErrorType.MissingModList:
                    Console.WriteLine($"Required file mod-list.json is missing. Cannot fetch mods.");
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
    static class SettingsHelper
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
    // A class that handles mod-list.json stuff.
    static class ModLister
    {
        public static List<Mod> ReadModList(string modsPath, Mod[] fetched_mod_zips)
        {
            FileStream mod_list;
            try
            {
                mod_list = File.Open(modsPath + "mod-list.json", FileMode.Open, FileAccess.Read);
            }
            catch (FileNotFoundException e)
            {
                ConsoleHelper.ThrowError(ErrorType.MissingModList, skip: false);
                return null;
            }
            StreamReader mod_list_stream = new StreamReader(mod_list);

            //StreamReader test = new StreamReader(mod_list);
            //Console.WriteLine(test.ReadToEnd());
            //test.Close();

            Mod[] mod_list_fetch = JsonSerializer.Deserialize<Mods>(mod_list_stream.ReadToEnd()).mods;

            mod_list_stream.Close();
            mod_list.Close();

            for (int i = 0; i < fetched_mod_zips.Length; i++)
            {
                foreach (Mod mod_in_list_fetch in mod_list_fetch)
                {
                    if (fetched_mod_zips[i] == mod_in_list_fetch)
                    {
                        fetched_mod_zips[i].enabled = mod_in_list_fetch.enabled;
                    }
                }
            }

            return fetched_mod_zips.ToList();
        }
        public static void WriteModList(string modsPath, List<Mod> mods)
        {
            Console.WriteLine("Writing mod-list.json...");

            FileStream mod_list = File.Open(modsPath + "mod-list.json", FileMode.Create, FileAccess.Write);
            StreamWriter mod_list_stream = new StreamWriter(mod_list);

            mod_list_stream.Write(JsonSerializer.Serialize(new Mods(mods), new JsonSerializerOptions() { WriteIndented = true }));

            mod_list_stream.Close();
            mod_list.Close();

            Console.WriteLine("Done writing.");
        }
    }
    
    // This tool should catch all possible exceptions and throw it's own kind of error.
    enum ErrorType
    {
        NoError,
        WrongOption,
        NoArgument,
        NoSuchMod,
        NoPath,
        NoSetup,
        LocalModExists,
        LocalModDoesNotExist,
        NoService,
        BadMod,
        WrongPath,
        MissingModList,
        NotImplemented
    }
}
