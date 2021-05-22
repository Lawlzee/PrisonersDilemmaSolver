using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrisonersDilemmaSolver
{
    public static class Program
    {
        static void Main(string[] args)
        {
            int seed = 1;
            Random random = new Random(seed);

            const int roundLength = 256;
            const int weigthSize = roundLength * 4 + 2;
            const int playerCount = 32;
            const int playerToRemovePerRound = 8;
            const int mutationPerIteration = 16;

            float[,] weights = new float[playerCount, weigthSize];

            for (int i = 0; i < playerCount; i++)
            {
                for (int j = 0; j < weigthSize; j++)
                {
                    weights[i, j] = ((float)random.NextDouble() * 2) - 1;
                }
            }

            int[,] roundScores = new int[playerCount, playerCount];
            Span<int> results = stackalloc int[playerCount];

            int[] newPlayers = Enumerable.Range(0, playerCount).ToArray();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int iteration = 0; iteration < int.MaxValue; iteration++)
            {
                Parallel.ForEach(newPlayers, i =>
                {
                    float forgetfulnessA = weights[i, weigthSize - 1];
                    for (int j = 0; j < playerCount; j++)
                    {
                        if (j >= i && newPlayers.Contains(j))
                        {
                            continue;
                        }

                        float forgetfulnessB = weights[j, weigthSize - 1];

                        float sumA = weights[i, weigthSize - 2];
                        float sumB = weights[j, weigthSize - 2];
                        int scoreA = 0;
                        int scoreB = 0;
                        
                        for (int round = 0; round < roundLength; round++)
                        {
                            bool moveA = sumA > 0;
                            bool moveB = sumB > 0;
                            if (moveA)
                            {
                                if (moveB)//Silence, Silence
                                {
                                    scoreA += 3;
                                    scoreB += 3;
                                }
                                else //Silence, Truth
                                {
                                    scoreA += 0;
                                    scoreB += 5;
                                }
                            }
                            else
                            {
                                if (moveB) //Truth, Silence
                                {
                                    scoreA += 5;
                                    scoreB += 0;
                                }
                                else //Truth, Truth
                                {
                                    scoreA += 1;
                                    scoreB += 1;
                                }
                            }

                            int historyIndex = round * 4;

                            sumA *= forgetfulnessA;
                            sumA += weights[i, historyIndex + (moveA ? 1 : 0)];
                            sumA += weights[i, historyIndex + 2 + (moveB ? 1 : 0)];

                            sumB *= forgetfulnessB;
                            sumB += weights[j, historyIndex + (moveB ? 1 : 0)];
                            sumB += weights[j, historyIndex + 2 + (moveA ? 1 : 0)];
                        }

                        roundScores[i, j] = scoreA;
                        roundScores[j, i] = scoreB;
                    }
                });

                for (int i = 0; i < playerCount; i++)
                {
                    int score = 0;
                    for (int j = 0; j < playerCount; j++)
                    {
                        score += roundScores[i, j];
                    }
                    results[i] = score;
                }

                var playerToRemove = results.ToArray()
                    .Select((score, player) => (score, player))
                    .OrderBy(x => x.score);

                newPlayers = new int[playerToRemovePerRound];
                int index = 0;
                foreach ((int score, int player) in playerToRemove.Take(playerToRemovePerRound))
                {
                    newPlayers[index] = player;
                    for (int j = 0; j < weigthSize; j++)
                    {
                        weights[player, j] = ((float)random.NextDouble() * 2) - 1;
                    }
                    index++;
                }

                for (int i = 0; i < playerCount; i++)
                {
                    for (int x = 0; x < mutationPerIteration; x++)
                    {
                        int mutationIndex = random.Next(0, weigthSize);
                        weights[i, mutationIndex] *= (float)random.NextDouble();
                        weights[i, mutationIndex] += ((float)random.NextDouble() * 2) - 1;
                    }
                }

                const int printInterval = 10000;
                if (iteration % printInterval == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(iteration + "----------------------------");
                    Console.WriteLine("Average time per iteration :" + stopwatch.ElapsedMilliseconds / (float)printInterval + "ms");
                    foreach (var playerScore in playerToRemove.Reverse())
                    {
                        Console.WriteLine("Player " + playerScore.player + " = " + playerScore.score);
                    }

                    int player = playerToRemove.Last().player;
                    float[] playersWeights = Enumerable.Range(0, weigthSize)
                        .Select(x => weights[player, x])
                        .ToArray();

                    SaveStrategy(playersWeights, iteration);

                    stopwatch.Restart();
                }
            }
        }

        private static void SaveStrategy(float[] weigths, int generation)
        {
            string weigthsString = string.Join(", ", weigths.Select(x => x.ToString(CultureInfo.InvariantCulture)));

            string python = $@"def strategy(history, memory):
	weightSize = {weigths.Length - 2}
	weights = [{weigthsString}]
	round = history.shape[1]
	
	if (round == 0):
		memory = weights[len(weights) - 2]
	else:
		forgetfulness = weights[len(weights) - 1]
		memory *= forgetfulness;
		memory += weights[(round * 4 + (1 if history[0, round - 1] else 0)) % weightSize];
		memory += weights[(round * 4 + 2 + (1 if history[1, round - 1] else 0)) % weightSize];

	if (memory > 0):
		return ""stay silent"", memory


	return ""tell truth"", memory";

            File.WriteAllTextAsync(Path.Combine(@"C:\Users\Olivier\Documents\Projects\PrisonersDilemmaTournament-policestation\code\exampleStrats", "RandomWeights_Gen_" + generation + ".py"), python, Encoding.UTF8);
        }
    }
}
