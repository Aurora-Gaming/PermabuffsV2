﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace Permabuffs_V2
{
    public class BuffGroup
    {
        public string groupName;
        public string groupPerm;
        public bool isperma;
        public List<int> buffIDs;
    }
    public class Config
    {
        public BuffGroup[] buffgroups = new BuffGroup[]
        {
            new BuffGroup() { groupName = "probuffs", groupPerm = "probuffs", isperma = true, buffIDs = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 26, 29, 48, 58, 63, 71, 73, 74, 75, 76, 77, 78, 79, 93, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 119, 121, 122, 123, 124} },
            new BuffGroup() { groupName = "petbuffs", groupPerm = "petbuffs", isperma = false, buffIDs = new List<int>() { 19, 27, 40, 41, 42, 45, 49, 50, 51, 52, 53, 54, 55, 56, 57, 61, 64, 65, 66, 81, 82, 83, 84, 85, 90, 91, 92, 101, 102, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 139}},
            new BuffGroup() { groupName = "debuffs", groupPerm = "debuffs", isperma = true, buffIDs = new List<int>() { 20, 21, 22, 23, 24, 25, 30, 31, 32, 33, 35, 36, 46, 47, 67, 68, 69, 70, 72, 80, 94, 103, 137}}
        };

        public Dictionary<string, List<int>> regionbuffs = new Dictionary<string, List<int>>()
        {
            {"spawn", new List<int>() { 11 }}
        };

        public void Write(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(Permabuffs.config, Formatting.Indented));
        }

        public static Config Read(string path)
        {
            return !File.Exists(path)
                ? new Config()
                : JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
        }
    }
}