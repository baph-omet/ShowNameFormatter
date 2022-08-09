using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ShowNameFormatter {
    public static class Program {
        private static bool UseSeasons = true;
        private static bool PrintOnly = false;
        private static bool IgnoreBadRead = false;

        private static string ShowName = string.Empty;
        private static string CWD = Directory.GetCurrentDirectory();

        public static void Main(string[] args) {
            ParseArgs(args);

            Console.WriteLine("Beginning parsing.");

            if (string.IsNullOrWhiteSpace(ShowName)) {
                ShowName = Path.GetFileName(CWD) ?? throw new NullReferenceException("Weird path shennanigans.");

                Regex ShowNameParser = new(@"\s\(\d{4}\)");
                ShowName = ShowNameParser.Replace(ShowName, string.Empty);
            }

            if (UseSeasons) {
                List<string> seasons = Directory.GetDirectories(CWD, "Season *").ToList();
                foreach (string s in seasons) {
                    int seasonNo = Convert.ToInt32(Path.GetFileName(s)?.Split(" ")[1]);
                    Directory.GetFiles(s, "*.mkv")
                        .Where(x => !IsPreformatted(x, seasonNo))
                        .OrderBy(x => x).ToList()
                        .ForEach(x => ConvertEpisode(x, seasonNo));
                }
            } else {
                Directory.GetFiles("*.mkv")
                    .Where(x => !IsPreformatted(x))
                    .OrderBy(x => x).ToList()
                    .ForEach(x => ConvertEpisode(x));
            }

            Console.WriteLine($"Finished processing episodes. Check the files and Plex to make sure everything copied okay!");
            if (PrintOnly) Console.WriteLine("Reminder, this program was run in PrintOnly mode, so no actual changes were made.");
            Console.ReadKey();
        }

        private static void ParseArgs(string[] args) {
            if (args.Contains("--help", StringComparer.InvariantCultureIgnoreCase)) {
                PrintHelp();
                Environment.Exit(0);
            }

            if (args.Contains("--noseasons", StringComparer.InvariantCultureIgnoreCase)) {
                UseSeasons = false;
                Console.WriteLine("Seasons logic is disabled. Will parse all files in current folder.");
            }

            if (args.Contains("--printonly", StringComparer.InvariantCultureIgnoreCase)) {
                PrintOnly = true;
                Console.WriteLine("Will print file names only and will not make any changes.");
            }

            if (args.Contains("--ignorebadread", StringComparer.InvariantCultureIgnoreCase)) {
                IgnoreBadRead = true;
                Console.WriteLine("Will ignore instances where the program can't access an episode file and skip to the next (not recommended while MakeMKV is running a rip).");
            }

            string argNameShow = "--show";
            if (args.Contains(argNameShow, StringComparer.InvariantCultureIgnoreCase)) {
                string arg = args.First(x => x.Equals(argNameShow, StringComparison.InvariantCultureIgnoreCase));
                int argIndex = Array.IndexOf(args, arg);
                try {
                    string show = args[argIndex + 1];
                    if (show.Contains("--")) throw new ArgumentException("Expected a name of show.", nameof(args));
                    ShowName = show;
                    Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), ShowName));
                } catch (Exception e) {
                    throw new ArgumentException("Expected a name of show.", nameof(args), e);
                }
            }

            string argNameDir = "--dir";
            if (args.Contains(argNameDir, StringComparer.InvariantCultureIgnoreCase)) {
                string arg = args.First(x => x.Equals(argNameDir, StringComparison.InvariantCultureIgnoreCase));
                int argIndex = Array.IndexOf(args, arg);
                try {
                    string dir = args[argIndex + 1];
                    if (dir.Substring(0, 1).Equals("--") || !Directory.Exists(dir)) throw new ArgumentException("Expected a directory.", nameof(args));
                    CWD = dir;
                } catch (Exception e) {
                    throw new ArgumentException("Expected a directory.", nameof(args), e);
                }
            }
        }

        private static void PrintHelp() {
            new List<string>() {
                "Converts files outputted by MakeMKV and makes them digestible by Plex media server.",
                "Arguments:",
                "--help - Show this text!",
                "--show <ShowName> - Manually specify show name, otherwise will just assume the the current folder is the show.",
                "--dir <Directory> - Manually specify working directory (should be the main show directory). Otherwise will just use the current directory.",
                "--noseasons - This show does not have seasons. All episodes are assumed to be directly in the main show folder. Otherwise, will attempt to find season folders in the format \"Season <number>\"",
                "--printonly - Doesn't execute any filesystem actions and just prints runtime data instead.",
                "--ignorebadread - Will ignore instances where the program can't access an episode file and skip to the next (not recommended while MakeMKV is running a rip)."
            }.ForEach(x => Console.WriteLine(x));
        }

        private static void ConvertEpisode(string path, int? season = null) {
            string fileName = Path.GetFileNameWithoutExtension(path);

            bool cannotAccess = false;
            try {
                using FileStream stream = new FileInfo(path).Open(FileMode.Open, FileAccess.Read, FileShare.None);
                stream.Close();
            } catch (IOException) {
                cannotAccess = true;
            } catch (AccessViolationException) {
                cannotAccess = true;
            }

            if (cannotAccess) {
                Console.Write($"Cannot access episode {path}. It might still be being written. ");
                if (IgnoreBadRead) {
                    Console.WriteLine("Skipping.");
                    return;
                } else {
                    Console.WriteLine("Stopping to avoid episode order mis-match.");
                    Environment.Exit(5);
                }
            }

            if (IsPreformatted(path, season)) {
                Console.WriteLine($"Found preformatted episode: {fileName}. Skipping.");
                return;
            }

            Console.WriteLine($"Found unformatted episode: {fileName}. Beginning conversion.");

            StringBuilder newNameBuilder = new($"{ShowName} ");
            if (season != null) newNameBuilder.Append($"s{season}");
            newNameBuilder.Append($"e{GetNextEpisodeNumber(season)}.mkv");
            string newPath = Path.Combine(
                    Path.GetDirectoryName(path) 
                        ?? throw new ArgumentNullException(nameof(path)),
                    newNameBuilder.ToString());

            Console.WriteLine($"Moving to new file: {newPath}");
            if (!PrintOnly) File.Move(
                path,
                newPath, 
                true
            );

            Console.WriteLine($"Processing finished for episode: {newNameBuilder}");
        }

        private static bool IsPreformatted(string path, int? season = null) {
            string fileName = Path.GetFileNameWithoutExtension(path);
            Regex PreFormattedParser;
            if (season == null) {
                PreFormattedParser = new Regex($@"{ShowName} e\d+");
                if (PreFormattedParser.IsMatch(fileName)) return true;
            } else {
                PreFormattedParser = new Regex(@$"{ShowName} s\d+e\d+");
                if (PreFormattedParser.IsMatch(fileName)) return true;
            }

            return false;
        }

        private static int GetNextEpisodeNumber(int? season = null) {
            string dir = CWD;
            if (season != null) dir = Path.Combine(dir, $"Season {season}");

            List<string> files = Directory.GetFiles(dir, "*.mkv").Where(x => IsPreformatted(x, season)).ToList();

            Regex EpisodeParser = new(@"e\d+");
            int highestEpisodeNo = files.Max(x => Convert.ToInt32(EpisodeParser.Match(x).Value[1..]));
            return highestEpisodeNo + 1;
        }
    }
}