# ShowNameFormatter
Converts ouputted TV Show video files from MakeMKV into file names usable by Plex.

# Usage
## Basic
For basic operation, assuming you have a file structure like this:
```
[Show Name]
 ⮩ Season 1
  ⮩ [Disc Name]_t00.mkv
  ⮩ [Disc Name]_t01.mkv, etc.
 ⮩ Season 2, etc.
```
Just place the executable inside the `Show Name` directory and run. The program will grab the show name and season numbers from your file structure, renaming the `[Disc Name]_tXX.mkv` files to `[Show Name]_sXXeXX.mkv`. Will process files in alphabetical order by filename and will ignore any already-formatted files.
## Advanced
If you wanna get more technical, the program has some arguments.
* `--help` - Shows help. Will be more up-to-date than this README.
* `--show <showName>` - Manually specify the show name, in which case the program will look for a folder in the current working directory by that name to set as the show folder.
* `--dir <directory>` - Manually specify the current working directory. Otherwise will just use the directory where the executable is located.
* `--firstep <number>` - Manually specify the number of the first episode in each season. Mostly for directories that contain partial seasons.
* `--noseasons` - The program will ignore season directories and will parse episodes in the show directory.
* `--printonly` - Does not execute any file system actions and just prints the targets of such actions. Mostly only useful for debugging, as for each episode the program expects the previously processed episodes to already be formatted.
* `--ignorebadread` - Will skip instances where the program has trouble reading a file and will continue to the next in the case of locked/corrupted files. Otherwise, the program will stop when it encounters such an issue. This flag is not recommended while MakeMKV is running a rip as it might produce unexpected results.

# Contributing
If you find any bugs or have suggestions, feel free to leave an issue or pull request!
