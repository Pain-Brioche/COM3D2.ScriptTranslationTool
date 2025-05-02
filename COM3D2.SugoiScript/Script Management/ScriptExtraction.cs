using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CM3D2.Toolkit.Guest4168Branch.Arc;
using Newtonsoft.Json;
using ArcFileEntry = CM3D2.Toolkit.Guest4168Branch.Arc.Entry.ArcFileEntry;

namespace COM3D2.ScriptTranslationTool
{
    internal static class EngScriptExtraction
    {
        const string engArchistoryFile = "eng_archistory.json";
        static Dictionary<string, long> arcHistoryENG = new Dictionary<string, long>();
        internal static HashSet<string> shortOfficialCache = new HashSet<string>();   //shortOfficialCache is there to avoid duplicated lines

        internal static void ExtractOfficial(bool isSourceEngGame)
        {
            //If built from the Script/English folder
            if(!isSourceEngGame) { Cache.BuildOfficial(); Program.OptionMenu(); }

            Console.Title = "Extracting ENG Scripts";

            if (string.IsNullOrEmpty(Program.engGameDataPath) || !Directory.Exists(Program.engGameDataPath))
            {
                Tools.WriteLine("English game install path isn't set or is wrong, please enter it.", ConsoleColor.Yellow);
                Tools.Write("COM3D2.exe path:", ConsoleColor.White);
                string path = Console.ReadLine();
                Program.engGameDataPath = Path.Combine(path, "GameData");
            }

            //Don't bother if the GameData can't be found
            if (!Directory.Exists(Program.engGameDataPath))
            {
                Tools.WriteLine($"{Program.engGameDataPath} doesn't exist, aborting extraction", ConsoleColor.Red);
                Program.OptionMenu();
            }


            string arcHistoryJson = Path.Combine(Program.archistoryFolder, engArchistoryFile);
            if (File.Exists(Program.officialCacheFile))
            {
                shortOfficialCache = File.ReadAllLines(Program.officialCacheFile).ToHashSet();
                Tools.WriteLine($"{shortOfficialCache.Count} lines already cached", ConsoleColor.Green);

                if (File.Exists(arcHistoryJson))
                {
                    Console.WriteLine("Loading extraction history");
                    arcHistoryENG = Cache.LoadJson(arcHistoryENG, arcHistoryJson);
                }
            }

            int startingLineNb = shortOfficialCache.Count;
            int newScripts = 0;
            string[] arcs = Directory.GetFiles(Program.engGameDataPath, "*.*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f) == ".arc").ToArray();

            foreach (string arc in arcs)
            {
                //skip already parsed .arc if they haven't changed size
                string arcName = Path.GetFileName(arc);
                long arcFileSize = new FileInfo(arc).Length;

                if (arcHistoryENG.ContainsKey(arcName))
                {
                    if (arcHistoryENG[arcName] == arcFileSize)
                        continue;
                    else
                        arcHistoryENG.Remove(arcName);
                }

                Tools.Write($"{arcName}: ", ConsoleColor.Gray);

                //opening the .arc file
                ArcFile arcFile = new ArcFile(arc);

                int scriptScannedNb = 0;

                //getting all .ks and their string content
                foreach (ScriptFile script in arcFile.GetAllScripts())
                {
                    //Console.Write($", [{script.Name}]");
                    for (int i = 0; i < script.Lines.Count(); i++)
                    {
                        //Special case for the main story subtitles
                        if (script.Lines[i].StartsWith("@LoadSubtitleFile"))
                        {
                            script.CaptureSubtitleFile(i, arcFile);
                        }

                        //written dialogues always start by "@talk"
                        else if (script.Lines[i].StartsWith("@talk", StringComparison.InvariantCultureIgnoreCase))
                        {
                            script.CaptureTalk(i);
                        }

                        //another type of subtitles
                        else if (script.Lines[i].StartsWith("@SubtitleDisplayForPlayVoice", StringComparison.InvariantCultureIgnoreCase))
                        {
                            script.CaptureSubtitle(i);
                        }

                        //Choice type boxes
                        else if (script.Lines[i].StartsWith("@ChoicesSet", StringComparison.InvariantCultureIgnoreCase))
                        {
                            script.CaptureChoice(i);
                        }

                        //Yotogi Message is a new kind of text found only in Yotigi+ scripts
                        else if (script.Lines[i].StartsWith("@YotogiMessage", StringComparison.InvariantCultureIgnoreCase))
                        {
                            script.CaptureYotogiMessage(i);
                        }
                    }

                    scriptScannedNb++;
                    newScripts++;
                    script.SaveToCache(Program.officialCacheFile, false);
                    script.SaveToCache(Path.Combine(Program.cacheFolder, "__npc_names.txt"), true);
                    script.SaveSubtitles();
                }
                Tools.WriteLine($"{scriptScannedNb} scripts", ConsoleColor.Green);
                arcHistoryENG.Add(arcName, arcFileSize);
                Cache.SaveJson(arcHistoryENG, arcHistoryJson, false);
            }

            Tools.WriteLine($"{shortOfficialCache.Count - startingLineNb} new translations in {newScripts} new scripts\n\n", ConsoleColor.Green);

            int OffCount = 0;
            if (newScripts > 0)
                Cache.LoadOfficialCache(ref OffCount); //reload the cache if anything was added.
        }
    }

    internal static class JpScriptExtraction
    {
        const string jpArchistoryFile = "jp_archistory.json";
        static Dictionary<string, long> arcHistoryJP = new Dictionary<string, long>();
        static SortedDictionary<string, List<string>> jpCache = new SortedDictionary<string, List<string>> ();

        internal static void ExtractJapanese(bool isSourceJpGame)
        {
            //If built from the Script/Japanese folder
            if (!isSourceJpGame) { Program.OptionMenu(); }

            Console.Title = "Extracting JP Scripts";

            if (string.IsNullOrEmpty(Program.jpGameDataPath))
            {
                Tools.WriteLine("Japanese game install path isn't set, please enter it.", ConsoleColor.Yellow);
                Tools.Write("COM3D2.exe path:", ConsoleColor.White);
                string path = Console.ReadLine();
                Program.jpGameDataPath = Path.Combine(path, "GameData");
            }

            //Don't bother if the GameData can't be found
            if (!Directory.Exists(Program.jpGameDataPath))
            {
                Tools.WriteLine($"{Program.jpGameDataPath} doesn't exist, aborting extraction", ConsoleColor.Red);
                Program.OptionMenu();
            }

            string jpCachePath = Path.Combine(Program.cacheFolder, Program.jpCacheFile);
            string arcHistoryJson = Path.Combine(Program.archistoryFolder, jpArchistoryFile);

            if (File.Exists(jpCachePath))
            {
                Console.WriteLine("Loading extraction history");
                jpCache = Cache.LoadJson(jpCache, jpCachePath);
                Tools.WriteLine($"{jpCache.Count} scripts already cached", ConsoleColor.Green);

                if (File.Exists(arcHistoryJson))
                {
                    arcHistoryJP = Cache.LoadJson(arcHistoryJP, arcHistoryJson);
                    Tools.WriteLine($"{arcHistoryJP.Count} .arc already scanned.", ConsoleColor.Green);
                    Console.WriteLine("Only new or updated .arc will be scanned");
                }
            }


            int newScripts = 0;
            int newLines = 0;
            string[] arcs = Directory.GetFiles(Program.jpGameDataPath, "*.*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f) == ".arc").ToArray();

            foreach (string arc in arcs)
            {
                //skip already parsed .arc if they haven't changed size
                string arcName = Path.GetFileName(arc);
                long arcFileSize = new FileInfo(arc).Length;

                if (arcHistoryJP.ContainsKey(arcName))
                {
                    if (arcHistoryJP[arcName] == arcFileSize)
                        continue;
                    else
                        arcHistoryJP.Remove(arcName);
                }

                //ignore ChuBLips stuff
                if (arcName.Contains("_cbl") && Program.isIgnoreCbl) { continue; }

                Tools.Write($"{arcName}: ", ConsoleColor.Gray);

                //opening the .arc file
                ArcFile arcFile = new ArcFile(arc);

                int scriptScannedNb = 0;

                //getting all .ks and their string content
                foreach (ScriptFile script in arcFile.GetAllScripts())
                {
                    //Console.Write($", [{script.Name}]");
                    for (int i = 0; i < script.Lines.Count(); i++)
                    {
                        //Jp doesn't support subtitles, so I don't bother checking for them.
                        //written dialogues always start by "@talk"
                        if (script.Lines[i].StartsWith("@talk", StringComparison.InvariantCultureIgnoreCase))
                        {
                            script.CaptureTalk(i);
                        }

                        //Choice type boxes
                        else if (script.Lines[i].StartsWith("@ChoicesSet", StringComparison.InvariantCultureIgnoreCase) || script.Lines[i].StartsWith("@VRChoicesSet", StringComparison.InvariantCultureIgnoreCase))
                        {
                            script.CaptureChoice(i);
                        }

                        //Yotogi Message is a new kind of text found only in Yotigi+ scripts
                        else if (script.Lines[i].StartsWith("@YotogiMessage", StringComparison.InvariantCultureIgnoreCase))
                        {
                            script.CaptureYotogiMessage(i);
                        }
                    }

                    //get all parsed lines and add them to the cache, making sure they are unique
                    List<string> newJpTalks = script.GetJpTalks();

                    if (jpCache.ContainsKey(script.Name))
                    {
                        jpCache[script.Name].AddRange(newJpTalks);
                        jpCache[script.Name] = jpCache[script.Name].Distinct().ToList();
                        newLines += jpCache[script.Name].Count;
                    }
                    else
                    {
                        if (newJpTalks.Count > 0) //avoid empty scripts
                            jpCache.Add(script.Name, newJpTalks);
                    }

                    scriptScannedNb++;
                    newScripts++;

                }
                Tools.WriteLine($"{scriptScannedNb} scripts", ConsoleColor.Green);
                arcHistoryJP.Add(arcName, arcFileSize);
                Cache.SaveJson(arcHistoryJP, arcHistoryJson, false);
            }

            //adding loose script files
            if (Directory.Exists(Program.japaneseScriptFolder))
            {
                var looseScripts = Directory.GetFiles(Program.japaneseScriptFolder, "*.txt*", SearchOption.AllDirectories);
                if (looseScripts.Length != 0)
                {
                    Console.WriteLine($"Found {looseScripts.Length} loose Japanese scripts.");
                    foreach (string script in looseScripts)
                    {
                        string scriptname = $"{Path.GetFileNameWithoutExtension(script)}.ks";
                        string[] lines = File.ReadAllLines(script);

                        if (jpCache.ContainsKey(scriptname) && lines.Any())
                        {
                            jpCache[scriptname].AddRange(lines.Select(l => l.Trim()));   
                            jpCache[scriptname] = jpCache[scriptname].Distinct().ToList();
                        }
                        else if(lines.Any())
                        {
                            jpCache.Add(scriptname, lines.Select(l => l.Trim()).ToList());
                        }
                    }
                }
            }

            Console.WriteLine("Saving Jp Cache.");
            Cache.SaveJson(jpCache, jpCachePath, false);
            Tools.WriteLine($"{newLines} new/updated lines in {newScripts} new/updated scripts\n\n", ConsoleColor.Green);
        }
    } 

    internal class ScriptFile
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public string[] Lines { get; set; }
        public List<(string Jp, string Eng)> Talks { get; set; } = new List<(string Jp, string Eng)> ();
        public List<(string Jp, string Eng)> NPCs { get; set; } = new List<(string Jp, string Eng)> ();
        public List<SubtitleData> Subs { get; set;} = new List<SubtitleData> ();

        internal ScriptFile(string name, string content)
        {
            Name = name;
            Content = content;
            Lines = content.Split('\n')
                           .Where(l => !l.StartsWith(";"))
                           .Select(l => l.Trim())
                           .ToArray();
        }

        internal void CaptureSubtitleFile(int i, ArcFile arcFile)
        { 
            //getting the subtitle file and loading it
            int pos = 0;
            if ((pos = Lines[i].IndexOf("file=", StringComparison.InvariantCultureIgnoreCase)) > 0)
            {
                var fileName = Lines[i].Substring(pos + 5).Trim();
                ScriptFile subScript = arcFile.GetScript(fileName);

                //get the first voice played, as it seems to be the starting point of all subtitles
                while (i < Lines.Length && !Lines[i].StartsWith("@PlayVoice")) { i++; }

                string voice = "";
                if ((pos = Lines[i].IndexOf("voice=", StringComparison.InvariantCultureIgnoreCase)) > 0)
                {
                    voice = Lines[i].Substring(pos + 6).Replace("wait", "").Trim();
                }

                //parse the subtitle script
                for (int j = 0; j < subScript.Lines.Count(); j++)
                {
                    (int Start, int End) timing;
                    if (subScript.Lines[j].StartsWith("@talk", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // get the timings
                        string[] talkTiming = subScript.Lines[j].Substring("@talk".Length).Trim('[', ']', ' ').Split('-');
                        timing.Start = int.Parse(talkTiming[0]);
                        timing.End = int.Parse(talkTiming[1]);

                        //Capture the JP and ENG text
                        j++;
                        StringBuilder sb = new StringBuilder();
                        while (!subScript.Lines[j].StartsWith("@hitret", StringComparison.InvariantCultureIgnoreCase))
                        {
                            sb.Append(subScript.Lines[j]);
                            j++;
                        }

                        (string Jp, string Eng) line = SplitTranslation(sb.ToString());

                        //shove everything in an i18nEx compatible subtitle format
                        SubtitleData subTitleData = new SubtitleData
                         {
                             original = line.Jp,
                             translation = line.Eng,
                             startTime = timing.Start,
                             displayTime = timing.End - timing.Start,
                             voice = voice,
                             isCasino = false
                         };
                         Subs.Add(subTitleData);                        
                    }
                }
            }
        }

        internal void CaptureSubtitle(int i)
        {
            //Check CaptureSubtitlesFiles() it works nearly the same
            bool isCasino = false;
            (string Jp, string Eng) line = (string.Empty, string.Empty);


            //getting text with regex this time as it's nested in "quotes"
            if (Lines[i].ToLower().Contains("text="))
            {
                //MatchCollection matchCollection = Regex.Matches(Lines[i], "\"(.*?)\"");
                Match match = Regex.Match(Lines[i], @"text=""(.*?)""");

                line = SplitTranslation(match.Groups[1].Value);

                isCasino = Lines[i].ToLower().Contains("mode_c");
            }

            while (!Lines[i].Contains("@PlayVoice")) { i++; }

            string voice = "";
            int pos;
            if ((pos = Lines[i].IndexOf("voice=", StringComparison.InvariantCultureIgnoreCase)) > 0)
            {
                voice = Lines[i].Substring(pos + 6).Replace("wait", "").Trim();
            }

            var subData = new SubtitleData
            {
                original = line.Jp,
                translation = line.Eng,
                isCasino = isCasino,
                voice = voice
            };

            Subs.Add(subData);
        }

        internal void CaptureTalk(int i)
        {
            //In some cases a NPC name can be specified
            var talkLine = Lines[i];
            int pos = 0;
            if ((pos = talkLine.IndexOf("name=", StringComparison.InvariantCultureIgnoreCase)) > 0)
            {
                var name = talkLine.Substring(pos + 5);
                if (!name.StartsWith("["))
                {
                    if (name.ToLower().Contains("real="))
                    {
                        int realPos = name.IndexOf("real=", StringComparison.CurrentCultureIgnoreCase);
                        name = name.Substring(0, realPos - 1).Replace("\"", "").Trim();                            
                    }
                    NPCs.Add(SplitTranslation(name.Trim('\"')));
                }                    
            }

            //Capture the JP text and ENG
            i++;
            StringBuilder sb = new StringBuilder();
            while(!Lines[i].StartsWith("@", StringComparison.InvariantCultureIgnoreCase))
            {
                sb.Append(Lines[i]);
                i++;
            }
            (string Jp, string Eng) line = SplitTranslation(sb.ToString());
            Talks.Add(line);
            //Console.WriteLine($"\t\tJP:{line.Jp}, ENG:{line.Eng}");
        }

        internal void CaptureChoice(int i)
        {
            //getting text with regex this time as it's nested in "quotes"
            if (Lines[i].ToLower().Contains("text="))
            {
                //string choiceText = Lines[i].Substring(Lines[i].IndexOf("text=", StringComparison.CurrentCultureIgnoreCase));

                Match match = Regex.Match(Lines[i], @"text=""(.*?)""");


                var line = SplitTranslation(match.Groups[1].Value);
                Talks.Add(line);

                /*
                MatchCollection match = Regex.Matches(Lines[i], @"text=""(.*?)""");

                if (match.Count > 0)
                {
                    var line = SplitTranslation(match[0].Value);
                    Talks.Add(line);
                }
                */
            }
        }

        internal void CaptureYotogiMessage(int i)
        {
            //getting text with regex this time as it's nested in "quotes"
            if (Lines[i].ToLower().Contains("text="))
            {
                Match match = Regex.Match(Lines[i], @"text=""(.*?)""");

                var line = SplitTranslation(match.Groups[1].Value);
                Talks.Add(line);
            }
        }

        private (string Jp, string Eng) SplitTranslation(string text)
        {
            int pos;
            if ((pos = text.IndexOf("<e>", StringComparison.InvariantCultureIgnoreCase)) > 0)
            {
                var japanese = text.Substring(0, pos).Trim();
                var english = text.Substring(pos + 3).Replace("…", "...").Replace("<E>", "").Trim(); //had to add <E> replace because of Kiss <E><E> errors 
                return (japanese, english);
            }

            return (text.Trim(), string.Empty);
        }

        internal void SaveToCache(string cachePath, bool isNPC)
        {
            string[] content;

            if (isNPC)
            {
                content = NPCs.Distinct()
                              .Where(t => !EngScriptExtraction.shortOfficialCache.Contains($"{t.Jp}\t{t.Eng}") && !string.IsNullOrEmpty(t.Eng))
                              .Select(t => $"{t.Jp}\t{t.Eng}").ToArray();
            }
            else
            {
                content = Talks.Distinct()
                              .Where(t => !EngScriptExtraction.shortOfficialCache.Contains($"{t.Jp}\t{t.Eng}") && !string.IsNullOrEmpty(t.Eng))
                              .Select(t => $"{t.Jp}\t{t.Eng}").ToArray();
            }

            File.AppendAllLines(cachePath, content);
            EngScriptExtraction.shortOfficialCache.UnionWith(content);
        }

        internal void SaveSubtitles()
        {
            if (Subs.Count <= 0) return;


            Tools.MakeFolder(Path.Combine(Program.cacheFolder, "Subtitles"));
            string path = $"{Path.Combine(Program.cacheFolder, "Subtitles", Path.GetFileNameWithoutExtension(Name))}.txt";
            string[] formatedSubs = Subs.Where(s => !string.IsNullOrEmpty(s.original) || !string.IsNullOrEmpty(s.translation))
                                        .Select(s => $"@VoiceSubtitle{JsonConvert.SerializeObject(s)}")
                                        .ToArray();

            File.WriteAllLines (path, formatedSubs);
        }

        internal List<string> GetJpTalks()
        {
            return Talks.Where(t => !string.IsNullOrEmpty(t.Jp))
                        .Select(t => t.Jp.Trim())
                        .Distinct()
                        .ToList();
        }
    }

    internal class ArcFile
    {
        private readonly ArcFileSystem arc = new ArcFileSystem();

        internal ArcFile(string path)
        {
            arc.LoadArc(path);            
        }

        internal List<ScriptFile> GetAllScripts()
        {
            return (from KeyValuePair<string, ArcFileEntry> arcEntry in arc.Files.Where(f => Path.GetExtension(f.Value.Name) == ".ks")
                    let pointer = arcEntry.Value.Pointer.Decompress() //Looks like all scripts are compressed, won't hurt if they aren't
                    let textData = GetEncoding(pointer.Data) ? Encoding.UTF8.GetString(pointer.Data) : Encoding.GetEncoding(932).GetString(pointer.Data) //I need to check what encoding it's in since Kiss started to use TF8 in addition to ShiftJS
                    select new ScriptFile(arcEntry.Value.Name, textData)).ToList();
        }

        internal ScriptFile GetScript(string fileName)
        {
            fileName = $"{fileName}".ToLower();

            var scripts = (from KeyValuePair<string, ArcFileEntry> arcEntry in arc.Files.Where(f => Path.GetFileNameWithoutExtension(f.Value.Name) == fileName)
                           let pointer = arcEntry.Value.Pointer.Decompress()
                           let textData = GetEncoding(pointer.Data) ? Encoding.UTF8.GetString(pointer.Data) : Encoding.GetEncoding(932).GetString(pointer.Data)
                           select new ScriptFile(arcEntry.Value.Name, textData)).ToArray();

            return scripts[0];            
        }

        //Kiss and Unity use a BOM at the start of scripts to know if it's encoded in UTF8 (EF BB BF or as numbers 239 187 191)  
        private bool GetEncoding(byte[] sjis_bytes)
        {
            if (sjis_bytes != null && sjis_bytes.Length >= 3 && sjis_bytes[0] == 239 && sjis_bytes[1] == 187 && sjis_bytes[2] == 191)
                return true;
            else 
                return false;
        }
    }

    internal class SubtitleData
    {
        public int addDisplayTime = 0;
        public int displayTime = -1;
        public bool isCasino;
        public string original = string.Empty;
        public int startTime;
        public string translation = string.Empty;
        public string voice = string.Empty;
    }
}
