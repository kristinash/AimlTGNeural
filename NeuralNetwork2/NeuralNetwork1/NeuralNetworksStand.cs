using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NeuralNetwork1
{
    public partial class NeuralNetworksStand : Form
    {
        /// <summary>
        /// Генератор и обработчик изображений
        /// </summary>
        private DatasetProcessor dataset = new DatasetProcessor();

        /// <summary>
        /// Кэш нейросетей
        /// </summary>
        private Dictionary<string, BaseNetwork> networksCache = new Dictionary<string, BaseNetwork>();

        private readonly Dictionary<string, Func<int[], BaseNetwork>> networksFabric;

        /// <summary>
        /// Текущая выбранная через селектор нейросеть
        /// </summary>
        public BaseNetwork Net
        {
            get
            {
                var selectedItem = (string)netTypeBox.SelectedItem;
                if (selectedItem == null) return null;
                if (!networksCache.ContainsKey(selectedItem))
                    networksCache.Add(selectedItem, CreateNetwork(selectedItem));

                return networksCache[selectedItem];
            }
        }

        /// <summary>
        /// Конструктор формы
        /// </summary>
        public NeuralNetworksStand(Dictionary<string, Func<int[], BaseNetwork>> networksFabric)
        {
            InitializeComponent();
            this.networksFabric = networksFabric;
            netTypeBox.Items.AddRange(this.networksFabric.Keys.Select(s => (object)s).ToArray());
            netTypeBox.SelectedIndex = 0;
            dataset.LetterCount = (int)classCounter.Value;

            // Инициализация сети при запуске
            button3_Click(this, null);
            pictureBox1.Image = Properties.Resources.Title;
        }

        /// <summary>
        /// Обновление прогресс-бара и ошибок в UI
        /// </summary>
        public void UpdateLearningInfo(double progress, double error, TimeSpan elapsedTime)
        {
            if (progressBar1.InvokeRequired)
            {
                progressBar1.Invoke(new TrainProgressHandler(UpdateLearningInfo), progress, error, elapsedTime);
                return;
            }

            StatusLabel.Text = "Ошибка: " + error.ToString("F6");
            int progressPercent = (int)Math.Round(progress * 100);
            progressPercent = Math.Min(100, Math.Max(0, progressPercent));
            elapsedTimeLabel.Text = "Затраченное время : " + elapsedTime.Duration().ToString(@"hh\:mm\:ss\:ff");
            progressBar1.Value = progressPercent;
        }

        /// <summary>
        /// Клик по картинке: берем случайный пример и распознаем его
        /// </summary>
        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            // Метод getSample возвращает Tuple<Sample, Bitmap>
            var fullSample = dataset.getSample();

            if (fullSample == null || fullSample.Item1 == null) return;

            // Предсказание
            Net.Predict(fullSample.Item1);

            // Проверка корректности (сравнение предсказанного класса с реальным)
            bool isCorrect = fullSample.Item1.recognizedClass == fullSample.Item1.actualClass;
            label1.ForeColor = isCorrect ? Color.Green : Color.Red;

            label1.Text = "Распознано : " + DatasetProcessor.LetterTypeToString(fullSample.Item1.recognizedClass) +
                          (isCorrect ? " (Верно)" : " (Ошибка, это " + DatasetProcessor.LetterTypeToString(fullSample.Item1.actualClass) + ")");

            // Вывод изображения (теперь оно нормализовано и бинаризовано)
            pictureBox1.Image = fullSample.Item2;
            pictureBox1.Invalidate();
        }

        /// <summary>
        /// Асинхронное обучение сети
        /// </summary>
        private async Task<double> train_networkAsync(int training_size, int epoches, double acceptable_error, bool parallel = true)
        {
            label1.Text = "Выполняется обучение...";
            label1.ForeColor = Color.Red;
            groupBox1.Enabled = false;
            pictureBox1.Enabled = false;

            SamplesSet samples = dataset.getTrainDataset(training_size);

            try
            {
                var curNet = Net;
                double f = await Task.Run(() => curNet.TrainOnDataSet(samples, epoches, acceptable_error, parallel));

                label1.Text = "Обучение завершено. Щелкните на картинку для теста";
                label1.ForeColor = Color.Green;
                StatusLabel.Text = "Финальная ошибка: " + f.ToString("F6");
                StatusLabel.ForeColor = Color.Green;
                return f;
            }
            catch (Exception e)
            {
                label1.Text = $"Исключение: {e.Message}";
            }
            finally
            {
                groupBox1.Enabled = true;
                pictureBox1.Enabled = true;
            }

            return 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            train_networkAsync((int)TrainingSizeCounter.Value, (int)EpochesCounter.Value,
                (100 - AccuracyCounter.Value) / 100.0, true);
        }

        /// <summary>
        /// Тестирование на тестовой выборке
        /// </summary>
        private void button2_Click(object sender, EventArgs e)
        {
            Enabled = false;
            SamplesSet samples = dataset.getTestDataset((int)TrainingSizeCounter.Value);
            double accuracy = samples.TestNeuralNetwork(Net);

            StatusLabel.Text = $"Точность теста : {accuracy * 100,5:F2}%";
            StatusLabel.ForeColor = accuracy * 100 >= AccuracyCounter.Value ? Color.Green : Color.Red;
            Enabled = true;
        }

        /// <summary>
        /// Пересоздание сети
        /// </summary>
        private void button3_Click(object sender, EventArgs e)
        {
            int[] structure = CurrentNetworkStructure();
            if (structure.Length < 2 || structure[0] != 200 ||
                structure[structure.Length - 1] != dataset.LetterCount)
            {
                MessageBox.Show(
                    $"Ошибка структуры! Вход должен быть 200, выход - {dataset.LetterCount}",
                    "Ошибка", MessageBoxButtons.OK);
                return;
            }

            foreach (var network in networksCache.Values)
                network.TrainProgress -= UpdateLearningInfo;

            networksCache = networksCache.ToDictionary(oldNet => oldNet.Key, oldNet => CreateNetwork(oldNet.Key));

            StatusLabel.Text = "Сеть готова";
            StatusLabel.ForeColor = Color.Black;
        }

        private int[] CurrentNetworkStructure()
        {
            return netStructureBox.Text.Split(';').Select(int.Parse).ToArray();
        }

        private void classCounter_ValueChanged(object sender, EventArgs e)
        {
            dataset.LetterCount = (int)classCounter.Value;
            var vals = netStructureBox.Text.Split(';');
            if (vals.Length < 1) return;
            vals[vals.Length - 1] = classCounter.Value.ToString();
            netStructureBox.Text = string.Join(";", vals);
        }

        private BaseNetwork CreateNetwork(string networkName)
        {
            var network = networksFabric[networkName](CurrentNetworkStructure());
            network.TrainProgress += UpdateLearningInfo;
            return network;
        }

        // --- МЕТОДЫ ПОДСКАЗОК (MouseEnter) ---

        private void recreateNetButton_MouseEnter(object sender, EventArgs e)
        {
            infoStatusLabel.Text = "Заново пересоздаёт сеть с указанными параметрами";
        }

        private void netTrainButton_MouseEnter(object sender, EventArgs e)
        {
            infoStatusLabel.Text = "Обучить нейросеть с указанными параметрами";
        }

        private void testNetButton_MouseEnter(object sender, EventArgs e)
        {
            infoStatusLabel.Text = "Тестировать нейросеть на тестовой выборке такого же размера";
        }

        private void btnCamera_Click(object sender, EventArgs e)
        {
            var form = new Camera(Net, dataset);
            form.Show();
        }
    }
}