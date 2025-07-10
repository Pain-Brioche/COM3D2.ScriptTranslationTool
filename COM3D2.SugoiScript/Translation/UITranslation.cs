using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace COM3D2.ScriptTranslationTool
{
    internal static class UITranslation
    {
        internal static void Process(ref int csvCount, ref int termCount)
        {
            Tools.MakeFolder(Program.i18nExUIFolder);
            IEnumerable<string> csvs = Directory.EnumerateFiles(Program.japaneseUIFolder, "*.csv*", SearchOption.AllDirectories);
            
            foreach (string csv in csvs)
            {
                csvCount++; 
                Console.Title = $"Processing ({csvCount} out of {csvs.Count()} scripts)";

                string fileName = Path.GetFileName(csv);

                Tools.WriteLine($"\n-------- {fileName} --------", ConsoleColor.Yellow);

                string csvInput = File.ReadAllText(csv);
                List<string> csvOutput = new List<string>();

                using (var csvReader = new StringReader(csvInput))
                using (var parser = new NotVisualBasic.FileIO.CsvTextFieldParser(csvReader))
                {
                    //First line is always the header, so adding it back as is.
                    csvOutput.Add(string.Join(",", parser.ReadFields()));

                    //While EoF isn't reached.
                    while (!parser.EndOfData)
                    {
                        termCount++;

                        //We parse the line
                        string[] values = parser.ReadFields();

                        //The japanese is always the third index
                        string japanese = values[3].Trim();
                        if (string.IsNullOrEmpty(japanese)) continue;

                        //Skip already translated entries
                        if (!string.IsNullOrEmpty(values[4])) continue;

                        string translation = string.Empty;
                        ConsoleColor color = ConsoleColor.Gray;

                        Console.Write(japanese);
                        Tools.Write(" => ", ConsoleColor.Yellow);

                        Line currentLine = Db.GetLine(japanese);

                        //Get best translation possible Manual > Official > Machine.
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

                        //Adding the translation to value[4] as this is the english index.
                        values[4] = translation;

                        //and those values in the csv
                        csvOutput.Add(string.Join(",", values));
                    }
                }

                //Get the new pathcreate folders and write the file
                string newPath = csv.Replace("UI\\Japanese", Program.i18nExUIFolder);
                Tools.MakeFolder(Path.GetDirectoryName(newPath));
                File.AppendAllLines(newPath, csvOutput);

                csvCount++;
            }
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
