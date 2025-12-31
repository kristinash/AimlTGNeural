using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace NeuralNetwork1
{
    public class StudentNetwork : BaseNetwork
    {
        // Поля сети
        private double[][] layers;
        private double[][][] weights;
        private double[][] biases;
        private Random rand = new Random();
        public Stopwatch stopWatch = new Stopwatch();

        public StudentNetwork(int[] structure)
        {
            layers = new double[structure.Length][];
            biases = new double[structure.Length][];
            weights = new double[structure.Length - 1][][];

            for (int i = 0; i < structure.Length; i++)
            {
                layers[i] = new double[structure[i]];
                biases[i] = new double[structure[i]];

                if (i < structure.Length - 1)
                {
                    weights[i] = new double[structure[i]][];
                    for (int j = 0; j < structure[i]; j++)
                    {
                        weights[i][j] = new double[structure[i + 1]];
                        for (int k = 0; k < structure[i + 1]; k++)
                        {
                            // Инициализация Ксавье для плавной сходимости
                            weights[i][j][k] = (rand.NextDouble() * 2 - 1) * Math.Sqrt(2.0 / structure[i]);
                        }
                    }
                }
                // Биасы инициализируем нулями или малыми значениями
                for (int j = 0; j < structure[i]; j++)
                    biases[i][j] = 0.01;
            }
        }

        // Сигмоида и её производная
        private double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-2.0 * x));
        private double SigmoidDerivative(double x) => 2.0 * x * (1.0 - x);

        // Прямой проход для конкретных массивов слоев (важно для параллельности)
        private double[] Forward(double[] input, double[][] targetLayers)
        {
            Array.Copy(input, targetLayers[0], input.Length);

            for (int i = 0; i < weights.Length; i++)
            {
                for (int k = 0; k < targetLayers[i + 1].Length; k++)
                {
                    double sum = 0;
                    for (int j = 0; j < targetLayers[i].Length; j++)
                        sum += targetLayers[i][j] * weights[i][j][k];

                    targetLayers[i + 1][k] = Sigmoid(sum + biases[i + 1][k]);
                }
            }
            return targetLayers.Last();
        }

        protected override double[] Compute(double[] input) => Forward(input, this.layers);

        public override int Train(Sample sample, double acceptableError, bool parallel)
        {
            int iters = 0;
            double error = double.MaxValue;
            while (error > acceptableError && iters < 100)
            {
                error = BackPropagate(sample.input, sample.Output, 0.01);
                iters++;
            }
            return iters;
        }

        // Классический последовательный BackPropagate
        private double BackPropagate(double[] input, double[] target, double learningRate)
        {
            double[] output = Compute(input);
            double[][] deltas = new double[layers.Length][];

            // 1. Ошибка выходного слоя
            deltas[layers.Length - 1] = new double[output.Length];
            double totalError = 0;
            for (int i = 0; i < output.Length; i++)
            {
                double err = target[i] - output[i];
                deltas[layers.Length - 1][i] = err * SigmoidDerivative(output[i]);
                totalError += err * err;
            }

            // 2. Обратный проход
            for (int i = layers.Length - 2; i >= 1; i--)
            {
                deltas[i] = new double[layers[i].Length];
                for (int j = 0; j < layers[i].Length; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < layers[i + 1].Length; k++)
                        sum += deltas[i + 1][k] * weights[i][j][k];
                    deltas[i][j] = sum * SigmoidDerivative(layers[i][j]);
                }
            }

            // 3. Обновление весов
            for (int i = 0; i < weights.Length; i++)
            {
                for (int j = 0; j < layers[i].Length; j++)
                    for (int k = 0; k < layers[i + 1].Length; k++)
                        weights[i][j][k] += learningRate * deltas[i + 1][k] * layers[i][j];

                for (int k = 0; k < layers[i + 1].Length; k++)
                    biases[i + 1][k] += learningRate * deltas[i + 1][k];
            }

            return totalError / 2.0;
        }

        public override double TrainOnDataSet(SamplesSet samplesSet, int epochsCount, double acceptableError, bool parallel)
        {
            int epoch = 0;
            double error = double.MaxValue;
            double learningRate = 0.01; // МАЛЕНЬКИЙ ШАГ для плавности ползунка
            stopWatch.Restart();

            while (epoch < epochsCount && error > acceptableError)
            {
                epoch++;
                double currentEpochError = 0;

                if (parallel)
                {
                    object lockObj = new object();
                    Parallel.ForEach(samplesSet.samples, sample =>
                    {
                        // Создаем локальную копию слоев для потока
                        double[][] localLayers = layers.Select(l => new double[l.Length]).ToArray();
                        double[] output = Forward(sample.input, localLayers);

                        // Расчет дельт (локальный)
                        double[][] deltas = new double[localLayers.Length][];
                        deltas[localLayers.Length - 1] = new double[output.Length];
                        for (int i = 0; i < output.Length; i++)
                            deltas[localLayers.Length - 1][i] = (sample.Output[i] - output[i]) * SigmoidDerivative(output[i]);

                        for (int i = localLayers.Length - 2; i >= 1; i--)
                        {
                            deltas[i] = new double[localLayers[i].Length];
                            for (int j = 0; j < localLayers[i].Length; j++)
                            {
                                double sum = 0;
                                for (int k = 0; k < localLayers[i + 1].Length; k++)
                                    sum += deltas[i + 1][k] * weights[i][j][k];
                                deltas[i][j] = sum * SigmoidDerivative(localLayers[i][j]);
                            }
                        }

                        // Обновление общих весов
                        lock (lockObj)
                        {
                            for (int i = 0; i < weights.Length; i++)
                            {
                                for (int j = 0; j < localLayers[i].Length; j++)
                                    for (int k = 0; k < localLayers[i + 1].Length; k++)
                                        weights[i][j][k] += learningRate * deltas[i + 1][k] * localLayers[i][j];

                                for (int k = 0; k < localLayers[i + 1].Length; k++)
                                    biases[i + 1][k] += learningRate * deltas[i + 1][k];
                            }

                            double e = 0;
                            for (int i = 0; i < output.Length; i++) e += Math.Pow(sample.Output[i] - output[i], 2);
                            currentEpochError += e / 2.0;
                        }
                    });
                }
                else
                {
                    foreach (var sample in samplesSet.samples)
                        currentEpochError += BackPropagate(sample.input, sample.Output, learningRate);
                }

                error = currentEpochError;
                // Обновляем ползунок (движется постепенно)
                OnTrainProgress((double)epoch / epochsCount, error, stopWatch.Elapsed);
            }

            // ГАРАНТИРУЕМ, что ползунок заполнится до конца (1.0)
            OnTrainProgress(1.0, error, stopWatch.Elapsed);
            stopWatch.Stop();
            return error;
        }
    }
}