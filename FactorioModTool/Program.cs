using System;
using System.IO;
using System.Net;
using System.Text.Json;

namespace FactorioModTool
{
    struct InitStuff
    {
        public string exePath; // Factorio executable path. This is also a data path, which is used to determine Factorio version.
        public string readWritePath; // Factorio read/write data path, which is a path to: player-data.json, mods and saves.
    }
    struct ArgsContainer
    {
        public int n_args; // Number of parsed options. Does not include option arguments.

        public string[] toEnable; // Contains IDs of mods to be enabled.
        public string[] toDisable; // Contains IDs of mods to be disabled.

        public string[] toDownload; // Contains URLs to mod portal pages of mods to download.

        public bool runSetupTool; // Specifies if setup run should be run.

        public bool help; // Force help screen print.

        public bool noDependencyCheck; // Specifies if dependency mods should not be downloaded.
        public bool noCompatibilityCheck; // Specifies if no compatibility check between enabled mods should be run.
        public bool forceChecks; // Specifies if compatibility and/or dependency should be force checked.

        public bool silent; // Specifies if there should be no console output.
    }   

    class Program
    {
        static void Main(string[] args)
        {
            ArgsContainer _parsedArgs = ParseArgs(args);

            if (_parsedArgs.n_args == 0 || _parsedArgs.help)
            {
                ConsoleHelper.PrintUsage();
            }

            if(_parsedArgs.runSetupTool)
            {
                SetupTool();
            }

            InitStuff init = Init();
        }

        // Parses args, return number of args parsed and whole container of juicy args stuff.
        // (See ArgsContainer struct for more info)
        static ArgsContainer ParseArgs(string[] args)
        {
            int n_args = 0;

            ArgsContainer _parsedArgs = new ArgsContainer();

            for (int n = 0; n < args.Length; n++)
            {
                string curArg = args[n];
                string nextArg = args[n + 1];
                string nextNextArg = args[n + 2];

                switch(curArg)
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
                    default:
                        ConsoleHelper.PrintError(ErrorType.WrongOption, curArg);
                        break;
                }

                n_args++;
            }

            _parsedArgs.n_args = n_args;

            return _parsedArgs;
        }
        
        // Loads path settings.
        static InitStuff Init()
        {
            return new InitStuff();
        }
        
        // Setup tool to create path settings.
        static void SetupTool()
        {

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
            Console.WriteLine("Usage: factoriomodtool [--setup] [-h] [-s] [-f] [-c] [-r] [-e MOD_ID] [-d MOD_ID] [-i MOD_ID] [-u MOD_ID]\nOptions:\n -h, --help\tPrint this screen.\n\n --setup\tLaunch setup tool.\n\n -s, --silent\tDo not print progress into console.\n\n -e, --enable MOD_ID\tEnable mod with mod id MOD_ID.\n -d, --disable MOD_ID\tDisable mod with mod id MOD_ID.\n -i, --install, --download MOD_ID\tDownload a mod from Factorio mod portal\n\t\t\t\t\t\twith given mod id or mod portal URI.\n\t\t\t\t\t\t(won't work if you specify a non mod-portal URI)\nExample:\n --download Krastorio2\n --download https://mods.factorio.com/mod/Krastorio2\n\n -u, --uninstall, --remove MOD_ID\tRemove downloaded MOD_ID.\n\n -c, --no-compatibility\tDo not test for compatibility of enabled mods.\n -r, --no-dependency\tDo not download dependency mods.\n -f, --force-checks\tForce compatibility and/or dependency checks even if mod list isn't changed.");
        }

        // A bit obfuscated way of printing errors.
        public static void PrintError(ErrorType error = ErrorType.WrongCall, string msg0 = "", string msg1 = "", string msg2 = "", bool skip = true)
        {
            Console.WriteLine($"ERROR {error}\n");
            switch(error)
            {
                case ErrorType.WrongOption:
                    Console.Write($"There is no such option as {msg0}.");
                    break;
                case ErrorType.WrongArgument:
                    Console.Write($"For option {msg0}, expected argument of type {msg1}, got {msg2}.");
                    break;
                case ErrorType.NoSuchURL:
                    Console.Write($"There is no such URL as {msg0}.");
                    break;
                case ErrorType.NoSuchMOD_ID:
                    Console.Write($"There is no such MOD_ID as {msg0}.");
                    break;
                case ErrorType.NoPath:
                    Console.Write($"Required path {msg0} is not specified. Please, launch setup tool: factoriomodtool --setup");
                    break;
                case ErrorType.NoSetup:
                    Console.Write($"Settings file is missing. Please, launch setup tool: factoriomodtool --setup");
                    break;
                default:
                    Console.Write("This seems to be a wrong error call. Nevermind.");
                    break;
            }

            if(skip)
            {
                Console.WriteLine("Skipping.");
            }
        }
    }
    // Various stuff to help with file I/O.
    static class FileHelper
    {
        
    }

    enum ErrorType
    {
        WrongOption,
        WrongArgument,
        NoSuchURL,
        NoSuchMOD_ID,
        NoPath,
        NoSetup,
        WrongCall
    }
}
