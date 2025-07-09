using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace COM3D2.ScriptTranslationTool
{
    public static class Db
    {
        internal static Dictionary<string, Line> data = new Dictionary<string, Line>();

        internal static Line GetLine(string japanese)
        {
            if (data.ContainsKey(japanese))
                return data[japanese];
            else
            {
                data[japanese] = new Line();
                return data[japanese];
            }
        }

        internal static void Add(string japanese, string translation = "", TlType type = TlType.Ignored, string scriptFile = "", string csvFile = "")
        { 
            //All Japanese sentences should be trimmed and obviously not empty
            japanese = japanese.Trim();
            if (string.IsNullOrWhiteSpace(japanese)) return;

            //Create the entry in the database then update it
            if (!data.ContainsKey(japanese))
                data.Add(japanese, new Line());

            Update(japanese, translation, type, scriptFile, csvFile);
        }

        private static void Update(string japanese, string translation = "", TlType type = TlType.Ignored, string scriptFile = "", string csvFile = "")
        {
            japanese = japanese.Trim();

            if (data.ContainsKey(japanese))
            {
                // avoid replacing eventual existing translations by empty ones
                if (!string.IsNullOrEmpty(translation))
                {
                    if (type == TlType.Official)
                        data[japanese].Official = translation;
                    else if (type == TlType.Machine)
                        data[japanese].Machine = translation;
                    else if (type == TlType.Manual)
                        data[japanese].Manual = translation;
                }

                //Add script file name if it exists and isn't already there, same for csv
                if (!string.IsNullOrEmpty(scriptFile) && !data[japanese].scriptFiles.Contains(scriptFile))
                    data[japanese].scriptFiles.Add(scriptFile);
                if (!string.IsNullOrEmpty(csvFile) && !data[japanese].csvFiles.Contains(csvFile))
                    data[japanese].csvFiles.Add(csvFile);
            }
        }

        internal static void Remove(string japanese)
        {
            data.Remove(japanese.Trim());
        }

        internal static void ClearMachineTranslations()
        {
            Tools.WriteLine("Are You sure? This will delete ALL machine translation in the database", ConsoleColor.Red);
            Tools.WriteLine("A backup will be created.", ConsoleColor.Red);
            Tools.Write("Enter \"YES\" to proceed, anything else to abort: ", ConsoleColor.Red);
            string answer = Console.ReadLine();

            if (answer.ToLower() == "yes")
            {
                string currentTime = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                string backupPath = $"{Program.cacheFolder}\\({currentTime})_TranslationData_Backup.json";
                File.Move(Program.databaseFile, backupPath);

                data.Values.ToList().ForEach(line => line.Machine = string.Empty);

                SaveToJson();

                Tools.WriteLine("Every Machine translation entries are now cleared.", ConsoleColor.Red);
                Tools.WriteLine($"A Backup [{backupPath}] has been created", ConsoleColor.Red);
            }
            else
            {
                Tools.WriteLine("Aborted", ConsoleColor.Green);
            }

            Console.WriteLine("\n");
            Program.OptionMenu();

        }

        internal static void SaveToJson()
        {
            Tools.WriteLine("Saving Database...", ConsoleColor.White);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(Program.databaseFile, json);

            Tools.WriteLine("Database Saved", ConsoleColor.Green);
        }

        internal static void LoadFromJson()
        {
            if (File.Exists(Program.databaseFile))
            {
                var json = File.ReadAllText(Program.databaseFile);
                data = JsonConvert.DeserializeObject<Dictionary<string, Line>>(json);
            }
        }
    }


    public class Line
    {
        public string Official { get; set; }
        public  string Machine { get; set; }
        public  string Manual { get; set; }
        public List<string> scriptFiles { get; set; } = new List<string>();

        [JsonIgnore]
        public List<string> csvFiles { get; set; } = new List<string>();

        //Used for translation purposes
        [JsonIgnore]
        private string Japanese { get; set; }
        [JsonIgnore]
        private string JapanesePrep { get; set; }
        [JsonIgnore]
        internal bool HasRepeat { get; set; } = false;
        [JsonIgnore]
        internal bool HasError { get; set; } = false;
        [JsonIgnore]
        internal bool HasTag { get; set; } = false;
        [JsonIgnore]
        private List<string> Tags { get; set; } = new List<string>();



        //returns the first best translation available, otherwise returns an empty string.
        internal string GetBestTranslation(ref ConsoleColor color)
        {
            if (!string.IsNullOrEmpty(Manual))
            {
                color = ConsoleColor.Cyan;
                return Manual;
            }
            if (!string.IsNullOrEmpty(Official) && !Program.isSafeExport)
            {
                color = ConsoleColor.Green;
                return Official;
            }
            if (!string.IsNullOrEmpty(Machine))
            {
                color = ConsoleColor.DarkBlue;
                return Machine;
            }

            color = ConsoleColor.Red;
            return string.Empty;
        }

        internal string GetBestTranslation()
        {
            ConsoleColor color = ConsoleColor.Red;
            string tl = GetBestTranslation(ref color);
            return tl;
        }

        // Preping Japanese for translation
        private void PrepJapanese()
        {
            // check for name tags [HF], [HF2], ... 
            Regex rx = new Regex(@"\[.*?\]", RegexOptions.Compiled);

            MatchCollection matches = rx.Matches(Japanese);

            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                    Tags.Add(match.Groups[0].Value);                

                JapanesePrep = Regex.Replace(Japanese, @"\[.*?\]", "MUKU");

                HasTag = true;
            }
            else
            {
                JapanesePrep = Japanese;
            }

            // for the rare lines having quotes
            JapanesePrep = JapanesePrep.Replace("\"", "\\\"");

            // remove ♀ symbol because it messes up sugoi
            JapanesePrep = JapanesePrep.Replace("♀", "");
        }

        // Clean all kind of reccurrent syntax error and put back any [HF] tag when needed
        internal void CleanPost()
        {
            // replace MUKU by the corresponding [HF] tags
            if (this.HasTag)
            {
                //line.English = Regex.Replace(line.English, @"the tag placeholder", "TAGPLACEHOLDER", RegexOptions.IgnoreCase);

                Regex rx = new Regex(@"\bMUKU\b", RegexOptions.IgnoreCase);

                for (int i = 0; i < this.Tags.Count; i++)
                {
                    this.Machine = rx.Replace(this.Machine, this.Tags[i], 1);
                }

                // remove "the" from before [HF] tags
                this.Machine = this.Machine.Replace("the [", "[");
                this.Machine = this.Machine.Replace("The [", "[");
            }

            // unk ? Looks like symbols the translator doesn't know how to handle
            this.Machine = this.Machine.Replace("<unk>", "");


            // check for repeating characters
            Match matchChar = Regex.Match(this.Machine, @"(\w)\1{15,}");
            if (matchChar.Success)
            {
                this.HasRepeat = true;
            }

            // check for repating words
            Match matchWord = Regex.Match(this.Machine, @"(?<word>\w+)(-(\k<word>)){5,}");
            if (matchWord.Success)
            {
                this.HasRepeat = true;
            }

            // check for server bad request
            if (this.Machine.Contains("400 Bad Request"))
            {
                this.HasError = true;
            }
        }

        public string GetTranslation(string japanese)
        {
            Japanese = japanese;
            PrepJapanese();
            Machine = Translate.ToEnglish(JapanesePrep);
            CleanPost();
            return Machine;
        }
    }    


    enum TlType
    {
        Official,
        Machine,
        Manual,
        Ignored
    }
}
