

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DinkModelPathFixer {
    class Program {

        private static LogFile _logFile = new LogFile(DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_log");
        private static int _count_ERR_TEXTURE_NOT_FOUND = 0;
        private static int _count_ERR_RELATIVE_PATH = 0;
        private static int _count_ERR_OFFSET = 0;

        public static void Main(string[] args) {
            string path = @".";
            if (args.Length > 0 && Directory.Exists(args[0])) {
                path = args[0];
            }
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            
            if (dirInfo.Exists == false) {
                LogAndConsole($"Path not found at \"{path}\"! Exiting...");
                _logFile.Close();
                return;
            }

            // get all the models in location
            List<FileInfo> models = FindFiles(dirInfo, new Dictionary<string, bool>() {
                [".max"] = true,
            });
            Console.WriteLine($"Found {models.Count} model files!...");
            
            // get all the textures in location
            List<FileInfo> textures = FindFiles(dirInfo, new Dictionary<string, bool>() {
                [".tif"] = true,
                [".psd"] = true,
                [".tga"] = true,
                [".eps"] = true,
                [".bmp"] = true,
            });
            Console.WriteLine($"Found {textures.Count} texture files!...");

            // get texture dictionary
            Dictionary<string, List<FileInfo>> textureDictionary = GetTextureDictionary(textures);

            // process mdoel files...
            int index = 0;
            foreach (FileInfo model in models) {
                index++;
                LogAndConsole($"Processing file {index}/{models.Count}...");
                ProcessModelFile(model, textureDictionary);
                LogAndConsole($"");
            }

            LogAndConsole("");
            LogAndConsole("...ALL DONE!");
            LogAndConsole("");
            LogAndConsole("Total errors found:");
            LogAndConsole($"    ERR_TEXTURE_NOT_FOUND  {_count_ERR_TEXTURE_NOT_FOUND}");
            LogAndConsole($"    ERR_OFFSET             {_count_ERR_OFFSET}");
            LogAndConsole($"    ERR_RELATIVE_PATH      {_count_ERR_RELATIVE_PATH}");
            _logFile.Close();
        }

        private static void LogAndConsole(string errormsg) {
            Console.WriteLine(errormsg);
            _logFile.WriteLine(errormsg);
        }

        private static void LogModelProcessingError(string messageHeader, string message, string modelName) {
            _logFile.WriteLine(messageHeader + " - " + message);
            Console.WriteLine($"ERROR - {modelName}");
            Console.WriteLine(messageHeader + " - " + message);
        }

        private static void ProcessModelFile(FileInfo model, Dictionary<string, List<FileInfo>> textureDictionary) {
            if (model.Extension.ToLowerInvariant() != ".max") {
                return;
            }
            
            _logFile.WriteLine($"Model File = \"{model.FullName}\" ");

            int maxPath = 260;
            
            using FileStream fs = new FileStream(model.FullName, FileMode.Open, FileAccess.ReadWrite);
            byte[] buffer = new byte[fs.Length];
            for (int i = 0; i < fs.Length; i++) {
                buffer[i] = (byte)fs.ReadByte();
            }
            
            for (int i = 0; i < buffer.Length -4; i++) {
                // the old models use unicode path encoding...
                
                if (buffer[i] >= 0x41 && buffer[i] <= 0x5A &&     // is A..Z
                    buffer[i+1] == 0x00 &&                        // is null
                    buffer[i+2] == 0x3A &&                        // is :
                    buffer[i+3] == 0x00 &&                        // is null
                    buffer[i+4] == 0x5C &&                        // is \
                    buffer[i+5] == 0x00 ) {                       // is null
                    // this is the start of an absolute file path...
                    _logFile.WriteLine($"    Found absolute path at index={i}!");
                    int endOffset = -1;
                    for (int j = 6; j < maxPath && i+j < buffer.Length; j+=2) {
                        // look for end marker, must be 0x40 followed by 0x12
                        if (buffer[i + j] == 0x40 && buffer[i + j + 1] == 0x12) {
                            endOffset = j;
                            break;
                        }
                    }
                    if (endOffset < 0) {
                        LogModelProcessingError( "        ERR_OFFSET", $"Could not find end marker for path at index={i}!", model.FullName);
                        _count_ERR_OFFSET++;
                        continue;
                    }
                    
                    // found a valid absolute path an we know where it ends!
                    byte[] unicodeStringBytes = new byte[endOffset];
                    for (int j = 0; j < endOffset; j++) {
                        unicodeStringBytes[j] = buffer[i + j];
                    }
                    
                    if (TryReplacePath(unicodeStringBytes, model, textureDictionary)) {
                        // if replace was successful, write it back into the file!
                        for (int j = 0; j < endOffset; j++) {
                            buffer[i + j] = unicodeStringBytes[j];
                        }
                    }
                }
            }

            fs.Seek(0, SeekOrigin.Begin);
            fs.Write(buffer, 0, buffer.Length);
            fs.Flush();
            fs.Close();
        }

        private static string GetRelativePath(FileInfo relativeTo, FileInfo texture) {
            try {
                string relPath = Path.GetRelativePath(relativeTo.Directory.FullName, texture.Directory.FullName);
                relPath += Path.DirectorySeparatorChar;
                relPath += texture.Name;

                if (Path.IsPathRooted(relPath)) {
                    // could not get relative path...
                    return "";
                }
                
                return relPath;
            } catch (Exception ex) {
                // could not get relative path...
                return "";
            }
        }

        private static bool TryReplacePath(byte[] unicodeStringBytes, FileInfo model, Dictionary<string, List<FileInfo>> textureDictionary) {
            
            UnicodeEncoding unicode = new UnicodeEncoding();
            
            string path = unicode.GetString(unicodeStringBytes);
            _logFile.WriteLine($"        HardcodedPath=\"{path}\"");
            string[] split = path.Split('\\');

            string fileName = split[split.Length - 1];
            string fileNameLower = fileName.ToLowerInvariant();

            // see if texture file exists...
            if (textureDictionary.ContainsKey(fileNameLower) == false) {
                LogModelProcessingError("        ERR_TEXTURE_NOT_FOUND",$"Texture=\"{fileName}\" - Could not find this texture!", model.FullName);
                _count_ERR_TEXTURE_NOT_FOUND++;
                return false;
            }

            int index = 0;

            if (textureDictionary[fileNameLower].Count > 1) {
                // need to identify the right file out of many...
                // do so by seeing how many elements their paths have in common...
                
                int max = 0;
                int maxIndex = -1;
                for (int i = 0; i < textureDictionary[fileNameLower].Count; i++) {
                    string pathT = textureDictionary[fileNameLower][i].FullName;
                    string[] splitT = pathT.Split('\\');
                    int matchDepth = 0;
                    for (int j = 0; j < split.Length-1; j++) {
                        string str1 = split[split.Length - j - 1];
                        string str2 = splitT[splitT.Length - j - 1];
                        if (str1 == str2) {
                            matchDepth = j + 1;
                        }
                    }
                    if (matchDepth > max) {
                        max = matchDepth;
                        maxIndex = i;
                    }
                }

                if (maxIndex >= 0) {
                    index = maxIndex;
                }
            }

            FileInfo replacement = textureDictionary[fileNameLower][index];
            string newPath = GetRelativePath(model, replacement);
            
            _logFile.WriteLine($"        RelativePath=\"{newPath}\"");
            
            if (string.IsNullOrWhiteSpace(newPath)) {
                LogModelProcessingError("        ERR_RELATIVE_PATH",$"Texture=\"{fileName}\" - Could not get relative path!", model.FullName);
                _count_ERR_RELATIVE_PATH++;
                return false;
            }

            byte[] newBytes = unicode.GetBytes(newPath);
            if (newBytes.Length > unicodeStringBytes.Length) {
                LogModelProcessingError("        ERR_RELATIVE_PATH",$"Texture=\"{fileName}\" - Relative path does not fit!", model.FullName);
                _count_ERR_RELATIVE_PATH++;
                return false;
            }

            // copy relative path
            for (int i = 0; i < newBytes.Length; i++) {
                unicodeStringBytes[i] = newBytes[i];
            }

            // clear remaining bytes
            for (int i = newBytes.Length; i < unicodeStringBytes.Length; i++) {
                unicodeStringBytes[i] = 0x00;
            }

            return true;
        }

        private static Dictionary<string, List<FileInfo>> GetTextureDictionary(List<FileInfo> files) {
            Dictionary<string, List<FileInfo>> dictionary = new Dictionary<string, List<FileInfo>>();

            foreach (FileInfo file in files) {
                string nameLower = file.Name.ToLowerInvariant();
                if (dictionary.ContainsKey(nameLower)) {
                    LogAndConsole($"NOTE - Duplicate texture file name found! {nameLower}");
                    dictionary[nameLower].Add(file);
                } else {
                    dictionary.Add(nameLower,new List<FileInfo>());
                    dictionary[nameLower].Add(file);
                }
            }

            return dictionary;
        }
        
        private static List<FileInfo> FindFiles(DirectoryInfo rootDir, Dictionary<string,bool> extensions) {
            Queue<DirectoryInfo> dirQueue = new Queue<DirectoryInfo>();
            List<FileInfo> foundFiles = new List<FileInfo>();
            dirQueue.Enqueue(rootDir);

            while (dirQueue.Count > 0) {
                DirectoryInfo dir = dirQueue.Dequeue();
                FileInfo[] files = dir.GetFiles();
                for (int i = 0; i < files.Length; i++) {
                    string ext = files[i].Extension.ToLowerInvariant();
                    if (extensions.ContainsKey(ext)) {
                        foundFiles.Add(files[i]);
                    }
                }

                DirectoryInfo[] dirs = dir.GetDirectories();
                for (int i = 0; i < dirs.Length; i++) {
                    dirQueue.Enqueue(dirs[i]);
                }
            }

            return foundFiles;
        } 
    }
}
