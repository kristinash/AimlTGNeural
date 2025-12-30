using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace NeuralNetwork1
{
    public class StudentNetwork : BaseNetwork
    {
        private readonly int[] structure;
        private double[][,] weights;
        private double[][] biases;
        private double[][] outputs;
        private double[][] sums;
        private double learningRate = 0.1;
        private double momentum = 0.9;
        private double weightDecay = 0.00001; 
        private double[][,] prevWeightDeltas;
        private double[][] prevBiasDeltas;
        private Random random = new Random();
        private double[][] deltas;
        public Stopwatch stopWatch = new Stopwatch();

        private double minLearningRate = 0.001;
        private double learningRateDecay = 0.995;
        private int noImprovementCount = 0;
        private double bestError = double.MaxValue;

        public StudentNetwork(int[] structure)
        {
            this.structure = structure;

            if (structure.Length < 3)
                throw new ArgumentException("Сеть должна содержать как минимум 3 слоя");

            if (structure[0] != 400)
                throw new ArgumentException("Входной слой должен содержать 400 нейронов");

            if (structure[structure.Length - 1] != 4)
                throw new ArgumentException("Выходной слой должен содержать 4 нейрона для 4 фигур");

            InitializeNetwork();
        }

        private void InitializeNetwork()
        {
            int layers = structure.Length;

            weights = new double[layers - 1][,];
            biases = new double[layers - 1][];
            outputs = new double[layers][];
            sums = new double[layers][];
            deltas = new double[layers][];
            prevWeightDeltas = new double[layers - 1][,];
            prevBiasDeltas = new double[layers - 1][];

            for (int layer = 0; layer < layers; layer++)
            {
                int neurons = structure[layer];
                outputs[layer] = new double[neurons];
                sums[layer] = new double[neurons];
                deltas[layer] = new double[neurons];

                if (layer > 0)
                {
                    int prevNeurons = structure[layer - 1];
                    weights[layer - 1] = new double[prevNeurons, neurons];
                    prevWeightDeltas[layer - 1] = new double[prevNeurons, neurons];
                    biases[layer - 1] = new double[neurons];
                    prevBiasDeltas[layer - 1] = new double[neurons];

                    double scale = Math.Sqrt(2.0 / (prevNeurons + neurons));

                    for (int to = 0; to < neurons; to++)
                    {
                        biases[layer - 1][to] = (random.NextDouble() * 0.2) - 0.1;

                        for (int from = 0; from < prevNeurons; from++)
                        {
                            weights[layer - 1][from, to] = (random.NextDouble() * 2 - 1) * scale;
                            prevWeightDeltas[layer - 1][from, to] = 0;
                        }
                    }
                }
            }
        }

        private double Sigmoid(double x)
        {
            if (x >= 0)
                return 1.0 / (1.0 + Math.Exp(-x));
            else
            {
                double expX = Math.Exp(x);
                return expX / (1.0 + expX);
            }
        }

        private double SigmoidDerivative(double output)
        {
            return output * (1.0 - output);
        }

        private void ForwardPass(double[] input, bool training = true)
        {
            double maxInput = input.Max();
            double minInput = input.Min();
            double range = maxInput - minInput;

            if (range > 0)
            {
                for (int i = 0; i < structure[0]; i++)
                {
                    outputs[0][i] = (input[i] - minInput) / range;
                    sums[0][i] = outputs[0][i];
                }
            }
            else
            {
                for (int i = 0; i < structure[0]; i++)
                {
                    outputs[0][i] = input[i];
                    sums[0][i] = outputs[0][i];
                }
            }

            for (int layer = 1; layer < structure.Length; layer++)
            {
                int prevLayer = layer - 1;

                for (int to = 0; to < structure[layer]; to++)
                {
                    double sum = 0;
                    for (int from = 0; from < structure[prevLayer]; from++)
                    {
                        sum += outputs[prevLayer][from] * weights[prevLayer][from, to];
                    }
                    sum += biases[prevLayer][to];
                    sums[layer][to] = sum;
                    outputs[layer][to] = Sigmoid(sum);

                    if (training && random.NextDouble() < 0.1)
                    {
                        outputs[layer][to] += (random.NextDouble() - 0.5) * 0.01;
                    }
                }
            }
        }

        private double CalculateError(double[] target)
        {
            int lastLayer = structure.Length - 1;
            double error = 0;

            for (int i = 0; i < structure[lastLayer]; i++)
            {
                double output = Math.Max(Math.Min(outputs[lastLayer][i], 0.999999), 0.000001);
                error += target[i] * Math.Log(output) + (1 - target[i]) * Math.Log(1 - output);
            }

            return -error;
        }

        private void BackwardPass(double[] target)
        {
            int lastLayer = structure.Length - 1;

            for (int i = 0; i < structure[lastLayer]; i++)
            {
                double output = outputs[lastLayer][i];
                deltas[lastLayer][i] = output - target[i];
            }

            for (int layer = lastLayer - 1; layer >= 1; layer--)
            {
                for (int j = 0; j < structure[layer]; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < structure[layer + 1]; k++)
                    {
                        sum += deltas[layer + 1][k] * weights[layer][j, k];
                    }
                    deltas[layer][j] = sum * SigmoidDerivative(outputs[layer][j]);
                }
            }
        }

        private void UpdateWeights(double lr, double batchSize = 1.0)
        {
            double effectiveLR = lr / batchSize;

            for (int layer = 0; layer < structure.Length - 1; layer++)
            {
                for (int to = 0; to < structure[layer + 1]; to++)
                {
                    for (int from = 0; from < structure[layer]; from++)
                    {
                        double gradient = deltas[layer + 1][to] * outputs[layer][from];
                        double delta = effectiveLR * gradient + momentum * prevWeightDeltas[layer][from, to];

                        delta += effectiveLR * weightDecay * weights[layer][from, to];

                        weights[layer][from, to] += delta;
                        prevWeightDeltas[layer][from, to] = delta;
                    }

                    double biasDelta = effectiveLR * deltas[layer + 1][to] + momentum * prevBiasDeltas[layer][to];
                    biases[layer][to] += biasDelta;
                    prevBiasDeltas[layer][to] = biasDelta;
                }
            }
        }

        protected override double[] Compute(double[] input)
        {
            ForwardPass(input, false);
            return outputs[structure.Length - 1];
        }

        public override int Train(Sample sample, double acceptableError, bool parallel)
        {
            int iterations = 0;
            double error;

            do
            {
                ForwardPass(sample.input);
                error = CalculateError(sample.Output);

                if (error <= acceptableError)
                    break;

                BackwardPass(sample.Output);
                UpdateWeights(learningRate);

                iterations++;

                if (iterations > 5000)
                    break;

            } while (error > acceptableError);

            return iterations;
        }

        private void ShuffleList<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private List<int> GetShuffledIndices(int count)
        {
            var indices = new List<int>(count);
            for (int i = 0; i < count; i++)
                indices.Add(i);

            ShuffleList(indices);
            return indices;
        }

        public override double TrainOnDataSet(SamplesSet samplesSet, int epochsCount, double acceptableError, bool parallel)
        {
            stopWatch.Restart();

            int totalSamples = samplesSet.Count;
            int trainSize = (int)(totalSamples * 0.8);
            int valSize = totalSamples - trainSize;

            var trainSet = new List<Sample>();
            var validationSet = new List<Sample>();

            var shuffledIndices = GetShuffledIndices(totalSamples);

            for (int i = 0; i < totalSamples; i++)
            {
                int index = shuffledIndices[i];
                var sample = samplesSet[index];

                if (i < trainSize)
                    trainSet.Add(sample);
                else
                    validationSet.Add(sample);
            }

            Console.WriteLine($"Начало обучения: {trainSet.Count} обучающих, {validationSet.Count} валидационных");

            double currentLR = learningRate;
            bestError = double.MaxValue;
            noImprovementCount = 0;

            int batchSize = Math.Min(16, trainSet.Count);
            int batchesPerEpoch = trainSet.Count > 0 ? (trainSet.Count + batchSize - 1) / batchSize : 1;

            for (int epoch = 0; epoch < epochsCount; epoch++)
            {
                ShuffleList(trainSet);

                double epochError = 0;
                int epochCorrect = 0;

                for (int batch = 0; batch < batchesPerEpoch; batch++)
                {
                    int start = batch * batchSize;
                    int end = Math.Min(start + batchSize, trainSet.Count);
                    int currentBatchSize = end - start;

                    if (currentBatchSize == 0) continue;

                    for (int layer = 0; layer < structure.Length; layer++)
                    {
                        Array.Clear(deltas[layer], 0, deltas[layer].Length);
                    }

                    double batchError = 0;
                    for (int i = start; i < end; i++)
                    {
                        var sample = trainSet[i];

                        ForwardPass(sample.input);

                        batchError += CalculateError(sample.Output);

                        int lastLayer = structure.Length - 1;
                        int predicted = 0;
                        double maxProb = 0;
                        for (int j = 0; j < structure[lastLayer]; j++)
                        {
                            if (outputs[lastLayer][j] > maxProb)
                            {
                                maxProb = outputs[lastLayer][j];
                                predicted = j;
                            }
                        }
                        if (predicted == (int)sample.actualClass)
                            epochCorrect++;

                        BackwardPass(sample.Output);
                    }

                    UpdateWeights(currentLR, currentBatchSize);
                    epochError += batchError / currentBatchSize;
                }

                epochError /= batchesPerEpoch;
                double trainAccuracy = trainSet.Count > 0 ? (double)epochCorrect / trainSet.Count : 0;

                double validationError = 0;
                int validationCorrect = 0;

                for (int i = 0; i < validationSet.Count; i++)
                {
                    var sample = validationSet[i];
                    ForwardPass(sample.input, false);
                    validationError += CalculateError(sample.Output);

                    int lastLayer = structure.Length - 1;
                    int predicted = 0;
                    double maxProb = 0;
                    for (int j = 0; j < structure[lastLayer]; j++)
                    {
                        if (outputs[lastLayer][j] > maxProb)
                        {
                            maxProb = outputs[lastLayer][j];
                            predicted = j;
                        }
                    }
                    if (predicted == (int)sample.actualClass)
                        validationCorrect++;
                }

                validationError /= validationSet.Count > 0 ? validationSet.Count : 1;
                double validationAccuracy = validationSet.Count > 0 ? (double)validationCorrect / validationSet.Count : 0;

                if (validationError < bestError * 0.995) 
                {
                    bestError = validationError;
                    noImprovementCount = 0;
                    currentLR = Math.Min(currentLR * 1.05, 0.5);
                }
                else
                {
                    noImprovementCount++;
                    if (noImprovementCount >= 5)
                    {
                        currentLR *= 0.7;
                        currentLR = Math.Max(currentLR, minLearningRate);
                    }
                }

                if (noImprovementCount >= 15)
                {
                    Console.WriteLine($"Ранняя остановка на эпохе {epoch + 1}");
                    break;
                }

                OnTrainProgress((double)epoch / epochsCount, validationError, stopWatch.Elapsed);

                if (epoch % 5 == 0 || epoch == 0)
                {
                    Console.WriteLine($"Эпоха {epoch + 1}: " +
                                    $"Train Acc={trainAccuracy:P2}, Val Acc={validationAccuracy:P2}, " +
                                    $"Val Error={validationError:F6}, LR={currentLR:F4}");
                }

                if (validationAccuracy >= 0.95 && validationError < acceptableError)
                {
                    Console.WriteLine($"Достигнута хорошая точность на эпохе {epoch + 1}");
                    break;
                }

                currentLR = Math.Max(currentLR * learningRateDecay, minLearningRate);
            }

            int totalCorrect = 0;
            for (int i = 0; i < samplesSet.Count; i++)
            {
                var sample = samplesSet[i];
                ForwardPass(sample.input, false);

                int lastLayer = structure.Length - 1;
                int predicted = 0;
                double maxProb = 0;
                for (int j = 0; j < structure[lastLayer]; j++)
                {
                    if (outputs[lastLayer][j] > maxProb)
                    {
                        maxProb = outputs[lastLayer][j];
                        predicted = j;
                    }
                }
                if (predicted == (int)sample.actualClass)
                    totalCorrect++;
            }

            double finalAccuracy = (double)totalCorrect / samplesSet.Count;
            Console.WriteLine($"Финальная точность на всем датасете: {finalAccuracy:P2}");
            Console.WriteLine($"Общее время обучения: {stopWatch.Elapsed.TotalSeconds:F2} сек");

            OnTrainProgress(1.0, bestError, stopWatch.Elapsed);
            stopWatch.Stop();

            return bestError;
        }

        public string GetNetworkInfo()
        {
            int totalWeights = 0;
            for (int i = 0; i < structure.Length - 1; i++)
            {
                totalWeights += structure[i] * structure[i + 1];
            }

            return $"Структура: {string.Join(" → ", structure)}\n" +
                   $"Всего весов: {totalWeights:N0}\n" +
                   $"Learning Rate: {learningRate:F4}\n" +
                   $"Momentum: {momentum:F2}\n" +
                   $"Weight Decay: {weightDecay:F6}";
        }
    }
}
