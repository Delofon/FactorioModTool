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
        public bool disableAll; // Disable all mods lol
        public string[] toRemove; // Contains IDs of mods to be removed.

        public string[] toDownload; // Contains IDs of mods to download.

        public bool runSetupTool; // Specifies if setup run should be run.

        public bool help; // Force help screen print.

        public bool noDependencyCheck; // Specifies if dependency mods should not be downloaded.
        public bool noCompatibilityCheck; // Specifies if no compatibility check between enabled mods should be run.
        public bool forceChecks; // Force compatibility and/or dependency checks.

        public bool silent; // Specifies if there should be no console output.

        public bool getMods;
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
    // Contains info about the mod version.
    struct SemVer
    {
        public static SemVer Null
        {
            get
            {
                return new SemVer() { major = 0, minor = 0, patch = 0 };
            }
        }
        public static SemVer Initial
        {
            get
            {
                return new SemVer() { major = 0, minor = 1, patch = 0 };
            }
        }

        public int major, minor, patch;

        public static implicit operator SemVer(string version)
        {
            string[] strs = version.Split('.');

            try
            {
                int[] ints = new int[3];

                for(int i = 0; i < 3; i++)
                {
                    ints[i] = Int32.Parse(strs[i]);
                }

                return new SemVer() { major = ints[0], minor = ints[1], patch = ints[2] };
            }
            catch
            {
                return Null;
            }
        }

        public static bool operator ==(SemVer left, SemVer right)
        {
            if (left.major == right.major && left.minor == right.minor && left.patch == right.patch) return true;
            return false;
        }
        public static bool operator !=(SemVer left, SemVer right)
        {
            return !(left == right);
        }
        public static bool operator >(SemVer left, SemVer right)
        {
            if (left.major > right.major) return true;
            if (left.major < right.major) return false;

            if (left.minor > right.minor) return true;
            if (left.minor < right.minor) return false;

            if (left.patch > right.patch) return true;
            if (left.patch < right.patch) return false;

            return false;
        }
        public static bool operator <(SemVer left, SemVer right)
        {
            return !(left > right) ^ (left == right);
        }
        public static bool operator >=(SemVer left, SemVer right)
        {
            return !(left < right);
        }
        public static bool operator <=(SemVer left, SemVer right)
        {
            return !(left > right);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            SemVer? ver = obj as SemVer?;
            if (ver.HasValue != true) return false;
            else return Equals(ver.Value);
        }
        public bool Equals(SemVer ver)
        {
            return this == ver;
        }

        public override int GetHashCode()
        {
            return major.GetHashCode() << 22 | minor.GetHashCode() << 12 | patch.GetHashCode();
        }
    }
    // Contains info about the mod.
    class Mod
    {
        public string name { get; set; }
        public bool enabled { get; set; }

        public SemVer version;

        public string zipName;

        public Mod(string name, bool enabled)
        {
            this.name = name;
            this.enabled = enabled;
        }

        public static explicit operator Mod(string mod_id)
        {
            return new Mod(mod_id, false);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            Mod mod = obj as Mod;
            if (mod == null) return false;
            else return Equals(mod);
        }
        public bool Equals(Mod mod)
        {
            if (mod == null) return false;
            return name == mod.name;
        }

        public override string ToString()
        {
            return name;
        }
    }

    static class EndStats
    {
        public static int errorsRecorded;
        // add any interesting statistics here
    }

    class Program
    {
        static List<Mod> mods;
        static Settings settings;
        static ArgsContainer parsedArgs;
        static ServiceCrdntls crdntls;
		
		static bool reFetch = false;

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
				
				if(reFetch)
					mods = FetchMods(settings);

                string[] toDisable;

                #region ugly
                if (parsedArgs.disableAll)
                {
                    toDisable = new string[mods.Count];
                    for (int i = 0; i < mods.Count; i++)
                    {
                        toDisable[i] = mods[i].name;
                    }
                }
                else
                {
                    toDisable = parsedArgs.toDisable;
                }
                #endregion

                if (toDisable.Length != 0)
                {
                    Disable(toDisable);
                }
                if (parsedArgs.toEnable.Length != 0)
                {
                    Enable();
                }

                if(parsedArgs.getMods)
                {
                    Console.Write("Enabled mods: ");
                    mods.ForEach(x => { if (x.enabled) Console.Write($"{x.name} "); });
                    Console.WriteLine();
                }

                ModLister.WriteModList(settings.modsPath, mods);

                Console.WriteLine($"Done with {EndStats.errorsRecorded} errors.");
                //Console.WriteLine(mods.Contains("bobores"));
                //parsedArgs.toDownload.ToList().ForEach(x => Console.WriteLine(x));
                //if (EndStats.errorsRecorded != 0) Console.Write("Error codes: "); EndStats.errorCodes.ToList().ForEach(x => Console.Write($"{x} ")); Console.Write('\n');
            }
        }
        
        // Pieces of code thrown out of Main method to make it look prettier.
        static void Enable()
        {
            foreach (string mod_enable in parsedArgs.toEnable)
            {
                Console.WriteLine($"Enabling {mod_enable}...");

                ChangeModEnabledState(mod_enable, true);
            }
        }
        static void Disable(string[] toDisable)
        {
            foreach (string mod_disable in toDisable)
            {
                Console.WriteLine($"Disabling {mod_disable}...");

                ChangeModEnabledState(mod_disable, false);
            }
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
			
			reFetch = true;
        }
        static void Remove()
        {
            string[] toRemove = parsedArgs.toRemove;

            foreach(string mod_remove in toRemove)
            {
                Mod mod;

                if(!mods.Contains((Mod)mod_remove))
                {
                    ConsoleHelper.ThrowError(ErrorType.LocalModDoesNotExist, mod_remove);
                    continue;
                }

                mod = mods.Find(x => x.name == mod_remove); // TODO: fix this uglyness

                RemoveMod(mod);
            }
			
			reFetch = true;
        }

        static void ChangeModEnabledState(string mod, bool newState)
        {
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i].name == mod)
                {
                    mods[i].enabled = newState;
                    break;
                }
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

            if (mods.Contains((Mod)mod_download))
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
        static void RemoveMod(Mod mod_remove)
        {
            Console.WriteLine($"Removing mod {mod_remove.name}...");
            File.Delete(mod_remove.zipName);
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
            List<int> ignoredErrors = new List<int>();

            #region PreParseArgs
            for (int n = 0; n < args.Length; n++)
            {
                switch (args[n])
                {
                    case "--ignore-error":
                        try
                        {
                            ignoredErrors.Add(int.Parse(args[++n]));
                        }
                        catch (FormatException e)
                        {
                            ConsoleHelper.ThrowError(ErrorType.BadArgument, "--ignore-error", "Int32");
                            n_args--;
                        }
                        break;
                    case "--crybaby":
                        ConsoleHelper.cryBabyMode = true;
                        break;
                }

                n_args++;
            }
            #endregion

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
                                ConsoleHelper.ThrowError(ErrorType.NotImplemented, "-s");
                            break;
                        case "-f": case "--force-checks":
                            _parsedArgs.forceChecks = true;
                                ConsoleHelper.ThrowError(ErrorType.NotImplemented, "-f");
                            break;
                        case "-c": case "--no-compatibility":
                            _parsedArgs.noCompatibilityCheck = true;
                                ConsoleHelper.ThrowError(ErrorType.NotImplemented, "-c");
                            break;
                        case "-D": case "--no-dependency":
                            _parsedArgs.noDependencyCheck = true;
                                ConsoleHelper.ThrowError(ErrorType.NotImplemented, "-D");
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
                        case "--disable-all":
                            _parsedArgs.disableAll = true;
                            break;
                        case "--get-mods":
                            _parsedArgs.getMods = true;
                            break;
                        case "--ignore-error": // TODO: fix this uglyness
							n++;
                            n_args--;
                            break;
                        case "--crybaby":
                            n_args--;
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

            ConsoleHelper.ignoredErrors = ignoredErrors.ToArray();

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
                        Mod mod;
                        mod = (Mod)info.RootElement.GetProperty("name").GetString();
                        mod.zipName = modPath;

                        mods.Add(mod);

                        n_fetched_ids++;

                        break;
                    }
                }

                mod_zip.Dispose();
            }

            //mods.ForEach(x => Console.WriteLine(x));

            Console.WriteLine($"Done. Total fetched mods: {n_fetched_ids}");

            return ModLister.ReadModList(settings.modsPath, mods.ToArray());
        }
    }
    
    // This tool should catch all possible exceptions and throw it's own kind of error.
    enum ErrorType
    {
        NoError,
        WrongOption,
        BadArgument,
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
