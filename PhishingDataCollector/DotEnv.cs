namespace PhishingDataCollector
{
    using System;
    using System.IO;

    public static class DotEnv
    {
        public static void Load(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            foreach (var line in File.ReadAllLines(filePath))
            {
                var parts = line.Split(new char [] {'='}, StringSplitOptions.RemoveEmptyEntries); 

                if (parts.Length != 2)
                    continue;
                parts[0] = parts[0].Trim();
                parts[1] = parts[1].Trim();
                Environment.SetEnvironmentVariable(parts[0], parts[1]);
            }
        }
    }
}