using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ShowNameFormatter {
    public static class Program {
        private static bool NoSeasons;
        private static bool PrintOnly;
        private static bool IgnoreBadRead;
        private static bool SkipPreformattedCheck;

        private static string? ShowName;
        private static string CWD = "";

        private static uint FirstEpisodeNo = 1;
        private static uint EpisodeNo = 0;

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
            if (PrintOnly) {
                Console.WriteLine("Reminder, this program was run in PrintOnly mode, so no actual changes were made. Press ENTER to exit.");
                Console.Read();
            }
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

            ShowName = Argument.Show.ParsePath();
            CWD = Argument.Dir.ParsePath() ?? Directory.GetCurrentDirectory();
            FirstEpisodeNo = Argument.FirstEpisode.ParseVariable<uint>();
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