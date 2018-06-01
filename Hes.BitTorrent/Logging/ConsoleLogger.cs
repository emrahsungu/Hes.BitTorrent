namespace Hes.BitTorrent.Logging {

    using System;

    public static class ConsoleLogger {

        public static void WriteLn(object output) {
            Write(output + "\n");
        }

        public static void WriteLn(object obj, string output){
            Write(obj + " " + output + "\n");
        }

        private static void Write(string output){
            Console.Write($"{DateTime.UtcNow:hh:mm:ss.fff} || {output}");
        }
    }
}