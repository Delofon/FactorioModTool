using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace FactorioModTool
{
    // A class that handles mod-list.json stuff.
    static class ModLister
    {
        public static void WriteModList(string readWritePath, string[] toEnable, string[] toDisable)
        {

        }
    }

    struct Mod
    {
        public string name { get; set; }
        public bool enabled { get; set; }

        public Mod(string name, bool enabled)
        {
            this.name = name;
            this.enabled = enabled;
        }
    }
}
