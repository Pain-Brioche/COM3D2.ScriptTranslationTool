using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace COM3D2.ScriptTranslationTool
{
    internal static class ScriptManagement
    { 
        /// <summary>
        /// List Already Translated Script files.
        /// </summary>
        internal static Dictionary<string, string> List()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();

            if (Directory.Exists(Program.i18nExScriptFolder))
            {
                foreach (string file in Directory.EnumerateFiles(Program.i18nExScriptFolder))
                {
                    string fileName = Path.GetFileName(file);
                    dict.Add(fileName, file);
                }
            }

            return dict;
        }
        
        internal static void CreateSortedFolders()
        {
            string parentPath = Directory.GetParent(Program.i18nExScriptFolder).FullName;
            parentPath = Directory.GetParent(parentPath).FullName;

            if (Directory.Exists(Program.i18nExScriptFolder))
            {
                string newPath = $"{parentPath} ({DateTime.Now:dd.mm.yyyy hhmmss})";
                Directory.Move(Program.i18nExScriptFolder, newPath);
            }


            // Make folders to sort files in
            foreach (KeyValuePair<string, string> keyValuePair in SortedFolder.Dict)
            {
                Tools.MakeFolder(Path.Combine(Program.i18nExScriptFolder, keyValuePair.Value));
            }
            Tools.MakeFolder(Path.Combine(Program.i18nExScriptFolder, "[UnCategorized]"));
        }
        
        /// <summary>
        /// Save translated lines to i18nEx sorted scripts.
        /// </summary>
        internal static void AddTo(ILine line)
        {
            string savedString = Tools.FormatLine(line.Japanese, line.English);
            string folder = "[UnCategorized]";

            foreach (KeyValuePair<string, string> kvp in SortedFolder.Dict)
            {
                if (line.FilePath.StartsWith(kvp.Key))
                {
                    folder = kvp.Value;
                    break;
                }
            }

            string fileName = $"{Path.GetFileNameWithoutExtension(line.FilePath)}.txt";

            string path = Path.Combine(Program.i18nExScriptFolder, folder, fileName);
            File.AppendAllText(path, savedString);
        }

        internal static void SaveTxt(string scriptName, IEnumerable<string> lines, List<string> failures)
        {
            string folder = "[UnCategorized]";

            foreach (KeyValuePair<string, string> kvp in SortedFolder.Dict)
            {
                if (scriptName.StartsWith(kvp.Key))
                {
                    folder = kvp.Value;
                    break;
                }
            }



            string fileName = $"{Path.GetFileNameWithoutExtension(scriptName)}.txt";
            string path = Path.Combine(Program.i18nExScriptFolder, folder, fileName);

            //Implementing failure checks
            //File.AppendAllLines(path, lines);

            bool ok = TryAppendAllLines(path, lines);

            if (!ok)
                failures.Add(fileName);
            else
                Console.WriteLine($"Writing: {fileName}");
        }

        private static bool TryAppendAllLines(string path, IEnumerable<string> lines)
        {
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    File.AppendAllLines(path, lines);
                    return true; // success
                }
                catch (Exception)
                {
                    // small delay before retrying
                    Thread.Sleep(500 * attempt);
                }
            }
            
            return false; // failure
        }
    }


    internal static class SortedFolder
    {
        internal readonly static Dictionary<string, string> Dict = new Dictionary<string, string>
        {
            {"a1_", "Muku"},
            {"b1_", "Majime"},
            {"c1_", "Rindere"},
            {"d1_", "Bookworm"},
            {"e1_", "Koakuma"},
            {"f1_", "LadyLike"},
            {"g1_", "Secretary"},
            {"h1_", "Imouto"},
            {"j1_", "Wary"},
            {"k1_", "Ojousama"},
            {"l1_", "Osananajime"},
            {"m1_", "Masochist"},
            {"n1_", "Haraguro"},
            {"p1_", "Gyaru" },
            {"v1_", "Kimajime"},
            {"w1_", "Kisakude"},
            {"a_", "Tsundere"},
            {"b_", "Kuudere"},
            {"c_", "Pure"},
            {"d_", "Yandere"},
            {"e_", "Onee-chan"},
            {"f_", "Genki"},
            {"g_", "Do-S"},
            {"crc_ck_", "[Commands & Choices]"},
            {"ck_sex", "[Commands & Choices]"},
            {"ck_h_", "[Commands & Choices]"},
            {"ck_dance_", "[Commands & Choices]"},
            {"ck_cas_", "[Commands & Choices]"},
            {"lifemode", "[Misc]\\Lifemode"},
            {"idol", "[Misc]\\Idol"},
            {"scout", "[Misc]\\Scout"},
            {"npc", "[Misc]\\NPC"},
            {"harem", "[Misc]\\Harem"},
            {"yuri", "[Misc]\\Yuri"},
            {"pj", "[Misc]\\Pajama Collab"},
            {"cw", "[Misc]\\Camping Event"},
            {"rehire", "[Misc]\\Extra Maids rehire"},
            {"club_gp", "[Misc]\\GP01 Club route"},
            {"xmas", "[Misc]\\Xmas"}
        };
    }
}
