using System;
using System.Collections.Generic;
using System.Linq;

namespace NeuralNetwork1
{
    public class StudentNetwork : BaseNetwork
    {
        private readonly int[] structure;
        private double[][,] weights;
        private double[][] biases;
        private double[][] outputs;
        private double[][] sums;
        private double learningRate = 0.25; 
        private double momentum = 0.9;
        private double weightDecay = 0.0001; 
        private double[][,] prevWeightDeltas;
        private double[][] prevBiasDeltas;
        private Random random = new Random();
        private double[][] deltas;

        public StudentNetwork(int[] structure)
        {
            this.structure = structure;

            if (structure.Length < 3)
                throw new ArgumentException("Сеть должна содержать как минимум 3 слоя");

            if (structure[0] != 400)
                throw new ArgumentException("Входной слой должен содержать 400 нейронов");

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

                    // Инициализация Хе (He initialization) для ReLU
                    double scale = Math.Sqrt(2.0 / prevNeurons);

                    for (int to = 0; to < neurons; to++)
                    {
                        biases[layer - 1][to] = 0.01; 

                        for (int from = 0; from < prevNeurons; from++)
                        {
                            weights[layer - 1][from, to] = (random.NextDouble() * 2 - 1) * scale;
                        }
                    }
                }
            }
        }

        private double LeakyReLU(double x)
        {
            return x > 0 ? x : 0.01 * x;
        }

        private double LeakyReLUDerivative(double x)
        {
            return x > 0 ? 1 : 0.01;
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

        private double SigmoidDerivative(double x)
        {
            double s = Sigmoid(x);
            return s * (1.0 - s);
        }

        private void ForwardPass(double[] input, bool training = true)
        {
            for (int i = 0; i < structure[0]; i++)
            {
                outputs[0][i] = input[i] / 200.0; 
                sums[0][i] = outputs[0][i];
            }

            int lastLayer = structure.Length - 1;
            for (int layer = 1; layer < lastLayer; layer++)
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
                    outputs[layer][to] = LeakyReLU(sum);
                }
            }

            int prev = lastLayer - 1;
            for (int neuron = 0; neuron < structure[lastLayer]; neuron++)
            {
                double sum = 0;
                for (int prevNeuron = 0; prevNeuron < structure[prev]; prevNeuron++)
                {
                    sum += outputs[prev][prevNeuron] * weights[prev][prevNeuron, neuron];
                }
                sum += biases[prev][neuron];
                sums[lastLayer][neuron] = sum;
                outputs[lastLayer][neuron] = Sigmoid(sum);
            }
        }

        private void BackwardPass(double[] target)
        {
            int lastLayer = structure.Length - 1;

            for (int i = 0; i < structure[lastLayer]; i++)
            {
                double output = outputs[lastLayer][i];
                double error = output - target[i];
                deltas[lastLayer][i] = error * SigmoidDerivative(sums[lastLayer][i]);
            }

            for (int layer = lastLayer - 1; layer > 0; layer--)
            {
                for (int neuron = 0; neuron < structure[layer]; neuron++)
                {
                    double sum = 0;
                    for (int nextNeuron = 0; nextNeuron < structure[layer + 1]; nextNeuron++)
                    {
                        sum += deltas[layer + 1][nextNeuron] * weights[layer][neuron, nextNeuron];
                    }
                    deltas[layer][neuron] = sum * LeakyReLUDerivative(sums[layer][neuron]);
                }
            }
        }

        private void UpdateWeights(double batchSize = 1.0)
        {
            double effectiveLearningRate = learningRate / batchSize;

            for (int layer = 0; layer < structure.Length - 1; layer++)
            {
                for (int to = 0; to < structure[layer + 1]; to++)
                {
                    for (int from = 0; from < structure[layer]; from++)
                    {
                        double gradient = deltas[layer + 1][to] * outputs[layer][from];
                        double delta = effectiveLearningRate * gradient +
                                      momentum * prevWeightDeltas[layer][from, to];

                        delta += effectiveLearningRate * weightDecay * weights[layer][from, to];

                        weights[layer][from, to] -= delta;
                        prevWeightDeltas[layer][from, to] = delta;
                    }

                    double biasDelta = effectiveLearningRate * deltas[layer + 1][to] +
                                      momentum * prevBiasDeltas[layer][to];
                    biases[layer][to] -= biasDelta;
                    prevBiasDeltas[layer][to] = biasDelta;
                }
            }
        }

        private double CalculateCrossEntropyError(double[] target)
        {
            double error = 0;
            int lastLayer = structure.Length - 1;
            double epsilon = 1e-15;

            for (int i = 0; i < structure[lastLayer]; i++)
            {
                double output = Math.Max(Math.Min(outputs[lastLayer][i], 1 - epsilon), epsilon);
                error -= target[i] * Math.Log(output);
            }

            return error;
        }

        private double CalculateAccuracy(SamplesSet samplesSet)
        {
            int correct = 0;
            int total = samplesSet.Count;

            for (int i = 0; i < total; i++)
            {
                var sample = samplesSet[i];
                ForwardPass(sample.input, false);

                int predicted = 0;
                double maxProb = 0;
                int lastLayer = structure.Length - 1;

                for (int j = 0; j < structure[lastLayer]; j++)
                {
                    if (outputs[lastLayer][j] > maxProb)
                    {
                        maxProb = outputs[lastLayer][j];
                        predicted = j;
                    }
                }

                if (predicted == (int)sample.actualClass)
                    correct++;
            }

            return (double)correct / total;
        }

        public override int Train(Sample sample, double acceptableError, bool parallel)
        {
            int iterations = 0;
            double error;

            do
            {
                ForwardPass(sample.input);
                error = CalculateCrossEntropyError(sample.Output);

                if (error <= acceptableError)
                    break;

                BackwardPass(sample.Output);
                UpdateWeights();

                iterations++;

            } while (iterations < 1000);

            return iterations;
        }

        public override double TrainOnDataSet(SamplesSet samplesSet, int epochsCount, double acceptableError, bool parallel)
        {
            int totalSamples = samplesSet.Count;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            int trainSize = (int)(totalSamples * 0.8);
            var trainIndices = new List<int>();
            var valIndices = new List<int>();

            for (int i = 0; i < totalSamples; i++)
            {
                if (i < trainSize)
                    trainIndices.Add(i);
                else
                    valIndices.Add(i);
            }

            double bestValidationError = double.MaxValue;
            int patience = 20;
            int patienceCounter = 0;

            double[][,] weightGradients = new double[structure.Length - 1][,];
            double[][] biasGradients = new double[structure.Length - 1][];

            for (int i = 0; i < structure.Length - 1; i++)
            {
                weightGradients[i] = new double[structure[i], structure[i + 1]];
                biasGradients[i] = new double[structure[i + 1]];
            }

            for (int epoch = 0; epoch < epochsCount; epoch++)
            {
                trainIndices = trainIndices.OrderBy(x => random.Next()).ToList();

                int batchSize = 16; 
                double trainError = 0;
                int trainCorrect = 0;

                for (int batchStart = 0; batchStart < trainSize; batchStart += batchSize)
                {
                    int batchEnd = Math.Min(batchStart + batchSize, trainSize);

                    for (int layer = 0; layer < structure.Length - 1; layer++)
                    {
                        Array.Clear(weightGradients[layer], 0, weightGradients[layer].Length);
                        Array.Clear(biasGradients[layer], 0, biasGradients[layer].Length);
                    }

                    for (int i = batchStart; i < batchEnd; i++)
                    {
                        var sample = samplesSet[trainIndices[i]];
                        ForwardPass(sample.input);

                        trainError += CalculateCrossEntropyError(sample.Output);

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
                            trainCorrect++;

                        BackwardPass(sample.Output);

                        for (int layer = 0; layer < structure.Length - 1; layer++)
                        {
                            for (int to = 0; to < structure[layer + 1]; to++)
                            {
                                for (int from = 0; from < structure[layer]; from++)
                                {
                                    weightGradients[layer][from, to] += deltas[layer + 1][to] * outputs[layer][from];
                                }
                                biasGradients[layer][to] += deltas[layer + 1][to];
                            }
                        }
                    }

                    for (int layer = 0; layer < structure.Length - 1; layer++)
                    {
                        for (int to = 0; to < structure[layer + 1]; to++)
                        {
                            for (int from = 0; from < structure[layer]; from++)
                            {
                                deltas[layer + 1][to] = weightGradients[layer][from, to] / (batchEnd - batchStart);
                            }
                            deltas[layer + 1][to] = biasGradients[layer][to] / (batchEnd - batchStart);
                        }
                    }

                    UpdateWeights(batchEnd - batchStart);
                }

                double validationError = 0;
                int validationCorrect = 0;

                foreach (var idx in valIndices)
                {
                    var sample = samplesSet[idx];
                    ForwardPass(sample.input, false);
                    validationError += CalculateCrossEntropyError(sample.Output);

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

                trainError /= trainSize;
                validationError /= valIndices.Count;
                double trainAccuracy = (double)trainCorrect / trainSize;
                double validationAccuracy = (double)validationCorrect / valIndices.Count;

                if (validationError < bestValidationError * 0.995)
                {
                    bestValidationError = validationError;
                    patienceCounter = 0;
                    learningRate = Math.Min(learningRate * 1.05, 0.5);
                }
                else
                {
                    patienceCounter++;
                    if (patienceCounter >= patience / 2)
                    {
                        learningRate *= 0.9; 
                    }
                }

                OnTrainProgress((double)epoch / epochsCount, validationError, stopwatch.Elapsed);

                if (epoch % 5 == 0)
                {
                    Console.WriteLine($"Эпоха {epoch}: Train Acc={trainAccuracy:P2}, Val Acc={validationAccuracy:P2}, " +
                                    $"Val Error={validationError:F6}, LR={learningRate:F4}");
                }

                if (patienceCounter >= patience)
                {
                    Console.WriteLine($"Ранняя остановка на эпохе {epoch}");
                    break;
                }

                if (validationAccuracy > 0.95 && validationError < acceptableError)
                {
                    Console.WriteLine($"Достигнута достаточная точность на эпохе {epoch}");
                    break;
                }
            }

            stopwatch.Stop();

            double finalAccuracy = CalculateAccuracy(samplesSet);
            Console.WriteLine($"Финальная точность: {finalAccuracy:P2}");

            return bestValidationError;
        }

        protected override double[] Compute(double[] input)
        {
            ForwardPass(input, false);
            return outputs[structure.Length - 1];
        }
    }
}