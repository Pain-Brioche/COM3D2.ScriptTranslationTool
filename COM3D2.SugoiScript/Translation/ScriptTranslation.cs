using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace COM3D2.ScriptTranslationTool
{
    internal static class ScriptTranslation
    {
        static HashSet<string> alreadyParsedScripts = new HashSet<string>();
        static Dictionary<string, List<string>> jpCache = new Dictionary<string, List<string>>();
        static Dictionary<string, byte[]> bsonDictionarry = new Dictionary<string, byte[]>();
        static List<string> scriptFiles = new List<string>();


        internal static void Process(ref int scriptCount, ref int lineCount)
        {
            // Create folder to sort script files in
            if (Program.exportToi18nEx && !Program.isExportBson)
            {
                ScriptManagement.CreateSortedFolders();
            }

            //getting script list from one of two potential sources
            scriptFiles = GetScripts();

            int scriptTotal = scriptFiles.Count;

            if (scriptTotal == 0)
            {
                Tools.WriteLine("No Scripts or Cache found, Translation Aborted", ConsoleColor.Red);
                Program.OptionMenu();
            }

            foreach (string file in scriptFiles)
            {
                if (alreadyParsedScripts.Contains(file)) { continue; }

                StringBuilder concatStrings = new StringBuilder();
                string filename = Path.GetFileName(file);
                bool hasError = false;

                Tools.WriteLine($"\n-------- {filename} --------", ConsoleColor.Yellow);

                //getting line list from one of two potential sources
                var lines = GetLines(filename);

                foreach (string line in lines)
                {
                    ScriptLine currentLine;

                    if (Cache.scriptCache.ContainsKey(line.Trim()))
                    {
                        //Console.WriteLine("EXISTING LINE");
                        currentLine = Cache.scriptCache[line.Trim()];
                    }                        
                    else
                    {
                        //Console.WriteLine("NEW LINE");
                        currentLine = new ScriptLine(filename, line);
                    }
                        

                    lineCount++;

                    // skip if line is empty
                    if (string.IsNullOrEmpty(currentLine.Japanese)) { continue; }

                    Console.Write(currentLine.Japanese);
                    Tools.Write(" => ", ConsoleColor.Yellow);


                    //Translate if needed/possible
                    if (Program.isSugoiRunning)
                    {
                        if (string.IsNullOrEmpty(currentLine.English) || (Program.forcedTranslation && string.IsNullOrEmpty(currentLine.MachineTranslation)))
                        {
                            currentLine.GetTranslation();

                            //ignore faulty returns
                            if (currentLine.HasRepeat || currentLine.HasError)
                            {
                                hasError = true;
                                Cache.AddToError(currentLine);
                                Tools.WriteLine($"This line returned a faulty translation and was placed in {Program.errorFile}", ConsoleColor.Red);
                                continue;
                            }

                            Cache.AddToMachineCache(currentLine);
                        }
                    }
                    else if (string.IsNullOrEmpty(currentLine.English))
                    {
                        Tools.WriteLine($"This line wasn't found in any cache and can't be translated since sugoi isn't running", ConsoleColor.Red);
                        continue;
                    }


                    Tools.WriteLine(currentLine.English, currentLine.Color);

                    currentLine.FilePath = filename;

                    // add to i18nEx script folder
                    if (Program.exportToi18nEx)
                    {
                        if (Program.isExportBson)
                            concatStrings.AppendLine($"{currentLine.Japanese}\t{currentLine.English}".Trim());                        
                        else
                            ScriptManagement.AddTo(currentLine);
                    }
                }

                scriptCount++;

                if (Program.isExportBson)
                {
                    bsonDictionarry.Add($"{Path.GetFileNameWithoutExtension(filename)}.txt",Encoding.UTF8.GetBytes(concatStrings.ToString().Trim()));
                }

                alreadyParsedScripts.Add(filename);

                if (Program.moveFinishedRawScript)
                {
                    ScriptManagement.MoveFinished(file, hasError);
                }

                Console.Title = $"Processing ({scriptCount} out of {scriptTotal} scripts)";
            }

            // Adding back subtitles
            if (Directory.Exists(Path.Combine(Program.cacheFolder, "Subtitles")) && Program.exportToi18nEx)
            {
                IEnumerable<string> subtitlesFiles = Directory.EnumerateFiles(Path.Combine(Program.cacheFolder, "Subtitles"));

                if (subtitlesFiles.Any())
                {
                    foreach (string subFile in subtitlesFiles)
                    {

                        if (Program.isExportBson)
                        {
                            string subFileName = $"{Path.GetFileNameWithoutExtension(subFile)}";
                            var bytes = File.ReadAllBytes(subFile);

                            if(bytes.Length > 0)
                            {
                                if (bsonDictionarry.ContainsKey(subFileName))
                                {
                                    bsonDictionarry[subFileName] = bsonDictionarry[subFileName].Concat(bytes).ToArray();
                                }
                                else
                                {
                                    bsonDictionarry.Add(subFileName, bytes);
                                }
                            }
                        }
                        else
                        {
                            string subFileName = $"{Path.GetFileName(subFile)}";
                            string[] scriptFile = Directory.GetFiles(Program.i18nExScriptFolder, subFileName, SearchOption.AllDirectories);

                            if (scriptFile.Length > 0)
                            {
                                Tools.WriteLine($"Adding subtitles to {subFileName}.", ConsoleColor.Green);
                                string[] strings = File.ReadAllLines(subFile);
                                File.AppendAllLines(scriptFile[0], strings);
                            }
                            else
                            {
                                Tools.WriteLine($"Creating new subtitle script {subFileName}", ConsoleColor.Green);
                                string subPath = Path.Combine(Program.i18nExScriptFolder, "[Subtitles]");
                                Tools.MakeFolder(subPath);
                                File.Copy(subFile, Path.Combine(subPath, subFileName));
                            }
                        }
                    }
                }
            }
            else
            {
                Tools.WriteLine($"No subtitles cache found, skipping", ConsoleColor.White);
            }

            if (Program.exportToi18nEx && Program.isExportBson)
            {
                Tools.WriteLine("\nSaving script as .bson.", ConsoleColor.Magenta);
                Tools.MakeFolder(Program.i18nExScriptFolder);
                string bsonPath = Path.Combine(Program.i18nExScriptFolder, "script.bson");
                Cache.SaveBson(bsonDictionarry, bsonPath);


                Tools.WriteLine("\nSaving script as .zst.", ConsoleColor.Magenta);
                Tools.MakeFolder(Program.i18nExScriptFolder);
                string zstPath = Path.Combine(Program.i18nExScriptFolder, "script.zst");
                Cache.SaveZstdMsgPack(bsonDictionarry, zstPath);
            }
        }



        private static List<string> GetScripts()
        {
            var scripts = new List<string>();

            if (Program.isSourceJpGame)
            {
                string jpCachePath = Path.Combine(Program.cacheFolder, Program.jpCacheFile);

                if (File.Exists(jpCachePath))
                {
                    Console.WriteLine("Loading Jp cache");
                    jpCache = Cache.LoadJson(jpCache, jpCachePath);
                    Tools.WriteLine($"{jpCache.Count} scripts cached", ConsoleColor.Green);

                    scripts = jpCache.Keys.ToList();
                }
                else
                {
                    Console.WriteLine("Jp cache not found.");

                }
            }
            else
            {
                scripts = Directory.EnumerateFiles(Program.japaneseScriptFolder, "*.txt*", SearchOption.AllDirectories)
                                   .ToList();
            }

            return scripts;
        }

        private static List<string> GetLines(string filename)
        {
            List<string> lines = new List<string>();

            if (Program.isSourceJpGame)
            {
                lines = jpCache[filename];
            }
            else
            {
                //Load all scripts with the same name
                string[] sameNameScripts = scriptFiles.Where(f => Path.GetFileName(f) == filename).ToArray();

                //merge them as one without duplicated lines.
                foreach (string s in sameNameScripts)
                {
                    lines.AddRange(File.ReadAllLines(s));
                }
            }

            return lines.Distinct().ToList();
        }
    }
}
