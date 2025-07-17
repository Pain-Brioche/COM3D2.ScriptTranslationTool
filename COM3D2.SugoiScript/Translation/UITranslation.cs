using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace COM3D2.ScriptTranslationTool
{
    internal static class UITranslation
    {
        internal static void Process(ref int csvCount, ref int termCount)
        {

            if (Directory.Exists(Program.UIExportFolder))
            {
                string newPath = $"{Program.UIExportFolder} ({DateTime.Now:dd-mm-yyyy hhmmss})";
                Directory.Move(Program.UIExportFolder, newPath);
            }

            Tools.MakeFolder(Program.UIExportFolder);
            string[] csvs = Directory.GetFiles(Program.UISourceFolder, "*.csv*", SearchOption.AllDirectories);
            List<string> errorCsv = new List<string>();
            
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
                    if (Program.currentExport != Program.ExportFormat.JaT)
                        csvOutput.Add(string.Join(",", parser.ReadFields()));

                    //While EoF isn't reached.
                    while (!parser.EndOfData)
                    {
                        termCount++;

                        string[] values;

                        //We parse the line
                        try
                        {
                            values = parser.ReadFields();
                        }
                        catch (Exception)
                        {
                            Tools.WriteLine($"line {parser.ErrorLineNumber} of {csv} cannot be parsed, this .csv will be ignored.", ConsoleColor.Red);
                            csvOutput.Clear();
                            errorCsv.Add($"{csv} line {parser.ErrorLineNumber}");
                            break;
                        }

                        if (values.Length < 5)
                        {
                            Tools.WriteLine($"{string.Join(",", values)} has less than the 5 required entries", ConsoleColor.Red);
                            errorCsv.Add($"{csv} Term {termCount}");
                            continue;
                        }

                        //The japanese is always the third index
                        string japanese = values[3].Trim();
                        if (string.IsNullOrEmpty(japanese)) continue;

                        //Check for translation placeholder
                        if (values[0] == values[4]) values[4] = "";

                        //Some line can already be translated
                        if (!string.IsNullOrEmpty(values[4]))
                        {
                            csvOutput.Add(GetExportString(values, csv));
                        }

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
                        csvOutput.Add(GetExportString(values, csv));
                    }
                }

                //Write the .csv
                if (csvOutput.Count > 0)
                {
                    string newPath = csv.Replace("UI\\Source", Program.UIExportFolder);
                    Tools.MakeFolder(Path.GetDirectoryName(newPath));
                    File.AppendAllLines(newPath, csvOutput);
                }

                Tools.WriteLine($"{termCount} Terms translated.", ConsoleColor.Magenta);

                //saving every 20 .csv
                if (csvCount % 20 == 0 )
                    Db.SaveToJson();
            }

            //report on files with errors
            if (errorCsv.Count > 0)
            {
                Tools.WriteLine("\nThose files returned an error:", ConsoleColor.Yellow);
                foreach (var line in errorCsv)
                    Tools.WriteLine(line, ConsoleColor.Red);
            }

            Db.SaveToJson();
        }

        private static string GetExportString(string[] values, string csv)
        {
            string csvExportString;
            string[] escapedValues = values.Select(v => EscapeCharacters(v)).ToArray();

            if (Program.currentExport != Program.ExportFormat.JaT)
            {
                csvExportString = string.Join(",", escapedValues);
            }
            else
            {
                csvExportString = $"\"{Path.GetFileNameWithoutExtension(csv)}/{escapedValues[0]}\",\"{escapedValues[3]}\",\"{escapedValues[4]}\"";
            }
                
            return csvExportString;
        }

        private static string EscapeCharacters(string str)
        {
            return str.Replace("\"", "\"\"");
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
