using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace NeuralNetwork1
{
    using FastBitmap;

    public enum LetterType : byte { D0 = 0, D1, D2, D3, D4, D5, D6, D7, D8, D9, Undef };

    public class DatasetProcessor
    {
        public static string LetterTypeToString(LetterType type) => (type == LetterType.Undef) ? "Undef" : ((int)type).ToString();

        private const string databaseLocation = "..\\..\\dataset";
        private Random random = new Random();
        public int LetterCount { get; set; }
        private Dictionary<LetterType, List<string>> structure = new Dictionary<LetterType, List<string>>();

        public DatasetProcessor()
        {
            for (int i = 0; i < 10; i++)
            {
                LetterType type = (LetterType)i;
                structure[type] = new List<string>();
                string path = Path.Combine(databaseLocation, LetterTypeToString(type));
                if (Directory.Exists(path)) structure[type].AddRange(Directory.GetFiles(path, "*.png"));
            }
        }

        // Методы getTestDataset и getTrainDataset остаются как в прошлом ответе...
        public SamplesSet getTestDataset(int count)
        {
            SamplesSet set = new SamplesSet();
            for (int type = 0; type < LetterCount; type++)
            {
                var files = structure[(LetterType)type];
                if (files.Count == 0) continue;
                for (int i = 0; i < Math.Min(files.Count, 20); i++)
                    using (var b = new Bitmap(files[random.Next(files.Count)]))
                        set.AddSample(new Sample(ExtractFeatures(b), LetterCount, (LetterType)type));
            }
            set.shuffle(); return set;
        }

        public SamplesSet getTrainDataset(int count)
        {
            SamplesSet set = new SamplesSet();
            if (LetterCount == 0) return set;
            int perClass = count / LetterCount;
            for (int type = 0; type < LetterCount; type++)
            {
                var files = structure[(LetterType)type];
                if (files.Count == 0) continue;
                for (int i = 0; i < perClass; i++)
                    using (var b = new Bitmap(files[random.Next(files.Count)]))
                        set.AddSample(new Sample(ExtractFeatures(b), LetterCount, (LetterType)type));
            }
            set.shuffle(); return set;
        }

        // Метод 1: Берет случайную картинку из папок dataset
        public Tuple<Sample, Bitmap> getSample()
        {
            var type = (LetterType)random.Next(LetterCount);
            if (structure[type].Count == 0) return null;

            var fileName = structure[type][random.Next(structure[type].Count)];
            var bitmap = new Bitmap(fileName);
            double[] input = ExtractFeatures(bitmap);

            // Возвращаем ПАРУ: (данные для нейросети, изображение для экрана)
            return Tuple.Create(new Sample(input, LetterCount, type), bitmap);
        }

        // Метод 2: Обрабатывает конкретную картинку (например, с камеры)
        public Sample getSample(Bitmap bitmap)
        {
            double[] input = ExtractFeatures(bitmap);
            return new Sample(input, LetterCount);
        }

        private const int W = 200;
        private const int H = 200;

        private double[] ExtractFeatures(Bitmap original)
        {
            // 1. Бинаризация Оцу
            var bmp = new Bitmap(original, new Size(W, H));
            int[] gray = new int[W * H];
            using (var fb = new FastBitmap(bmp))
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        Color c = fb[x, y];
                        gray[y * W + x] = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);
                    }
            }
            int threshold = GetOtsuThreshold(gray);

            // 2. Поиск границ (Bounding Box)
            int minX = W, minY = H, maxX = 0, maxY = 0;
            bool[,] rawBlack = new bool[W, H];
            bool hasBlack = false;

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (gray[y * W + x] < threshold)
                    {
                        rawBlack[x, y] = true;
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                        hasBlack = true;
                    }
                }

            if (!hasBlack) return new double[200];

            // 3. НОРМАЛИЗАЦИЯ: Растягиваем найденную цифру на весь массив 200x200
            bool[,] black = new bool[W, H];
            int bw = maxX - minX + 1;
            int bh = maxY - minY + 1;

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    // Обратное проецирование координат
                    int srcX = minX + (x * bw / W);
                    int srcY = minY + (y * bh / H);
                    if (srcX >= 0 && srcX < W && srcY >= 0 && srcY < H)
                        black[x, y] = rawBlack[srcX, srcY];
                }

            // 4. Сбор признаков (Сетка 10x10)
            var f = new List<double>();
            for (int gy = 0; gy < 10; gy++)
                for (int gx = 0; gx < 10; gx++)
                {
                    int cnt = 0;
                    for (int x = gx * 20; x < (gx + 1) * 20; x++)
                        for (int y = gy * 20; y < (gy + 1) * 20; y++)
                            if (black[x, y]) cnt++;
                    f.Add(cnt / 400.0);
                }

            // Доп. признаки (профили)
            for (int i = 0; i < 40; i++) f.Add(BlackFractionInRow(black, (int)(i * H / 40.0)));
            for (int i = 0; i < 40; i++) f.Add(BlackFractionInCol(black, (int)(i * W / 40.0)));

            f.Add(VerticalSymmetryScore(black));
            f.Add(bw / (double)bh); // Соотношение сторон

            while (f.Count < 200) f.Add(0);
            return f.Take(200).ToArray();
        }

        private int GetOtsuThreshold(int[] data)
        {
            int[] hist = new int[256];
            foreach (var v in data) hist[v]++;
            float sum = 0; for (int i = 0; i < 256; i++) sum += i * hist[i];
            float sumB = 0; int wB = 0; float varMax = 0; int threshold = 0;
            for (int i = 0; i < 256; i++)
            {
                wB += hist[i]; if (wB == 0) continue;
                int wF = data.Length - wB; if (wF == 0) break;
                sumB += (float)(i * hist[i]);
                float mB = sumB / wB; float mF = (sum - sumB) / wF;
                float varBetween = (float)wB * wF * (mB - mF) * (mB - mF);
                if (varBetween > varMax) { varMax = varBetween; threshold = i; }
            }
            return threshold;
        }

        private double BlackFractionInRow(bool[,] b, int y) { int c = 0; for (int x = 0; x < W; x++) if (b[x, y]) c++; return c / (double)W; }
        private double BlackFractionInCol(bool[,] b, int x) { int c = 0; for (int y = 0; y < H; y++) if (b[x, y]) c++; return c / (double)H; }
        private double VerticalSymmetryScore(bool[,] b)
        {
            int m = 0; for (int x = 0; x < W / 2; x++) for (int y = 0; y < H; y++) if (b[x, y] != b[W - 1 - x, y]) m++;
            return 1.0 - (m / (double)(W * H / 2));
        }
    }
}