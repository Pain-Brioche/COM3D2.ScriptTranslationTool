using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Bson;
using MessagePack;
using ZstdSharp;

namespace COM3D2.ScriptTranslationTool
{
    internal static class Cache
    {
        internal static Dictionary<string, ScriptLine> scriptCache = new Dictionary<string, ScriptLine>();
        internal static Dictionary<string, CsvLine> csvCache = new Dictionary<string, CsvLine>();

        /// <summary>
        /// Load translations from files
        /// </summary>
        internal static Dictionary<string, string> LoadFromFile(string file, bool progress = false)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();

            if (File.Exists(file))
            {
                List<string> subs = new List<string>();

                string[] rawText = File.ReadAllLines(file);
                double total = rawText.Length;
                double count = 0;

                foreach (string line in rawText)
                {
                    count++;

                    if (line.StartsWith(@"//")) { continue; }
                    if (string.IsNullOrEmpty(line)) { continue; }
                    if (line.StartsWith(@"@VoiceSubtitle")) {
                        subs.Add(line);
                        continue;
                    }

                    try
                    {
                        string[] parts = line.Split(Program.splitChar);
                        string key = parts[0];
                        string value = parts[1];

                        // remove unwanted scenarios
                        if (parts.Length != 2 || string.IsNullOrEmpty(value) || string.IsNullOrEmpty(key))
                        {
                            continue;
                        }

                        if (!dict.ContainsKey(key))
                        {
                            dict[key] = value;
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        AddToError(new ScriptLine(file, line));
                        continue;
                    }

                    if (progress)
                    {
                        Tools.ShowProgress(count, total);
                    }
                }

                if (subs.Count >= 1)
                {
                    BuildSubtitles(file, subs);
                }
            }
            return dict;
        }


        // I figured I'd just keep that to load legacy caches, it would be a waste to loose them.
        internal static void LoadOfficialCache(ref int officialCount)
        {
            if (File.Exists(Program.officialCacheFile))
            {
                Console.Write($"Loading Official Translation Cache:     ");
                Dictionary<string, string> officialDic = LoadFromFile(Program.officialCacheFile, true);

                foreach (KeyValuePair<string, string> entry in officialDic)
                {
                    officialCount++;

                    Db.Add(entry.Key, entry.Value, TlType.Official);                    

                    //legacy cache stuff, to be delete
                    /*
                    if (scriptCache.ContainsKey(entry.Key))
                    {
                        if (string.IsNullOrEmpty(scriptCache[entry.Key].OfficialTranslation))
                            scriptCache[entry.Key].OfficialTranslation = entry.Value;
                    }
                    else
                    {
                        scriptCache.Add(entry.Key, new ScriptLine(Program.officialCacheFile, entry.Key, official: entry.Value));
                    }
                    */
                }
            }
        }

        internal static void LoadMachineCache(ref int machineCount)
        {
            //loading machine .txt cache 
            if (File.Exists(Program.machineCacheFile))
            {
                Console.Write($"Loading Machine Translation Cache:     ");
                Dictionary<string, string> machineDic = LoadFromFile(Program.machineCacheFile, true);

                foreach (KeyValuePair<string, string> entry in machineDic)
                {
                    machineCount++;

                    Db.Add(entry.Key, entry.Value, TlType.Machine);

                    //legacy cache stuff, to be delete
                    /*
                    if (scriptCache.ContainsKey(entry.Key))
                    {
                        if (string.IsNullOrEmpty(scriptCache[entry.Key].MachineTranslation))
                            scriptCache[entry.Key].MachineTranslation = entry.Value;
                    }
                    else
                    {
                        scriptCache.Add(entry.Key, new ScriptLine(Program.machineCacheFile, entry.Key, machine: entry.Value));
                    }
                    */
                }
            }
        }

        internal static void LoadManualCache(ref int manualCount)
        {
            //loading manual .txt cache 
            if (File.Exists(Program.manualCacheFile))
            {
                Console.Write($"Loading Manual Translation Cache:     ");
                Dictionary<string, string> manualDic = LoadFromFile(Program.manualCacheFile, true);

                foreach (KeyValuePair<string, string> entry in manualDic)
                {
                    manualCount++;

                    Db.Add(entry.Key, entry.Value, TlType.Manual);

                    //legacy cache stuff, to be delete
                    /*
                    if (scriptCache.ContainsKey(entry.Key))
                    {
                        if (string.IsNullOrEmpty(scriptCache[entry.Key].ManualTranslation))
                            scriptCache[entry.Key].ManualTranslation = entry.Value;
                    }
                    else
                    {
                        scriptCache.Add(entry.Key, new ScriptLine(Program.manualCacheFile, entry.Key, manual: entry.Value));
                    }
                    */
                }
            }

            // loading multiple custom translation .txt cache 
            string[] manualCaches = Directory.GetFiles(Program.cacheFolder, "*.*", SearchOption.AllDirectories);
            manualCaches = manualCaches.Where(f => Path.GetFileName(f).StartsWith("CustomTranslationCache_")).ToArray();

            if (manualCaches.Length > 0)
            {
                foreach (string manualCache in manualCaches)
                {
                    Console.Write($"Loading additional Manual Translations [{Path.GetFileNameWithoutExtension(manualCache).Replace("ManualTranslationCache_", "")}]:     ");
                    var loadedCache = LoadFromFile(manualCache, true);
                    foreach (KeyValuePair<string, string> entry in loadedCache)
                    {
                        manualCount++;

                        Db.Add(entry.Key, entry.Value, TlType.Manual);


                        //legacy cache stuff, to be delete
                        /*
                        if (scriptCache.ContainsKey(entry.Key))
                        {
                            if (string.IsNullOrEmpty(scriptCache[entry.Key].ManualTranslation))
                                scriptCache[entry.Key].ManualTranslation = entry.Value;
                        }
                        else
                        {
                            scriptCache.Add(entry.Key, new ScriptLine(Program.manualCacheFile, entry.Key, manual: entry.Value));
                        }
                        */
                    }
                }
            }
        }


        /// <summary>
        /// Build the official translation cache
        /// </summary>
        internal static void BuildOfficial()
        {
            string[] files = Directory.GetFiles(Program.englishScriptFolder, "*.*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f) == ".txt").ToArray();
            double total = files.Length;
            double count = 0;

            //Skip if not script found
            if (files.Length == 0)
            {
                Tools.WriteLine($"No script found in: {Program.englishScriptFolder}", ConsoleColor.Red);
                Program.OptionMenu();
            }

            Console.Write($"Building official cache from {total} Scripts:     ");


            // listing all english translated lines from the official scritps and save as .txt cache
            foreach (string file in files)
            {
                Dictionary<string, string> fileContent = LoadFromFile(file);

                foreach (KeyValuePair<string, string> entry in fileContent)
                {
                    if (!scriptCache.ContainsKey(entry.Key))
                    {
                        ScriptLine scriptLine = new ScriptLine(file, entry.Key, entry.Value);

                        //add that line to the cache
                        scriptCache.Add(entry.Key, scriptLine);

                        //add that line to the .txt cache
                        string str = Tools.FormatLine(entry.Key, entry.Value);
                        File.AppendAllText(Program.officialCacheFile, str);
                    }
                }

                count++;
                Tools.ShowProgress(count, total);
            }

            Program.OptionMenu();
        }


        /// <summary>
        /// Add subtitles to a specific cache.
        /// </summary>
        internal static void BuildSubtitles(string file, List<string> subs)
        {
            // add to multiple files cache
            Tools.MakeFolder("Caches/Subtitles");
            File.WriteAllLines(Path.Combine("Caches", "Subtitles", Path.GetFileName(file)), subs);
        }


        /// <summary>
        /// Add an entry to the machine translation cache
        /// </summary>
        internal static void AddToMachineCache(ILine line)
        {
            if (string.IsNullOrEmpty(line.Japanese) || string.IsNullOrEmpty(line.MachineTranslation))
            {
                return;
            }

            string savedLine = Tools.FormatLine(line.Japanese, line.MachineTranslation);
            File.AppendAllText(Program.machineCacheFile, savedLine, Encoding.UTF8);
        }


        /// <summary>
        /// Add a faulty line in an error file
        /// </summary>
        internal static void AddToError(ILine line)
        {
            string str = $"##{line.FilePath}\n{line.Japanese}\n{line.English}\n\n";
            File.AppendAllText(Program.errorFile, str);
        }


        /// <summary>
        /// Save Cache as .json
        /// </summary>
        public static void SaveJson<T>(T dic, string cacheFilePath, bool isSafeExport = false)
        {
            if (isSafeExport)
            {
                string safeCacheFilePath = $"{cacheFilePath}_safe";
                string safeJson = JsonConvert.SerializeObject(dic, Formatting.Indented, new JsonSerializerSettings { ContractResolver = new SafeContractResolver() });
                File.WriteAllText(safeCacheFilePath, safeJson);
            }

            string json = JsonConvert.SerializeObject(dic, Formatting.Indented);
            File.WriteAllText(cacheFilePath, json);
        }

        public static T LoadJson<T>(T dictionary, string path)
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                dictionary = JsonConvert.DeserializeObject<T>(json);
            }
            return dictionary;
        }





        public static void TestZst()
        {
            //cleaning now useless translation cache
            scriptCache.Clear();


            string sourcePath = Program.i18nExScriptFolder + "\\script.bson.MsgPack";
            string savePath = Program.i18nExScriptFolder + "\\script.zst";
            MessagePackSerializerOptions SerializerOptions = new MessagePackSerializerOptions(MessagePack.Resolvers.ContractlessStandardResolver.Instance);

            //Loading data 
            Console.WriteLine("Loading Data");
            byte[] data = File.ReadAllBytes(sourcePath);
            Dictionary <string, byte[]> testDic = MessagePackSerializer.Deserialize<Dictionary<string,byte[]>>(data);


            //Compressing and saving
            Console.WriteLine("Compression");
            using (var fileStream = new FileStream(savePath, FileMode.Create))
            {
                using (var compressor = new CompressionStream(fileStream, 22))
                {
                    MessagePackSerializer.Serialize(compressor, testDic, SerializerOptions);
                }
            }
        }

        public class SafeContractResolver : DefaultContractResolver
        {
            private readonly string _startingWithOfficial = "Official";

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                IList<JsonProperty> properties = base.CreateProperties(type, memberSerialization);

                // only serializer properties that don't start with "Official"
                properties =
                    properties.Where(p => !p.PropertyName.StartsWith(_startingWithOfficial)).ToList();

                return properties;
            }
        }
    }
}
