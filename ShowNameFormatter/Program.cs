using System.Text;
using System.Text.RegularExpressions;

namespace ShowNameFormatter {
    public static class Program {
        private static bool NoSeasons;
        private static bool PrintOnly;
        private static bool IgnoreBadRead;
        private static bool SkipPreformattedCheck;

        private static string? ShowName;
        private static string CWD = Directory.GetCurrentDirectory();

        private static uint FirstEpisodeNo = 1;
        private static uint EpisodeNo = 0;

        private static string[] Args = Array.Empty<string>();

        public static void Main(string[] args) {
#if DEBUG
            if (args.Length == 0) {
                Console.WriteLine("Type arguments and hit ENTER.");
                Args = (Console.ReadLine() ?? string.Empty).Split(' ');
            }
#else
            Args = args;
#endif
            ParseArgs();

            Console.WriteLine("Beginning parsing.");

            if (string.IsNullOrWhiteSpace(ShowName)) {
                ShowName = Path.GetFileName(CWD) ?? throw new NullReferenceException("Weird path shennanigans.");

                Regex ShowNameParser = new(@"\s\(\d{4}\)");
                ShowName = ShowNameParser.Replace(ShowName, string.Empty);
            }

            if (NoSeasons) {
                Directory.GetFiles(CWD, "*.mkv")
                    .Where(x => !IsPreformatted(x))
                    .OrderBy(x => x).ToList()
                    .ForEach(x => ConvertEpisode(x));
            } else {
                List<string> seasons = Directory.GetDirectories(CWD, "Season *").ToList();
                foreach (string s in seasons) {
                    int seasonNo = Convert.ToInt32(Path.GetFileName(s)?.Split(" ")[1]);
                    EpisodeNo = GetFirstEpisodeNumber(seasonNo);
                    Directory.GetFiles(s, "*.mkv")
                        .Where(x => !IsPreformatted(x, seasonNo))
                        .OrderBy(x => x).ToList()
                        .ForEach(x => ConvertEpisode(x, seasonNo));
                }
            }

            Console.WriteLine($"Finished processing episodes. Check the files and Plex to make sure everything copied okay!");
            if (PrintOnly) Console.WriteLine("Reminder, this program was run in PrintOnly mode, so no actual changes were made.");
        }

        private static void ParseArgs() {
            ParseFlag("--help", onFound: () => { 
                PrintHelp();
                Environment.Exit(0);
                return true;
            });

            NoSeasons = ParseFlag("--noseasons", "Seasons logic is disabled. Will parse all files in current folder.");
            PrintOnly = ParseFlag("--printonly", "Will print file names only and will not make any changes.");
            IgnoreBadRead = ParseFlag("--ignorebadread", "Will ignore instances where the program can't access an episode file and skip to the next (not recommended while MakeMKV is running a rip).");
            SkipPreformattedCheck = ParseFlag("--force", "Skipping preformatted check and will rename all episodes in order.");

            ShowName = ParseVariable<string>("--show", "Parsing show {0}.", x => {
                if (!Directory.Exists(x)) throw new ArgumentException("Expected an existing directory.", nameof(Args), new DirectoryNotFoundException(x));
                return x;
            });

            string? cwd = ParseVariable<string>("--dir", "Setting {0} as working directory.", x => {
                if (!Directory.Exists(x)) throw new ArgumentException("Expected an existing directory.", nameof(Args), new DirectoryNotFoundException(x));
                return x;
            });
            if (!string.IsNullOrEmpty(cwd)) CWD = cwd;

            FirstEpisodeNo = ParseVariable<uint>("--firstep", "Beginning with episode {0}");
        }

        private static bool ParseFlag(string argName, string message = "", Func<bool>? onFound = null) {
            if (Args.Contains(argName, StringComparer.InvariantCultureIgnoreCase)) {
                try {
                    bool ret = true;
                    if (onFound != null) ret = onFound();
                    if (!string.IsNullOrWhiteSpace(message)) Console.WriteLine(message);
                    return ret;
                } catch (Exception e) {
                    Console.WriteLine($"Unhandled exception encountered when attempting to parse argument {argName}.");
                    Console.WriteLine(e);
                }
            }

            return false;
        }

        private static T? ParseVariable<T>(string argName, string message = "", Func<T,T>? onFound = null) {
            if (!Args.Contains(argName, StringComparer.InvariantCultureIgnoreCase)) return default;

            string arg = Args.First(x => x.Equals(argName, StringComparison.InvariantCultureIgnoreCase));
            int argIndex = Array.IndexOf(Args, arg);
            T convertedValue;
            try {
                string value = Args[argIndex + 1];

                if (string.IsNullOrWhiteSpace(value) || value[..2].Equals("--")) {
                    Console.WriteLine($"Expected a variable after argument {argName}. Ignoring.");
                    return default;
                }

                convertedValue = (T)Convert.ChangeType(value, typeof(T));
            } catch (Exception) {
                Console.WriteLine($"Expected a variable of type {typeof(T).Name} after argument {argName}. Ignoring.");
                return default;
            }

            try {
                if (onFound!=null) convertedValue = onFound(convertedValue);
                if (!string.IsNullOrWhiteSpace(message)) Console.WriteLine(string.Format(message, convertedValue));
                return convertedValue;
            } catch (Exception e) {
                Console.WriteLine($"Unhandled exception encountered when attempting to parse argument {argName}.");
                Console.WriteLine(e);
                return default;
            }
        }

        private static void PrintHelp() {
            new List<string>() {
                "Converts files outputted by MakeMKV and makes them digestible by Plex media server.",
                "Arguments:",
                "--help - Show this text!",
                "--show <ShowName> - Manually specify show name, otherwise will just assume the the current folder is the show.",
                "--dir <Directory> - Manually specify working directory (should be the main show directory). Otherwise will just use the current directory.",
                "--firstep <Number> - Manually set the first episode number for a folder that contains episodes that don't start at 1.",
                "--noseasons - This show does not have seasons. All episodes are assumed to be directly in the main show folder. Otherwise, will attempt to find season folders in the format \"Season <number>\"",
                "--printonly - Doesn't execute any filesystem actions and just prints runtime data instead.",
                "--ignorebadread - Will ignore instances where the program can't access an episode file and skip to the next (not recommended while MakeMKV is running a rip).",
                "--force - Skips preformatted check and renames all episodes in order. Make sure your episodes are in the order you want them!",
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
            newNameBuilder.Append($"e{GetNextEpisodeNumber()}.mkv");
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
            if (SkipPreformattedCheck) return false;

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

        private static uint GetFirstEpisodeNumber(int? season) {
            string dir = CWD;
            if (season != null) dir = Path.Combine(dir, $"Season {season}");

            List<string> files = Directory.GetFiles(dir, "*.mkv").Where(x => IsPreformatted(x, season)).ToList();

            Regex EpisodeParser = new(@"e\d+");
            int highestEpisodeNo = 0;
            if (files.Count>0) highestEpisodeNo = files.Max(x => Convert.ToInt32(EpisodeParser.Match(x).Value[1..]));
            return Math.Max((uint)highestEpisodeNo + 1, FirstEpisodeNo);
        }

        private static uint GetNextEpisodeNumber() {
            return EpisodeNo++;
        }
    }
}