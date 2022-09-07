using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShowNameFormatter {
    [Serializable]
    public class ConversionMapping {
        public string OldPath { get; set; }
        public string NewPath { get; set; }
        public ConversionMapping(string oldpath, string newpath) {
            OldPath = oldpath;
            NewPath = newpath;
        }
    }
    public static class Program {
        private static bool NoSeasons;
        private static bool PrintOnly;
        private static bool IgnoreBadRead;
        private static bool SkipPreformattedCheck;
        private static bool NoBackup;

        private static string? ShowName;
        private static string CWD = "";

        private static Regex? SeasonFilter;
        private static Regex? EpisodeFilter;

        private static uint FirstEpisodeNo = 1;
        private static uint EpisodeNo = 0;

        private static readonly List<ConversionMapping> ConversionPairs = new();

        internal static string[] Args = Array.Empty<string>();

        internal static void Main(string[] args) {
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
                    .Where(x => EpisodeFilter == null || EpisodeFilter.IsMatch(x))
                    .OrderBy(x => x).ToList()
                    .ForEach(x => ConvertEpisode(x));
            } else {
                List<string> seasons = Directory.GetDirectories(CWD, "Season *")
                    .Where(x=>SeasonFilter == null || SeasonFilter.IsMatch(x))
                    .ToList();
                foreach (string s in seasons) {
                    int seasonNo = Convert.ToInt32(Path.GetFileName(s)?.Split(" ")[1]);
                    EpisodeNo = GetFirstEpisodeNumber(seasonNo);
                    Directory.GetFiles(s, "*.mkv")
                        .Where(x => !IsPreformatted(x, seasonNo))
                        .Where(x => EpisodeFilter == null || EpisodeFilter.IsMatch(x))
                        .OrderBy(x => x).ToList()
                        .ForEach(x => ConvertEpisode(x, seasonNo));
                }
            }

            if (ConversionPairs.Any() && !NoBackup) {
                File.WriteAllText(Path.Combine(CWD, $"ConversionBackup-{DateTime.Now:yyyyMMdd-HHmmss}.json"), JsonSerializer.Serialize(ConversionPairs));
            }

            Console.WriteLine($"Finished processing episodes. Check the files and your media server to make sure everything copied okay!");
            if (PrintOnly) {
                Console.WriteLine("Reminder, this program was run in PrintOnly mode, so no actual changes were made. Press ENTER to exit.");
                Console.Read();
            }
#if DEBUG
            Console.Write("Press ENTER to exit.");
            Console.Read();
#endif
        }

        private static void ParseArgs() {
            if (Argument.Help.ParseFlag()) {
                PrintHelp();
                Environment.Exit(0);
            }

            NoSeasons = Argument.NoSeasons.ParseFlag();
            PrintOnly = Argument.PrintOnly.ParseFlag();
            IgnoreBadRead = Argument.IgnoreBadRead.ParseFlag();
            SkipPreformattedCheck = Argument.Force.ParseFlag();
            NoBackup = Argument.NoBackup.ParseFlag();

            ShowName = Argument.Show.ParsePath();
            CWD = Argument.Dir.ParsePath() ?? Directory.GetCurrentDirectory();
            FirstEpisodeNo = Argument.FirstEpisode.ParseVariable<uint>();

            string? epFilter = Argument.EpisodeFilter.ParseVariable<string>();
            if (!string.IsNullOrWhiteSpace(epFilter)) EpisodeFilter = new Regex(epFilter);
            string? sFilter = Argument.SeasonFilter.ParseVariable<string>();
            if (!string.IsNullOrWhiteSpace(sFilter)) SeasonFilter = new Regex(sFilter);

            if (Argument.Undo.ParseFlag()) {
                Undo();
                Environment.Exit(0);
            }
        }

        private static void PrintHelp() {
            ProgramAttributes att = Assembly.GetAssembly(typeof(Program))?.GetCustomAttribute<ProgramAttributes>() ?? throw new NullReferenceException();

            List<string> lines = new() {
                $"--- {att.FriendlyName} v{att.Version} ---",
                att.Description,
                $"Created by {att.Author} ({att.Company})",
                att.Site,
                "Arguments:",
            };
            lines.AddRange(Argument.Arguments.Select(x => x.ToString()));
            lines.ForEach(x => Console.WriteLine(x));
        }

        private static void Undo() {
            List<string> allBackups = Directory.GetFiles(CWD,"ConversionBackup*.json").OrderByDescending(x=>x).ToList();
            List<string>? backups = new();

            if (allBackups.Count <= 0) {
                Console.WriteLine($"No backups found in directory {CWD}.");
                return;
            }

            if (allBackups.Count == 1) {
                backups.Add(allBackups.First());
            } else {
                Console.WriteLine("Multiple backups found. Select the state to back up to and the program will work backwards to that point.");
                for (int i = 0; i < allBackups.Count; i++) {
                    Console.WriteLine($"{i} - {allBackups[i]}");
                }

                int index = -1;
                while (index < 0) {
                    Console.WriteLine($"Type the backup state to roll back to and hit ENTER. [0-{allBackups.Count - 1}]. Type a negative number to cancel.");
                    try {
                        index = Convert.ToInt32(Console.ReadLine());
                        if (index < 0) return;
                    } catch {
                        Console.WriteLine("Invalid input.");
                    }
                }

                for (int i = 0; i <= index; i++) {
                    backups.Add(allBackups[i]);
                }
            }

            foreach (string backup in backups) {
                string convName = Path.GetFileNameWithoutExtension(backup);
                Console.WriteLine($"Rolling back to conversion {convName}");
                List<ConversionMapping> conversions = null;
                try {
                    conversions = JsonSerializer.Deserialize<List<ConversionMapping>>(File.ReadAllText(backup));
                } catch {
                    Console.WriteLine($"Cannot deserialize backup found at {backup}. Fix the JSON and try again.");
                    return;
                }

                if (conversions == null) {
                    Console.WriteLine($"Cannot deserialize backup found at {backup}. Fix the JSON and try again.");
                    return;
                }

                foreach (ConversionMapping cm in conversions) {
                    File.Move(cm.NewPath, cm.OldPath);
                    Console.WriteLine($"Rolled back file {cm.NewPath} to {cm.OldPath}.");
                }
                Console.WriteLine($"Finished rolling back to conversion {convName}.");
            }

            Console.WriteLine("Rollback complete.");
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

            string newName = GetEpisodeName(season);
            string newPath = Path.Combine(
                    Path.GetDirectoryName(path)
                        ?? throw new ArgumentNullException(nameof(path)),
                    newName);

            FileExistsAction? fea = null;
            if (File.Exists(newPath)) {
                while (true) {
                    Console.WriteLine($"File {newPath} already exists. What do you want to do? [(o)verride, (s)kip, (k)eep]");
                    string input = Console.ReadLine() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(input)) {
                        char first = input.ToLower()[0];
                        if (first == 'o') {
                            fea = FileExistsAction.Overwrite;
                            break;
                        }

                        if (first == 'k') {
                            fea = FileExistsAction.Keep;
                            break;
                        }

                        if (first == 's') {
                            fea = FileExistsAction.Skip;
                            break;
                        }
                    }
                }
            }

            if (fea != FileExistsAction.Skip) {
                Console.WriteLine($"Moving to new file: {newPath}");
                if (!PrintOnly) {
                    if (fea == null || fea == FileExistsAction.Overwrite) {
                        if (fea == FileExistsAction.Overwrite) Console.WriteLine("Overriding existing file.");
                        File.Move(
                            path,
                            newPath,
                            true
                        );
                    } else if (fea == FileExistsAction.Keep) {
                        File.Move(
                            path,
                            $"{Path.GetFileNameWithoutExtension(newName)} (1){Path.GetExtension(newPath)}",
                            true);
                    }
                }

                ConversionPairs.Add(new(path, newPath));
                EpisodeNo++;
            }

            Console.WriteLine($"Processing finished for episode: {newName}");
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
            if (files.Count > 0) highestEpisodeNo = files.Max(x => Convert.ToInt32(EpisodeParser.Match(x).Value[1..]));
            return Math.Max((uint)highestEpisodeNo + 1, FirstEpisodeNo);
        }

        private static string GetEpisodeName(int? season = null) {
            StringBuilder newNameBuilder = new($"{ShowName} ");
            if (season != null) newNameBuilder.Append($"s{season}");
            newNameBuilder.Append($"e{EpisodeNo}.mkv");
            return newNameBuilder.ToString();
        }

        private enum FileExistsAction {
            Overwrite,
            Keep,
            Skip,
        }
    }
}