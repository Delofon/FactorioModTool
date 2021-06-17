﻿using System;
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
            switch (error)
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

            if (skip)
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
}