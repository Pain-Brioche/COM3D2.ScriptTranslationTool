using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Newtonsoft.Json;
using System.IO;

namespace COM3D2.ScriptTranslationTool
{
    internal interface ILine
    {
        string FilePath { get; set; }
        string Japanese { get; set; }
        string JapanesePrep { get; set; }
        string English { get; }
        string OfficialTranslation { get; set; }
        string MachineTranslation { get; set; }
        string ManualTranslation { get; set; }
        ConsoleColor Color { get; set; }
        bool HasRepeat { get; set; }
        bool HasError { get; set; }

        void GetTranslation();
    }

    internal class ScriptLine : ILine
    {
        public string OfficialTranslation { get; set; }
        public string MachineTranslation { get; set; }
        public string ManualTranslation { get; set; }

        //Fields bellow are not saved as .json
        [JsonIgnore]
        public string FileName { get; set; }
        [JsonIgnore]
        public string Japanese { get; set; }
        [JsonIgnore]
        public string English
        {
            //returns the first best translation available, otherwise returns an empty string.
            get
            {
                if (!string.IsNullOrEmpty(ManualTranslation))
                {
                    Color = ConsoleColor.Cyan;
                    return ManualTranslation;
                }
                if (!string.IsNullOrEmpty(OfficialTranslation) && !Program.isSafeExport)
                {
                    Color = ConsoleColor.Green;
                    return OfficialTranslation;
                }
                if (!string.IsNullOrEmpty(MachineTranslation))
                {
                    Color = ConsoleColor.DarkBlue;
                    return MachineTranslation;
                }

                Color= ConsoleColor.Red;
                return "";
            }
        }
        [JsonIgnore]
        public string FilePath { get; set; }
        [JsonIgnore]
        public string JapanesePrep { get; set; }
        [JsonIgnore]
        public ConsoleColor Color { get; set; }
        [JsonIgnore]
        public bool HasRepeat { get; set; } = false;
        [JsonIgnore]
        public bool HasError { get; set; } = false;
        [JsonIgnore]
        internal bool HasTag { get; set; } = false;
        [JsonIgnore]
        internal List<string> Tags { get; set; } = new List<string>();

        internal ScriptLine(string fileName, string japanese, string official = "", string machine = "", string manual = "")
        {
            FilePath = fileName;
            FileName = Path.GetFileName(fileName);
            Japanese = japanese.Trim();
            OfficialTranslation = official.Trim();
            MachineTranslation = machine.Trim();
            ManualTranslation = manual.Trim();
        }

        [JsonConstructor]
        internal ScriptLine() { }

        /// <summary>
        /// Preping Japanese for translation later
        /// </summary>
        private void PrepJapanese()
        {            
            // check for name tags [HF], [HF2], ... 
            Regex rx = new Regex(@"\[.*?\]", RegexOptions.Compiled);
            
            MatchCollection matches = rx.Matches(Japanese);
            
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    Tags.Add(match.Groups[0].Value);
                }
            
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

        /// <summary>
        /// Clean all kind of reccurrent syntax error and put back any [HF] tag when needed
        /// </summary>
        internal void CleanPost()
        {
            // replace MUKU by the corresponding [HF] tags
            if (this.HasTag)
            {
                //line.English = Regex.Replace(line.English, @"the tag placeholder", "TAGPLACEHOLDER", RegexOptions.IgnoreCase);

                Regex rx = new Regex(@"\bMUKU\b", RegexOptions.IgnoreCase);

                for (int i = 0; i < this.Tags.Count; i++)
                {
                    this.MachineTranslation = rx.Replace(this.MachineTranslation, this.Tags[i], 1);
                }

                // remove the from before [HF] tags
                this.MachineTranslation = this.MachineTranslation.Replace("the [", "[");
                this.MachineTranslation = this.MachineTranslation.Replace("The [", "[");
            }

            // unk ? Looks like symbols the translator doesn't know how to handle
            this.MachineTranslation = this.MachineTranslation.Replace("<unk>", "");


            // check for repeating characters
            Match matchChar = Regex.Match(this.MachineTranslation, @"(\w)\1{15,}");
            if (matchChar.Success)
            {
                this.HasRepeat = true;
            }

            // check for repating words
            Match matchWord = Regex.Match(this.MachineTranslation, @"(?<word>\w+)(-(\k<word>)){5,}");
            if (matchWord.Success)
            {
                this.HasRepeat = true;
            }

            // check for server bad request
            if (this.MachineTranslation.Contains("400 Bad Request"))
            {
                this.HasError = true;
            }
        }

        public void GetTranslation()
        {
            PrepJapanese();
            MachineTranslation = Translate.ToEnglish(JapanesePrep);
            CleanPost();
        }
    }

    internal class CsvLine : ILine
    {
        public string OfficialTranslation { get; set; }
        public string MachineTranslation { get; set; }
        public string ManualTranslation { get; set; }
        public string ChSimple { get; set; } = string.Empty;
        public string ChTraditional { get; set; } = string.Empty;

        public string FilePath { get; set; }
        public string Key { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string Japanese { get; set; }
        public string JapanesePrep { get; set; }
        public string English
        {
            //returns the first best translation available, otherwise returns an empty string.
            get
            {
                if (!string.IsNullOrEmpty(OfficialTranslation) && !Program.isSafeExport)
                {
                    Color = ConsoleColor.Green;
                    return OfficialTranslation;
                }
                else if (!string.IsNullOrEmpty(ManualTranslation))
                {
                    Color = ConsoleColor.Cyan;
                    return ManualTranslation;
                }

                else if (!string.IsNullOrEmpty(MachineTranslation))
                {
                    Color = ConsoleColor.DarkBlue;
                    return MachineTranslation;
                }

                Color = ConsoleColor.Red;
                return "";
            }
        }
        [JsonIgnore]
        public ConsoleColor Color { get; set; }
        [JsonIgnore]
        public bool HasRepeat { get; set; }
        [JsonIgnore]
        public bool HasError { get; set; }
        [JsonIgnore]
        public string[] Header { get; set; }
        [JsonIgnore]
        private readonly string[] matchString = new string[] {"|info", "|name" };

        public CsvLine(string fileName, string[] header, string[] values)
        {
            FilePath = fileName;
            Header = header;

            Key = values[0];
            Type = values[1];
            Description = values[2];
            Japanese = values[3];
            OfficialTranslation = values[4];
            if (Cache.scriptCache.ContainsKey(Japanese.Trim())) 
                ManualTranslation = Cache.scriptCache[Japanese.Trim()].ManualTranslation;


            /* I consider that if the Key contains |info / |name then the entry must be translated,
             * as it always seems to be the case. */
            if (matchString.Any(Key.Contains))
                OfficialTranslation = string.Empty;


            if (values.Length > 6)
            {
                ChSimple = values[5];
                ChTraditional = values[6];
            }

            //Some UI entries may contain \r, partially breaking the cache as a result...
            if (Japanese.Contains("\r") || Japanese.Contains("\n"))
            {
                string preString = Japanese.Replace("\r", "");
                preString = preString.Replace("\n", "");
                JapanesePrep = preString;
                Japanese = preString;
            }
            else
                JapanesePrep = Japanese;
        }

        /// <summary>
        /// Clean all kind of reccurrent syntax error and put back any [HF] tag when needed
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        internal void CleanPost()
        {
            // unk ? Looks like symbols the translator doesn't know how to handle
            MachineTranslation = MachineTranslation.Replace("<unk>", "");

            // check for repeating characters
            Match matchChar = Regex.Match(MachineTranslation, @"(\w)\1{15,}");
            if (matchChar.Success)
                this.HasRepeat = true;

            // check for repating words
            Match matchWord = Regex.Match(MachineTranslation, @"(?<word>\w+)(-(\k<word>)){5,}");
            if (matchWord.Success)
                this.HasRepeat = true;

            // check for server bad request
            if (MachineTranslation.Contains("400 Bad Request"))
                this.HasError = true;
        }

        public string ExportHeader()
        {
            string headerString = "";

            for (int i = 0; i < Header.Length; i++)
            {
                headerString = $"{headerString},{Header[i]}";
            }
            //remove first comma
            headerString = headerString.Remove(0, 1);
            //add eol
            headerString += "\n";

            return headerString;
        }

        public string ExportLine()
        {
            string line;

            //Don't export faulty translations, also add quotes, even if they don't exist in the original csv.
            if (HasError ||HasRepeat)
                line = $"\"{Key}\",{Type},{Description},\"{Japanese}\",";
            else
                line = $"\"{Key}\",{Type},{Description},\"{Japanese}\",\"{English}\"";

            if (Header.Length == 7)
                line = $"{line},\"{ChSimple}\",\"{ChTraditional}\"";

            return line ;
        }

        public void GetTranslation()
        {
            MachineTranslation = Translate.ToEnglish(JapanesePrep);
            CleanPost();
        }
    }
}
