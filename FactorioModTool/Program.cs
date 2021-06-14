using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;

namespace FactorioModTool
{
    struct Settings
    {
        public const string settings_path = "./factoriomodtool.settings"; // Factorio Mod Tool settings path. Created after using factoriomodtool --setup.

        public string exePath; // Factorio executable path. This is also a data path, which is used to determine Factorio version.
        public string readWritePath; // Factorio read/write data path, which is a path to: player-data.json, mods and saves.

        public Settings(string exePath, string readWritePath)
        {
            this.exePath = exePath;
            this.readWritePath = readWritePath;
        }
    }
    struct ArgsContainer
    {
        public int n_args; // Number of parsed options. Does not include option arguments.

        public string[] toEnable; // Contains IDs of mods to be enabled.
        public string[] toDisable; // Contains IDs of mods to be disabled.
        public string[] toRemove; // Contains IDs of mods to be removed.

        public string[] toDownload; // Contains URIs to mod portal pages of mods to download.

        public bool runSetupTool; // Specifies if setup run should be run.

        public bool help; // Force help screen print.

        public bool noDependencyCheck; // Specifies if dependency mods should not be downloaded.
        public bool noCompatibilityCheck; // Specifies if no compatibility check between enabled mods should be run.
        public bool forceChecks; // Force compatibility and/or dependency checks.

        public bool silent; // Specifies if there should be no console output.
    }   

    class Program
    {
        static void Main(string[] args)
        {
            ArgsContainer _parsedArgs;
            Settings settings;

            _parsedArgs = ParseArgs(args);

            if (_parsedArgs.n_args == 0 || _parsedArgs.help)
            {
                ConsoleHelper.PrintUsage();
            }

            if(_parsedArgs.runSetupTool)
            {
                SetupTool();
            }

            if(_parsedArgs.n_args != 0)
            {
                settings = FileHelper.ReadSettings();

                if(_parsedArgs.toDownload.Length != 0)
                {
                    Download(_parsedArgs.toDownload);
                }
                
                if(_parsedArgs.toEnable.Length != 0)
                {
                    
                }
            }
        }
        
        static void Enable(string[] toEnable)
        {

        }
        static void Disable(string[] toDisable)
        {

        }
        static void Download(string[] toDownload)
        {
            
        }
        static void Remove(string[] toRemove)
        {

        }

        // Parses args, return number of args parsed and whole container of juicy args stuff.
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
                        case "-r": case "--no-dependency":
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
                        case "-u": case "--uninstall": case "--remove":
                            toRemove.Add(args[++n]);
                            break;
                        default:
                            ConsoleHelper.ThrowError(ErrorType.WrongOption, args[n]);
                            break;
                    }
                }
                catch(IndexOutOfRangeException e)
                {
                    ConsoleHelper.ThrowError(ErrorType.NoArgument, args[n]);
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
    }

    // Various stuff to help with console I/O.
    static class ConsoleHelper
    {
        // Uh oh
        // There must have been other (BETTER) ways of doing that, I must just happen to be very ignorant lol
        // But I mean it works, soo....
        public static void PrintUsage()
        {
            Console.WriteLine("Usage: factoriomodtool [--setup] [-h] [-s] [-f] [-c] [-r] [-e MOD_ID] [-d MOD_ID] [-i MOD_ID] [-u MOD_ID]\nOptions:\n -h, --help\tPrint this screen.\n\n --setup\tLaunch setup tool.\n\n -s, --silent\tDo not print progress into console.\n\n -e, --enable MOD_ID\tEnable mod with mod id MOD_ID.\n -d, --disable MOD_ID\tDisable mod with mod id MOD_ID.\n -i, --install, --download MOD_ID\tDownload a mod from Factorio mod portal\n\t\t\t\t\t\twith given mod id or mod portal URI.\n\t\t\t\t\t\t(won't work if you specify a non mod-portal URI)\nExample:\n --download Krastorio2\n --download https://mods.factorio.com/mod/Krastorio2\n\n -u, --uninstall, --remove MOD_ID\tRemove downloaded MOD_ID.\n\n -c, --no-compatibility\tDo not test for compatibility of enabled mods.\n -r, --no-dependency\tDo not download dependency mods.\n -f, --force-checks\tForce compatibility and/or dependency checks even if mod list isn't changed.\n");
        }

        // A bit obfuscated way of handling errors.
        public static void ThrowError(ErrorType error = ErrorType.WrongCall, string msg0 = "", string msg1 = "", bool skip = true, int exitCode = 0)
        {
            Console.WriteLine($"ERROR: {error}");
            switch(error)
            {
                case ErrorType.WrongOption:
                    Console.WriteLine($"There is no such option as {msg0}.");
                    break;
                case ErrorType.NoArgument:
                    Console.WriteLine($"Argument expected for option {msg0}, got none at the end of option list.");
                    break;
                case ErrorType.NoSuchURL:
                    Console.WriteLine($"There is no such URL as {msg0}.");
                    break;
                case ErrorType.NoSuchMOD_ID:
                    Console.WriteLine($"There is no such MOD_ID as {msg0}.");
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
                Environment.Exit(exitCode);
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
            catch(FileNotFoundException e)
            {
                ConsoleHelper.ThrowError(ErrorType.NoSetup, "", "", false, (int)ExitCodes.NoFile);
                return new Settings();
            }

            switch (file_stream_settings.ReadLine())
            {
                case "exePath":
                    settings.exePath = file_stream_settings.ReadLine();
                    break;
                case "readWritePath":
                    settings.readWritePath = file_stream_settings.ReadLine();
                    break;
            }

            file_stream_settings.Close();
            file_settings.Close();

            return settings;
        }
    }

    enum ErrorType
    {
        WrongCall,
        WrongOption,
        WrongArgument,
        NoArgument,
        NoSuchURL,
        NoSuchMOD_ID,
        NoPath,
        NoSetup,
        WrongPath
    }
    enum ExitCodes
    {
        NoError = 0,
        NoFile = 2
    }
}
