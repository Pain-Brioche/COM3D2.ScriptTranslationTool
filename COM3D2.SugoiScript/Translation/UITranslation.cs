using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace COM3D2.ScriptTranslationTool
{
    internal static class UITranslation
    {
        internal static void Process()
        {
            Dictionary<string, string[]> tempTermCache = new Dictionary<string, string[]>();

            //trying to load official .json
            string[] jsonFiles =
            {
                "dynamic.json",
                "dance_subtitle.json",
                "parts.json",
                "yotogi.json"
            };

            foreach (string jsonFile in jsonFiles)
            {
                string jsonPath = Path.Combine(Program.cacheFolder, jsonFile);

                if (!File.Exists(jsonPath)) continue;

                string jsonData = File.ReadAllText(jsonPath);

                var jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;

                var translationsTerms = JsonConvert.DeserializeObject<TermDatas>(jsonData, jsonSerializerSettings);

                Console.WriteLine($"{jsonFile} contains {translationsTerms.mTerms.Count} terms.");

                foreach ( var termData in translationsTerms.mTerms )
                {
                    if (!tempTermCache.ContainsKey(termData.Term))
                        tempTermCache.Add(termData.Term, termData.Languages);
                }
                
                /*                if (File.Exists(jsonPath) && !Program.isSafeExport)
                                    {
                                        string[] strings = File.ReadAllLines(jsonPath);
                                        for (int i = 0; i < strings.Length; i++)
                                        {
                                            if (strings[i].Contains("\"Languages\":"))
                                            {
                                                string jp = Regex.Replace(strings[i + 1].Replace("\"", "").Trim().Trim(','), @"\s+", " ");
                                                string eng = strings[i + 2].Replace("\"", "").Trim().Trim(',');

                                                if (!tempCSVCache.ContainsKey(jp) && !string.IsNullOrEmpty(jp))
                                                {
                                                    tempCSVCache.Add(jp, eng);
                                                    Console.WriteLine($"Loaded translation: {jp} {eng}");
                                                }
                                            }
                                        }
                                    }*/
            }

            Tools.MakeFolder(Program.i18nExUIFolder);
            IEnumerable<string> csvs = Directory.EnumerateFiles(Program.japaneseUIFolder, "*.csv*", SearchOption.AllDirectories);
            int csvCount = 1;
            
            foreach (string csv in csvs)
            {
                Tools.WriteLine($"\n-------- {Path.GetFileName(csv)} --------", ConsoleColor.Yellow);

                //reading csv files
                List<CsvLine> csvLines = ParseCSV(csv);

                //let's try to translate each line
                for(int i = 0; i < csvLines.Count; i++)
                {
                    Console.Title = $"Processing: line {i}/{csvLines.Count} from {csvCount}/{csvs.Count()} UI files";

                    var currentLine = csvLines[i];

                    //Some entries may be empty...
                    if (String.IsNullOrWhiteSpace(currentLine.Japanese))
                        continue;

                    //retrieve from Cache
                    string category = Path.GetFileNameWithoutExtension(csv);
                    string key = currentLine.Key;
                    string term = $"{category}/{key}";
                    if (tempTermCache.ContainsKey(term))
                    {
                        currentLine.OfficialTranslation = tempTermCache[term][1];
                        currentLine.Color = ConsoleColor.Green;
                    }

                    Console.Write(currentLine.Japanese);
                    Tools.Write(" => ", ConsoleColor.Yellow);

                    //Translate if needed/possible
                    if (Program.isTranslatorRunning && (string.IsNullOrEmpty(currentLine.English) || (Program.forcedTranslation && string.IsNullOrEmpty(currentLine.MachineTranslation))))
                    {
                        currentLine.GetTranslation();

                        //ignore faulty returns
                        if (currentLine.HasRepeat || currentLine.HasError)
                        {
                            Cache.AddToError(currentLine);
                            Tools.WriteLine($"This line returned a faulty translation and was placed in {Program.errorFile}", ConsoleColor.Red);
                            continue;
                        }
                    }
                    else if (string.IsNullOrEmpty(currentLine.English))
                    {
                        Tools.WriteLine($"This line wasn't found in any cache and can't be translated since sugoi isn't running", ConsoleColor.Red);
                        continue;
                    }               


                    Tools.WriteLine(currentLine.English, currentLine.Color);

                    csvLines[i] = currentLine;
                }

                //Now that it's translated, let's rebuild the .csv

                //Ignore empty files
                if (csvLines.Count == 0) continue;

                //Get the new pathcreate folders and write the header
                string newPath = csv.Replace("UI\\Japanese", Program.i18nExUIFolder);
                Tools.MakeFolder(Path.GetDirectoryName(newPath));
                File.WriteAllText(newPath, csvLines[0].ExportHeader());

                //Write content
                List<string> lines = new List<string>();
                foreach(CsvLine line in csvLines)
                {
                    lines.Add(line.ExportLine());
                }

                File.AppendAllLines(newPath, lines);

                csvCount++;
            }
        }

        //This is using CsvTextFieldParser librabry https://github.com/22222/CsvTextFieldParser
        private static List<CsvLine> ParseCSV(string csvFileInput)
        {
            List<CsvLine> csvLines = new List<CsvLine>();

            string fileName = Path.GetFileName(csvFileInput);
            string csvInput = File.ReadAllText(csvFileInput);

            using (var csvReader = new StringReader(csvInput))
            using (var parser = new NotVisualBasic.FileIO.CsvTextFieldParser(csvReader))
            {
                // Save header to rebuild the csv and know the number of columns.
                string[] header = new string[0];
                if (!parser.EndOfData)
                {
                    header = parser.ReadFields();
                }
                //commented code was here to check where csv parsing broke
                //int i = 1;
                while (!parser.EndOfData)
                {
                    //i++;
                    //Tools.Write($" [{i}] ", ConsoleColor.DarkMagenta);
                    var values = parser.ReadFields();

                    CsvLine line = new CsvLine(fileName, header, values);

                    csvLines.Add(line);
                }
            }
            return csvLines;
        }
    }


    public class TermDatas
    {
        public List<TermData> mTerms { get; set; } = new List<TermData>();

        public class TermData
        {
            public string[] Languages { get; set; }
            public string Term { get; set; }
        }
    }
}
