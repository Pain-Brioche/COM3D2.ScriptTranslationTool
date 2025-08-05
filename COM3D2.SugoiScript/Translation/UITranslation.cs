using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace COM3D2.ScriptTranslationTool
{
    internal static class UITranslation
    {
        static List<string> alreadyParsedTerms = new List<string>();
        static readonly int autoSaveTimer = 12 * 60 * 1000;
        static Stopwatch stopwatch;


        internal static void Process(ref int csvCount, ref int termCount)
        {
            //Starting the timer for the AutoSave
            stopwatch = new Stopwatch();
            stopwatch.Start();


            if (Directory.Exists(Program.UIExportFolder))
            {
                string newPath = $"{Program.UIExportFolder} ({DateTime.Now:dd-mm-yyyy hhmmss})";
                Directory.Move(Program.UIExportFolder, newPath);
            }

            Tools.MakeFolder(Program.UIExportFolder);

            string[] csvs = Directory.GetFiles(Program.UISourceFolder, "*.csv*", SearchOption.AllDirectories)
                         .OrderBy(path => path.Contains("ENG") ? 0 : 1)
                         .ToArray();
            
            List<string> errorCsv = new List<string>();
            
            foreach (string csv in csvs)
            {
                CheckAutoSaveTimer();

                csvCount++; 
                Console.Title = $"Processing ({csvCount} out of {csvs.Count()} scripts)";

                string csvFileName = Path.GetFileName(csv);

                Tools.WriteLine($"\n-------- {csvFileName} --------", ConsoleColor.Yellow);

                string csvInput = File.ReadAllText(csv);
                List<string> csvOutput = new List<string>();

                using (var csvReader = new StringReader(csvInput))
                using (var parser = new NotVisualBasic.FileIO.CsvTextFieldParser(csvReader))
                {
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

                            //Getting rid of both chinese entries.
                            if (values.Length > 5)
                            {
                                values = values.Take(5).ToArray();
                            }

                            //The japanese is always the third index
                            string term = values[0].Trim();
                            string japanese = values[3].Trim();
                            string english = values[4].Trim();

                            //Discarding Terms already exported
                            if (alreadyParsedTerms.Contains(term))  continue;

                            //Sometimes both entries are empty, just ignore those
                            if (string.IsNullOrEmpty(japanese) && string.IsNullOrEmpty(english)) continue;

                            //If Only the Japanese entry is missing, pass the entire line as is
                            if (string.IsNullOrEmpty(japanese) && !string.IsNullOrEmpty(english))
                            {
                               csvOutput.Add(GetExportString(values, csv));
                               continue;
                            }                                      

                            //Check for translation placeholder
                            if (term == english) values[4] = "";

                            //Recover eventual translations from the database
                            Line currentLine = Db.GetLine(japanese);

                            //Some line can already be translated
                            if (!string.IsNullOrEmpty(english))
                            {
                                //Adding english to the database
                                TlType tlType = TlType.Ignored;

                                if (csv.Contains(Program.UISourceFolder + "\\ENG"))
                                    tlType = TlType.Official;

                                Db.Add(values[3], values[4], tlType);

                                //Recover eventual manual translations
                                if (!string.IsNullOrEmpty(currentLine.Manual))
                                      values[4] = currentLine.Manual;

                                csvOutput.Add(GetExportString(values, csv));
                                continue;
                            }

                            //Begin the actual translation
                            string translation = string.Empty;
                            ConsoleColor color = ConsoleColor.Gray;

                            Console.Write(japanese);
                            Tools.Write(" => ", ConsoleColor.Yellow);                            

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
                if (csvOutput.Count > 1)
                {
                    //First line is always the header
                    if (Program.currentExport != Program.ExportFormat.JaT)
                        csvOutput[0] = $"Key,Type,Desc,Japanese,English";


                    //First line is always the header, changing it for JaT.
                    if (Program.currentExport == Program.ExportFormat.JaT)
                        csvOutput[0] = $"Term,Original,Translation";

                    //removing additional headers.
                    string newPath = Path.Combine(Program.UIExportFolder, csvFileName);
                    if (File.Exists(newPath))
                        csvOutput.RemoveAt(0);


                    File.AppendAllLines(newPath, csvOutput);
                }

                Tools.WriteLine($"{termCount} Terms translated.", ConsoleColor.Magenta);
            }

            Db.SaveToJson();

            //report on files with errors
            if (errorCsv.Count > 0)
            {
                Tools.WriteLine("\nThose files returned an error:", ConsoleColor.Yellow);
                foreach (var line in errorCsv)
                    Tools.WriteLine(line, ConsoleColor.Red);
            }
        }

        private static string GetExportString(string[] values, string csv)
        {
            alreadyParsedTerms.Add(values[0].Trim());

            string csvExportString;
            string[] escapedValues = values.Select(v => EscapeCharacters(v)).ToArray();

            if (Program.currentExport != Program.ExportFormat.JaT)
            {
                csvExportString = string.Join(",", escapedValues);
            }
            else
            {
                csvExportString = $"{Path.GetFileNameWithoutExtension(csv)}/{escapedValues[0]},{escapedValues[3]},{escapedValues[4]}";
            }
                
            return csvExportString;
        }

        private static string EscapeCharacters(string str)
        {
            bool containsSpecialChars = str.Contains(",") || str.Contains("\"") || str.Contains("\n");

            if (containsSpecialChars)
            {
                // Échappe les guillemets doubles
                str = str.Replace("\"", "\"\"");
                // Entoure la valeur de guillemets
                return $"\"{str}\"";
            }

            return str;
        }

        private static void CheckAutoSaveTimer()
        {
            if (stopwatch.ElapsedMilliseconds > autoSaveTimer)
            {
                Console.WriteLine("\n===========================================================");
                Db.SaveToJson();
                Console.WriteLine("\n===========================================================");
                stopwatch.Restart();
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
