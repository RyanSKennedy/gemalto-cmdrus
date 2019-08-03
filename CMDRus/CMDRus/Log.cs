using System;
using System.IO;

namespace MyLogClass
{
    public class Log
    {
        #region Init Params
        private static object sync = new object();
        protected static string fileName;
        #endregion

        public Log(string fName)
        {
            fileName = fName;
        }

        #region Methods: Write
        public static void Write(string eventString)
        {
            try {
                string fullText = string.Format("[{0:dd.MM.yyyy  HH:mm:ss,fff}]  {1}\r\n", DateTime.Now, eventString);
                lock (sync) {
                    File.AppendAllText(fileName, fullText);
                }
            } catch {
                //перехватываем всё и ничего не делаем
            }
        }
        #endregion
    }
}
