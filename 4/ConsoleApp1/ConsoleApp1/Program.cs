using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ParallelFileReader
{
    public class FileSpaceCounter
    {
        public static async Task<(int totalSpaces, long elapsedMs)> CountSpacesInFilesAsync(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Папка не найдена: {folderPath}");

            var files = Directory.GetFiles(folderPath);
            if (files.Length == 0)
                return (0, 0);

            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task<int>[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                tasks[i] = Task.Run(() => CountSpacesInFile(filePath));
            }

            var results = await Task.WhenAll(tasks);
            int totalSpaces = 0;

            foreach (var count in results)
            {
                totalSpaces += count;
            }

            stopwatch.Stop();
            return (totalSpaces, stopwatch.ElapsedMilliseconds);
        }

        private static int CountSpacesInFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                int spaceCount = 0;

                foreach (char c in content)
                {
                    if (c == ' ')
                        spaceCount++;
                }

                return spaceCount;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static async Task<(int totalSpaces, long elapsedMs)> CountSpacesInFilesLineByLineAsync(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Папка не найдена: {folderPath}");

            var files = Directory.GetFiles(folderPath);
            if (files.Length == 0)
                return (0, 0);

            var stopwatch = Stopwatch.StartNew();
            var totalSpaces = 0;
            var fileTasks = new Task[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                fileTasks[i] = Task.Run(async () =>
                {
                    int fileSpaces = await CountSpacesInFileLineByLineAsync(filePath);
                    Interlocked.Add(ref totalSpaces, fileSpaces);
                });
            }

            await Task.WhenAll(fileTasks);
            stopwatch.Stop();
            return (totalSpaces, stopwatch.ElapsedMilliseconds);
        }

        private static async Task<int> CountSpacesInFileLineByLineAsync(string filePath)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
                var lineTasks = new Task<int>[lines.Length];
                int fileSpaces = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    lineTasks[i] = Task.Run(() =>
                    {
                        int lineSpaces = 0;
                        foreach (char c in line)
                        {
                            if (c == ' ')
                                lineSpaces++;
                        }
                        return lineSpaces;
                    });
                }

                var results = await Task.WhenAll(lineTasks);
                foreach (var count in results)
                {
                    fileSpaces += count;
                }

                return fileSpaces;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static void CreateTestFiles(string folderPath, int fileCount, int linesPerFile)
        {
            Directory.CreateDirectory(folderPath);

            var random = new Random();
            var tasks = new Task[fileCount];

            for (int i = 0; i < fileCount; i++)
            {
                int fileIndex = i;
                tasks[i] = Task.Run(() =>
                {
                    string filePath = Path.Combine(folderPath, $"test_{fileIndex}.txt");
                    using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

                    for (int j = 0; j < linesPerFile; j++)
                    {
                        int spacesInLine = random.Next(5, 50);
                        string line = new string(' ', spacesInLine) + $"Line {j} " + new string(' ', spacesInLine / 2);
                        writer.WriteLine(line);
                    }
                });
            }

            Task.WaitAll(tasks);
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string testFolder = "TestFiles";
            int fileCount = 50;
            int linesPerFile = 100;

            Console.WriteLine("Создание тестовых файлов...");
            FileSpaceCounter.CreateTestFiles(testFolder, fileCount, linesPerFile);
            Console.WriteLine($"Создано {fileCount} файлов по {linesPerFile} строк\n");

            Console.WriteLine("=== Метод 1: Один файл - одна задача ===");
            var (spaces1, time1) = await FileSpaceCounter.CountSpacesInFilesAsync(testFolder);
            Console.WriteLine($"Пробелов: {spaces1:N0}");
            Console.WriteLine($"Время: {time1} мс");
            Console.WriteLine($"Среднее: {time1 / (double)fileCount:F2} мс/файл\n");

            Console.WriteLine("=== Метод 2: Одна строка - одна задача ===");
            var (spaces2, time2) = await FileSpaceCounter.CountSpacesInFilesLineByLineAsync(testFolder);
            Console.WriteLine($"Пробелов: {spaces2:N0}");
            Console.WriteLine($"Время: {time2} мс");
            Console.WriteLine($"Среднее: {time2 / (double)fileCount:F2} мс/файл");
            Console.WriteLine($"Строк обработано: {fileCount * linesPerFile:N0}\n");

            Console.WriteLine("=== Сравнение производительности ===");
            Console.WriteLine($"Разница в пробелах: {Math.Abs(spaces1 - spaces2)}");
            Console.WriteLine($"Относительная разница: {Math.Abs(time1 - time2) / (double)Math.Max(time1, time2) * 100:F1}%");

            if (time1 < time2)
                Console.WriteLine($"Метод 1 быстрее на {time2 - time1} мс ({time2 / (double)time1:F2}x)");
            else if (time2 < time1)
                Console.WriteLine($"Метод 2 быстрее на {time1 - time2} мс ({time1 / (double)time2:F2}x)");
            else
                Console.WriteLine("Время одинаковое");

            try
            {
                Directory.Delete(testFolder, true);
                Console.WriteLine($"\nТестовая папка '{testFolder}' удалена");
            }
            catch { }

            Console.WriteLine("\nНажмите любую клавишу...");
            Console.ReadKey();
        }
    }
}