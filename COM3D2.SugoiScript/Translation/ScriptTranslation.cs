using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace COM3D2.ScriptTranslationTool
{
    internal static class ScriptTranslation
    {
        static Dictionary<string, List<string>> jpCache = new Dictionary<string, List<string>>();
        static List<string> scripts = new List<string>();
        static Dictionary<string, List<string>> subtitles = new Dictionary<string, List<string>>();
        static ScriptSourceType scriptSourceType = ScriptSourceType.None;


        internal static void Process(ref int scriptCount, ref int lineCount)
        {
            //getting script list from one of three potential sources
            scripts = GetScripts();

            if (scripts.Count == 0)
            {
                Tools.WriteLine("No Scripts or Cache found, Translation Aborted", ConsoleColor.Red);
                Program.OptionMenu();
            }

            foreach (string script in scripts)
            {
                string scriptName = Path.GetFileName(script);

                //getting line list from one of three potential sources
                var lines = GetLines(scriptName);
                if (lines.Count == 0) { continue; }

                Tools.WriteLine($"\n-------- {scriptName} --------", ConsoleColor.Yellow);

                foreach (string line in lines)
                {
                    // skip if line is empty
                    if (string.IsNullOrEmpty(line.Trim())) continue;

                    ConsoleColor color = ConsoleColor.Gray;
                    string japanese = line.Trim();
                    string translation = string.Empty;

                    lineCount++;
                    Console.Write(japanese);
                    Tools.Write(" => ", ConsoleColor.Yellow);

                    Line currentLine = Db.GetLine(japanese);

                    //Updating the script name List
                    Db.Update(japanese, scriptFile: scriptName);


                    //Get best translation possible Manual > Official > Machine, the database isn't supposed to have empty entries so it should always return one.
                    translation = currentLine.GetBestTranslation(ref color);

                    //translate if needed and possible
                    if (string.IsNullOrEmpty(translation) && Program.isTranslatorRunning)
                    {
                        translation = currentLine.GetTranslation(japanese);
                        color = ConsoleColor.Blue;
                    }

                    //In case a translation is missing and sugoi isn't running
                    else if (string.IsNullOrEmpty(translation) && !Program.isTranslatorRunning)
                    {
                        Tools.WriteLine($"This line wasn't found in any cache and can't be translated since sugoi isn't running", ConsoleColor.Red);
                        continue;
                    }

                    //Ignore faulty results
                    if (currentLine.HasError || currentLine.HasRepeat || translation == string.Empty)
                    {
                        Tools.WriteLine($"This line returned a faulty translation and was placed in {Program.errorFile}", ConsoleColor.Red);
                        continue;
                    }

                    //Display final result
                    Tools.WriteLine(translation, color);
                }

                Console.Title = $"Processing ({scriptCount} out of {scripts.Count} scripts)";
            }

            if (Program.exportToi18nEx)
            {
                if (Program.isExportBson)
                {
                    ExportToBson();
                    ExportToZst();
                }
                else 
                    ExportToTxt();
            }
        }


        private static void ExportToTxt()
        {
            Tools.WriteLine("Exporting as a batch of .txt files...", ConsoleColor.Magenta);

            // Create folder to sort script files in
            ScriptManagement.CreateSortedFolders();

            // Get all scripts names in the database
            IEnumerable<string> scriptList = Db.data.Values
                                      .SelectMany(line => line.scriptFiles)
                                      .Distinct();                                      

            //for each script get the japanese lines and their translations then save as .txt
            //this export format does not support tabulations in sentences
            foreach (string script in scriptList)
            {
                IEnumerable<string> lines = Db.data
                                    .Where(d => d.Value.scriptFiles.Contains(script))
                                    .Select(d => $"{d.Key}{Program.splitChar}{d.Value.GetBestTranslation().Replace("\t", "")}");

                //Adding back subtitles
                var subs = GetSubtitles(script);
                if (subs.Any()) { lines = lines.Concat(subs); }

                ScriptManagement.SaveTxt(script, lines);     
            }
        }

        private static void ExportToBson()
        {
            Tools.WriteLine("\nSaving script as .bson.", ConsoleColor.Magenta);

            Tools.MakeFolder(Program.i18nExScriptFolder);
            string bsonPath = Path.Combine(Program.i18nExScriptFolder, "script.bson");
            var byteDictionary = GetBytesDictionary();

            Export.SaveBson(byteDictionary, bsonPath);
        }

        private static void ExportToZst()
        {
            Tools.WriteLine("\nSaving script as .zst.", ConsoleColor.Magenta);

            Tools.MakeFolder(Program.i18nExScriptFolder);
            string zstPath = Path.Combine(Program.i18nExScriptFolder, "script.zst");
            var byteDictionary = GetBytesDictionary();

            Export.SaveZstdMsgPack(byteDictionary, zstPath);
        }

        private static Dictionary<string, byte[]> GetBytesDictionary()
        {
            Dictionary<string, byte[]> byteDictionary = new Dictionary<string, byte[]>();

            // Get all scripts names in the database
            IEnumerable<string> scriptList = Db.data.Values
                                      .SelectMany(line => line.scriptFiles)
                                      .Distinct();                                      

            //get all Japanese and English for each script and encode them to UTF8 to add to the bson dictionary
            foreach (string script in scriptList)
            {
                IEnumerable<string> lines = Db.data
                                           .Where(d => d.Value.scriptFiles.Contains(script))
                                           .Select(d => $"{d.Key}{Program.splitChar}{d.Value.GetBestTranslation()}");

                //Adding back subtitles
                var subs = GetSubtitles(script);
                if (subs.Any()) {lines = lines.Concat(subs);}


                byte[] bytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines).Trim());
                byteDictionary.Add(script, bytes);
            }

            return byteDictionary;
        }

        private static IEnumerable<string> GetSubtitles(string script)
        {
            string subPath = Path.Combine(Program.cacheFolder, "Subtitles");


            if (subtitles.Count == 0 && Directory.Exists(subPath))
            {
                IEnumerable<string> subtitlesFiles = Directory.EnumerateFiles(subPath);

                foreach (string subtitleFile in subtitlesFiles)
                {
                    string scriptName = Path.GetFileNameWithoutExtension(subtitleFile);
                    List<string> sbs = File.ReadAllLines(subtitleFile).ToList();

                    subtitles.Add(scriptName, sbs);
                }
            }

            List<string> subs = new List<string>();

            if (subtitles.ContainsKey(script))
                subs = subtitles[script];

            return subs;
        }

        private static List<string> GetScripts()
        {
            var scriptsSource = new List<string>();

            //The program will prioritize as follow: JpCache.json > Loose .txt scripts > TranslationData.json
            // The reason being JpCache has more likelyhood to be recent and more accurate to the game's actual content, Loose script for small translations job and lastly the database with everything ever recorded.

            if (Program.isSourceJpGame)
            {
                string jpCachePath = Path.Combine(Program.cacheFolder, Program.jpCacheFile);

                if (File.Exists(jpCachePath))
                {
                    jpCache = Cache.LoadJson(jpCache, jpCachePath);
                    Tools.WriteLine($"Loading {jpCache.Count} scripts from JpCache.json", ConsoleColor.Green);

                    scriptsSource = jpCache.Keys.ToList();
                    scriptSourceType = ScriptSourceType.JpCache;
                }
            }
            else
            {
                scriptsSource = Directory.EnumerateFiles(Program.japaneseScriptFolder, "*.txt*", SearchOption.AllDirectories)
                                   .ToList();

                if (scriptsSource.Any())
                {
                    Tools.WriteLine($"Loading {scriptsSource.Count} files from the Japanese script folder.", ConsoleColor.Green);
                    scriptSourceType = ScriptSourceType.ScriptFile;
                }
                else
                {
                    // Get all scripts names in the database, only scripts having at least one sentence to translate are selected.
                    scriptsSource = Db.data.Values
                                    .Where(l => string.IsNullOrEmpty(l.GetBestTranslation()))  
                                    .SelectMany(line => line.scriptFiles)
                                    .Distinct()
                                    .ToList();
                    Tools.WriteLine($"Loading {scriptsSource} scripts from the Translation Database.", ConsoleColor.Green);
                    Tools.WriteLine("Please note that only scripts containing untranslated sentences are selected, to avoid unecessary listing.", ConsoleColor.Green);
                    scriptSourceType = ScriptSourceType.Database;
                }
            }

            return scriptsSource;
        }

        private static List<string> GetLines(string filename)
        {
            List<string> lines = new List<string>();

            if (scriptSourceType == ScriptSourceType.JpCache)
            {
                lines = jpCache[filename];
            }
            else if (scriptSourceType == ScriptSourceType.ScriptFile)
            {
                //Load all scripts with the same name
                string[] sameNameScripts = scripts.Where(f => Path.GetFileName(f) == filename).ToArray();

                //merge them as one without duplicated lines.
                foreach (string s in sameNameScripts)
                    lines.AddRange(File.ReadAllLines(s));
            }
            else if (scriptSourceType == ScriptSourceType.Database)
            {
                //Only returns lines without available translations.
                lines = Db.data
                          .Where(d => d.Value.scriptFiles.Contains(filename) && string.IsNullOrEmpty(d.Value.GetBestTranslation()))
                          .Select(l => l.Key)
                          .ToList();
            }


            return lines.Distinct().ToList();
        }

        private enum ScriptSourceType
        {
            None,
            JpCache,
            ScriptFile,
            Database
        }
    }
}
