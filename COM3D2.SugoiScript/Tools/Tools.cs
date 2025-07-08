using System;
using System.IO;
using System.Configuration;
using Microsoft.Win32;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json;
using MessagePack;
using ZstdSharp;

namespace COM3D2.ScriptTranslationTool
{
    internal static class Tools
    {
        /// <summary>
        /// Display progress as xx% in the console
        /// </summary>
        internal static void ShowProgress(double current, double max)
        {
            double progress = (current / max) * 100;
            string str = Math.Floor(progress).ToString().PadRight(3);
            Console.Write($"\b\b\b\b{str}%");
            if (str == "100") { Console.Write("\n"); }
        }


        /// <summary>
        /// Create directory helper
        /// </summary>
        /// <param name="folderPath"></param>
        public static void MakeFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }

        /// <summary>
        /// return a formated line suited for scripts and caches
        /// </summary>
        /// <param name="jp"></param>
        /// <param name="eng"></param>
        /// <returns></returns>
        internal static string FormatLine(string jp, string eng)
        {
            string formatedLine = $"{jp}{Program.splitChar}{eng}\n";
            return formatedLine;
        }


        /// <summary>
        /// Recover config from file
        /// </summary>
        internal static void GetConfig()
        {
            if (File.Exists("COM3D2.ScriptTranslationTool.exe.config"))
            {
                Program.machineCacheFile = ConfigurationManager.AppSettings.Get("MachineTranslationCache");
                Program.officialCacheFile = ConfigurationManager.AppSettings.Get("OfficialTranslationCache");
                Program.officialSubtitlesCache = ConfigurationManager.AppSettings.Get("OfficialSubtitlesCache");
                Program.manualCacheFile = ConfigurationManager.AppSettings.Get("ManualTranslationCache");
                Program.errorFile = ConfigurationManager.AppSettings.Get("TranslationErrors");

                Program.japaneseScriptFolder = ConfigurationManager.AppSettings.Get("JapaneseScriptPath");
                Program.i18nExScriptFolder = ConfigurationManager.AppSettings.Get("i18nExScriptPath");
                Program.englishScriptFolder = ConfigurationManager.AppSettings.Get("EnglishScriptPath");
                Program.translatedScriptFolder = ConfigurationManager.AppSettings.Get("AlreadyTranslatedScriptFolder");

                Program.moveFinishedRawScript = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("MoveTranslated"));
                Program.exportToi18nEx = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("ExportToi18nEx"));

                //getting GameData path Setting > Registry > Ask
                if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings.Get("JPGamePath")))
                {
                    Program.jpGameDataPath = Path.Combine(ConfigurationManager.AppSettings.Get("JPGamePath"),"GameData");
                }
                else
                {
                    RegistryKey keyJp = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\KISS\カスタムオーダーメイド3D2");

                    if (keyJp != null)
                    {
                        string installPath = keyJp.GetValue("InstallPath").ToString();
                        keyJp.Close();
                        Program.jpGameDataPath = Path.Combine(installPath, "GameData");
                    }
                }

                if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings.Get("ENGGamePath")))
                {
                    Program.engGameDataPath = Path.Combine(ConfigurationManager.AppSettings.Get("ENGGamePath"), "GameData");
                }
                else
                {
                    RegistryKey keyEn = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\KISS\CUSTOM ORDER MAID3D 2");

                    if (keyEn != null)
                    {
                        string installPath = keyEn.GetValue("InstallPath").ToString();
                        keyEn.Close();
                        Program.engGameDataPath = Path.Combine(installPath, "GameData");
                    }
                }
            }
        }

        /// <summary>
        /// WriteLine with selected color then reset to default
        /// </summary>
        /// <param name="str"></param>
        /// <param name="color"></param>
        internal static void WriteLine(string str, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(str);
            Console.ResetColor();
        }

        /// <summary>
        /// Write with selected color then reset to default
        /// </summary>
        /// <param name="str"></param>
        /// <param name="color"></param>
        internal static void Write(string str, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(str);
            Console.ResetColor();
        }
    }

    internal static class Export
    {
        /// <summary>
        /// Save objects as .bson
        /// </summary>
        /// <param name="objectToSerialize"></param>
        /// <param name="path"></param>
        public static void SaveBson<T>(T objectToSerialize, string path)
        {
            using (var fileStream = new FileStream(path, FileMode.Create))
            using (var writer = new BsonDataWriter(fileStream))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, objectToSerialize);
            }
        }

        /// <summary>
        /// Saves an object as messagePack compressed as .zst
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objectToSerialize"></param>
        /// <param name="path"></param>
        public static void SaveZstdMsgPack<T>(T objectToSerialize, string path)
        {
            MessagePackSerializerOptions SerializerOptions = new MessagePackSerializerOptions(MessagePack.Resolvers.ContractlessStandardResolver.Instance);

            using (var fileStream = new FileStream(path, FileMode.Create))
            using (var compressor = new CompressionStream(fileStream, 22))
            {
                MessagePackSerializer.Serialize(compressor, objectToSerialize, SerializerOptions);
            }
        }
    }
}