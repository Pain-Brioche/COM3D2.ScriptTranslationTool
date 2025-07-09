using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Globalization;

namespace COM3D2.ScriptTranslationTool
{
    internal static class Program
    {
        internal static Dictionary<string, List<string>> subtitles = new Dictionary<string, List<string>>();

        internal static string cacheFolder = @"Caches";
        internal static string databaseFile = @"Caches\TranslationData.json";
        internal static string machineCacheFile = @"Caches\MachineTranslationCache.txt";
        internal static string officialCacheFile = @"Caches\OfficialTranslationCache.txt";
        internal static string officialSubtitlesCache = @"Caches\officialSubtitlesCache.txt";
        internal static string manualCacheFile = @"Caches\ManualTranslationCache.txt";
        internal static string archistoryFolder = @"Caches\ArcHistory";
        internal static string errorFile = "Errors.txt";
        internal const string jpCacheFile = "JpCache.json";
        internal const char splitChar = '\t';

        internal static string japaneseScriptFolder = @"Scripts\Japanese";
        internal static string englishScriptFolder = @"Scripts\English";
        internal static string translatedScriptFolder = @"Scripts\AlreadyTranslated";
        internal static string japaneseUIFolder = @"UI\Japanese";
        internal static string i18nExScriptFolder = @"Scripts\i18nEx\English\Script";
        internal static string i18nExUIFolder = @"UI\i18nEx\English\UI";


        internal static string jpGameDataPath = "";
        internal static string engGameDataPath = "";

        internal static bool isTranslatorRunning = false;
        internal static bool exportToi18nEx = false;
        internal static bool isSafeExport = false;
        internal static bool isExportBson = true;
        internal static bool moveFinishedRawScript = false;
        internal static bool forcedTranslation = false;
        internal static bool isSourceJpGame = true;
        internal static bool isSourceEngGame = true;
        internal static bool isIgnoreCbl = true;

        internal static ExportFormat currentExport = ExportFormat.bson;

        static void Main()
        {
            Tools.GetConfig();

            Console.OutputEncoding = System.Text.Encoding.Unicode;
            Console.WriteLine("==================== Initialization ====================\n");
            Console.Title = "Initialization";

            Tools.MakeFolder(englishScriptFolder);
            Tools.MakeFolder(japaneseScriptFolder);
            Tools.MakeFolder(cacheFolder);
            Tools.MakeFolder(japaneseUIFolder);

            if (moveFinishedRawScript)
                Tools.MakeFolder(translatedScriptFolder);

            int scriptsNb = Directory.EnumerateFiles(japaneseScriptFolder, "*.*", SearchOption.AllDirectories).Count(f => Path.GetExtension(f) == ".txt");
            int UInb = Directory.EnumerateFiles(japaneseUIFolder, "*.csv", SearchOption.AllDirectories).Count();

            //Loading the translation database and counting content
            LoadDatabase();

            //Loading JpCache.json into the database
            LoadJpCache();

            //Cache system is considered legacy, this is only to load pre-existing cache files into the database.
            LoadLegacyCache();

            //Loading Official .json (from I2 language.arc), those are UI translation stuff
            LoadI2JSon();


            Console.WriteLine("\n===================== Informations =====================");

            Console.WriteLine("English translation will be selected in this order when available:");
            Tools.WriteLine("Manual Translation", ConsoleColor.Cyan);
            Tools.WriteLine("Official Translations", ConsoleColor.Green);
            Tools.WriteLine("Machine Translation", ConsoleColor.DarkBlue);
            Tools.WriteLine("New Translation (Sugoi must be running)", ConsoleColor.Blue);

            Console.ResetColor();


            // Checking if any translator is ready
            isTranslatorRunning = Translate.CheckTranslatorState().Result;

            // Opening option menu loop
            OptionMenu();

            int scriptCount = 0;
            int lineCount = 0;

            ScriptTranslation.Process(ref scriptCount, ref lineCount);

            //if (UInb > 0) { UITranslation.Process(); }

            if (scriptsNb > 0 || isSourceJpGame)
            {
                Tools.WriteLine($"\n{lineCount} lines translated across {scriptCount} files.", ConsoleColor.Green);
                Tools.WriteLine("Everything done, you may recover your scripts in Scripts\\i18nEx and copy them in your game folder.", ConsoleColor.Green);
            }

            if (UInb > 0)
            {
                Tools.WriteLine("\nEverything done, you may recover your UI files  in UI\\i18nEx and copy them in your game folder.", ConsoleColor.Green);
            }

            Console.WriteLine("Press any key to close this program.");
            Console.ReadKey();
        }

        internal static void OptionMenu()
        {
            ConsoleKeyInfo key = new ConsoleKeyInfo();

            Console.WriteLine("\n===================== Options =====================");
            while (key.Key != ConsoleKey.Enter)
            {
                if ((key.Key == ConsoleKey.D1) || (key.Key == ConsoleKey.NumPad1)) { isSourceJpGame = !isSourceJpGame; }
                if ((key.Key == ConsoleKey.D2) || (key.Key == ConsoleKey.NumPad2)) { isSourceEngGame = !isSourceEngGame; }
                if ((key.Key == ConsoleKey.D3) || (key.Key == ConsoleKey.NumPad3)) { exportToi18nEx = !exportToi18nEx; }
                if ((key.Key == ConsoleKey.D4) || (key.Key == ConsoleKey.NumPad4)) { isSafeExport = !isSafeExport; }
                if ((key.Key == ConsoleKey.D5) || (key.Key == ConsoleKey.NumPad5)) { forcedTranslation = !forcedTranslation; }
                if ((key.Key == ConsoleKey.D6) || (key.Key == ConsoleKey.NumPad6)) { currentExport = (ExportFormat)(((int)currentExport + 1) % 3); }
                if ((key.Key == ConsoleKey.D7) || (key.Key == ConsoleKey.NumPad7)) { JpScriptExtraction.ExtractJapanese(isSourceJpGame); }
                if ((key.Key == ConsoleKey.D8) || (key.Key == ConsoleKey.NumPad8)) { EngScriptExtraction.ExtractOfficial(isSourceEngGame); }
                if ((key.Key == ConsoleKey.D9) || (key.Key == ConsoleKey.NumPad9)) { UITranslation.Process(); }
                //if ((key.Key == ConsoleKey.D0) || (key.Key == ConsoleKey.NumPad0)) { Translate.TranslateAsyncLLM(); }


                Console.ResetColor();
                Console.Write($" 1. Japanese Script Source: "); Tools.WriteLine(isSourceJpGame ? "JP Game .arc" : "Database", ConsoleColor.Blue);
                Console.Write($" 2. English Script Source: "); Tools.WriteLine(isSourceEngGame ? "ENG Game .arc" : "Script Folder", ConsoleColor.Blue);
                Console.Write($" 3. Export to i18nEx: "); Tools.WriteLine(exportToi18nEx.ToString(), ConsoleColor.Blue);
                Console.Write($" 4. Export with official translation: "); Tools.WriteLine((!isSafeExport).ToString(), ConsoleColor.Blue);
                Console.Write($" 5. Forced translation: "); Tools.WriteLine(forcedTranslation.ToString(), ConsoleColor.Blue);
                Console.Write($" 6. Export as: "); Tools.WriteLine(currentExport.ToString(), ConsoleColor.Blue);
                Console.Write($" 7. Build/Update the japanese cache. Source: "); Tools.WriteLine($"{(isSourceJpGame ? jpGameDataPath : japaneseScriptFolder)}", ConsoleColor.Blue);
                Console.Write($" 8. Build/Update the official translation cache. Source: "); Tools.WriteLine($"{(isSourceEngGame ? engGameDataPath : englishScriptFolder)}", ConsoleColor.Blue);
                Console.Write($" 9. Translate UI .csv");
                Console.Write("\nPress Numbers for options or Enter to start translating: ");

                key = Console.ReadKey();
                Console.SetCursorPosition(0, Console.CursorTop - 9);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, Console.CursorTop - 9);
            }
        }

        private static void LoadDatabase()
        {

            if (!File.Exists(databaseFile))
            {
                return; 
            } 

            Tools.WriteLine("Loading Translation Data.", ConsoleColor.White);
            Db.LoadFromJson();
            Console.WriteLine($"Total number of Japanese lines: {Db.data.Count.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))}");
            Console.WriteLine($"Official Translations: {Db.data.Values.Count(line => !string.IsNullOrWhiteSpace(line.Official)).ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))}");
            Console.WriteLine($"Manual Translations: {Db.data.Values.Count(line => !string.IsNullOrWhiteSpace(line.Manual)).ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))}");
            Console.WriteLine($"Machine Translations: {Db.data.Values.Count(line => !string.IsNullOrWhiteSpace(line.Machine)).ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))}");
        }

        private static void LoadLegacyCache()
        {
            if (File.Exists(machineCacheFile) || File.Exists(officialCacheFile) || File.Exists(machineCacheFile))
            {
                Tools.WriteLine("\nLoading legacy caches.", ConsoleColor.White);

                int officialCount = 0;
                int manualCount = 0;
                int machineCount = 0;

                Cache.LoadOfficialCache(ref officialCount);
                Cache.LoadManualCache(ref manualCount);
                Cache.LoadMachineCache(ref machineCount);


                Console.WriteLine("\n");

                if (officialCount > 0)
                    Console.WriteLine($"Legacy Official Translations Loaded: {officialCount}");
                if (manualCount > 0)
                    Console.WriteLine($"Legacy Manual Translations Loaded: {manualCount}");
                if (machineCount > 0)
                    Console.WriteLine($"Legacy Machine Translation Loaded: {machineCount}");

                if (officialCount + manualCount + machineCount > 0)
                {
                    Db.SaveToJson();

                    Console.WriteLine("Legacy caches have been loaded and saved inside the database, you can delete them from the cache folder.");
                }
            }
        }

        private static void LoadI2JSon()
        {
            string[] I2json =
{
                "dynamic.json",
                "dance_subtitle.json",
                "parts.json",
                "yotogi.json"
            };

            if (I2json.Any(f => File.Exists(Path.Combine(Program.cacheFolder, f))))
            {
                Tools.WriteLine("\nLoading I2 .json.", ConsoleColor.White);

                foreach (string jsonFile in I2json)
                {
                    string jsonPath = Path.Combine(Program.cacheFolder, jsonFile);

                    if (!File.Exists(jsonPath)) continue;

                    string jsonData = File.ReadAllText(jsonPath);

                    var jsonSerializerSettings = new JsonSerializerSettings();
                    jsonSerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;

                    var translationsTerms = JsonConvert.DeserializeObject<TermDatas>(jsonData, jsonSerializerSettings);

                    int count = 0;

                    foreach (var termData in translationsTerms.mTerms)
                    {
                        string japanese = termData.Languages[0];
                        string official = termData.Languages[1];

                        if (!string.IsNullOrEmpty(japanese))
                        {
                            Db.Add(japanese, official, TlType.Official);
                            count++;
                        }
                    }
                    Console.WriteLine($"{count} translations recovered from {jsonFile}");
                }

                Db.SaveToJson();

                Console.WriteLine("Officiel .json have been loaded and saved inside the database, you can delete them from the cache folder.");
            }
        }

        private static void LoadJpCache()
        {
            Dictionary<string, List<string>> jpCache = new Dictionary<string, List<string>>();
            string jpCachePath = Path.Combine(cacheFolder, jpCacheFile);


            if (File.Exists(jpCachePath))
            {
                Tools.WriteLine("Loading JpCache.json.", ConsoleColor.Green);
                jpCache = Cache.LoadJson(jpCache, jpCachePath);

                foreach (string key in jpCache.Keys)
                {
                    foreach (string value in jpCache[key])
                    {
                        Db.Add(value, scriptFile: key);
                    }
                }
                Tools.WriteLine($"{jpCache.Count} scripts loaded from JpCache.json.", ConsoleColor.Green);
                Db.SaveToJson();

                Console.WriteLine("JpCache.json has been loaded and saved inside the database.");
            }
        }

        internal enum ExportFormat
        {
            Txt,
            Bson,
            Zst
        }
    }
}
