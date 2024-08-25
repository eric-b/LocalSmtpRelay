using System.IO;

namespace LocalSmtpRelay
{
    static class Utility
    {
        public static bool TryDeleteFile(FileInfo file)
        {
            try
            {
                file.Delete();
                return true;
            }
            catch 
            {
                return false;
            }
        }
    }
}
