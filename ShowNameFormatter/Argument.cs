namespace ShowNameFormatter {
    internal struct Argument {
        private static readonly string[] Args = Program.Args;

        internal static List<Argument> Arguments => new() {
                Help,
                NoSeasons,
                PrintOnly,
                IgnoreBadRead,
                Force,
                Show,
                Dir,
                FirstEpisode,
            };
        internal static Argument Help => new("help", "Show this text!");
        internal static Argument NoSeasons => new("noseasons",
                "This show does not have seasons. All episodes are assumed to be directly in the main show folder. Otherwise, will attempt to find season folders in the format \"Season <number>\"",
                "Seasons logic is disabled. Will parse all files in current folder.");
        internal static Argument PrintOnly => new("printonly",
                "Will print file names only and will not make any changes.",
                "Seasons logic is disabled. Will parse all files in current folder.");
        internal static Argument IgnoreBadRead => new("ignorebadread",
                "Will ignore instances where the program can't access an episode file and skip to the next (not recommended while MakeMKV is running a rip).",
                "Ignoring file access errors.");
        internal static Argument Force => new("force",
                "Skips preformatted check and renames all episodes in order. Make sure your episodes are in the order you want them!",
                "Skipping preformatted check and will rename all episodes in order.");
        internal static Argument Show => new("show",
                "Parsing show {0}.",
                "Manually specify show name, otherwise will just assume the the current folder is the show.",
                "ShowName");
        internal static Argument Dir => new("dir",
                "Manually specify working directory (should be the main show directory). Otherwise will just use the current directory.",
                "Setting {0} as working directory.",
                "Directory");
        internal static Argument FirstEpisode => new("first",
            "Manually set the first episode number for a folder that contains episodes that don't start at 1.",
            "Beginning with episode {0}",
            "Number");

        internal string Name { get; private set; }
        internal string HelpDescription { get; private set; }
        internal string EnableMessage { get; private set; }
        internal string ValueName { get; private set; }

        internal Argument(string flag, string helpDescription = "", string enableMessage = "", string valueName = "") {
            Name = flag;
            HelpDescription = helpDescription;
            if (!string.IsNullOrWhiteSpace(enableMessage)) EnableMessage = enableMessage;
            else EnableMessage = HelpDescription;
            ValueName = valueName;
        }

        public override string ToString() {
            string v = string.Empty;
            if (!string.IsNullOrWhiteSpace(ValueName)) v = $"<{ValueName}> ";
            return $"--{Name} {v}- {HelpDescription}";
        }

        internal bool ParseFlag() {
            try {
                if (HasArg()) {
                    bool value = true;
                    if (!string.IsNullOrWhiteSpace(EnableMessage)) Console.WriteLine(EnableMessage);
                    return value;
                }
            } catch (Exception e) {
                Console.WriteLine($"Couldn't parse argument {Name}.");
                Console.WriteLine(e);
            }
            return false;
        }

        internal T? ParseVariable<T>() {
            int argIndex = GetArgIndex();
            if (argIndex == -1) return default;

            T? value = default;
            try {
                string variable = Args[argIndex + 1];

                if (string.IsNullOrWhiteSpace(variable) || variable[..2].Equals("--")) {
                    Console.WriteLine($"Expected a variable after argument {Name}. Ignoring.");
                    return default;
                }

                if (typeof(T) != typeof(string)) value = (T)Convert.ChangeType(variable, typeof(T));
            } catch (Exception) {
                Console.WriteLine($"Expected a variable of type {typeof(T).Name} after argument {Name}. Ignoring.");
                return default;
            }

            if (!string.IsNullOrWhiteSpace(EnableMessage)) Console.WriteLine(string.Format(EnableMessage, value));
            return value;
        }

        internal string? ParsePath() {
            string? path = ParseVariable<string>();
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!Directory.Exists(path)) {
                Console.WriteLine($"Couldn't parse argument {Name}. Expected the name of an existing directory.");
                return null;
            } return path;
        }

        private int GetArgIndex() {
            string n = Name;
            string? a = Args.FirstOrDefault(x => x[0] == '-' && x.Contains(n, StringComparison.InvariantCultureIgnoreCase));
            if (string.IsNullOrWhiteSpace(a)) return -1;
            return Array.IndexOf(Args, a);
        }

        private bool HasArg() {
            return GetArgIndex() >= 0;
        }
    }
}
