using System.Collections.Generic;
using System.IO;

namespace DinkModelPathFixer {
    public class LogFile {

        private FileStream _fileStream;
        private StreamWriter _streamWriter;

        public LogFile(string name) {
            this._fileStream = new FileStream($"{name}.txt", FileMode.Create, FileAccess.Write);
            this._streamWriter = new StreamWriter(this._fileStream);
        }

        public void WriteLine(string s) {
            this._streamWriter.WriteLine(s);
        }

        public void Flush() {
            this._streamWriter.Flush();
        }

        public void Close() {
            this._streamWriter.Close();
            this._fileStream.Close();
        }
    }
}
