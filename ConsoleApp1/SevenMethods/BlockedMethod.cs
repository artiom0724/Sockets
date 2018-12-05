using MPI;
using MPICalculator.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Environment = MPI.Environment;

namespace MPICalculator.SevenMethods
{
	public class BlockedMethod: CalculatingEntity
	{
		public void Execute(string[] parameters)
		{
			using (new Environment(ref parameters))
			{
				var communicator = Communicator.world;
				Rank = communicator.Rank;
				Size = communicator.Size;

				if (Rank == 0)
				{
					var timer = new Stopwatch();
					timer.Restart();

					var matrixA = Matrix.LoadMatrixFromFile($"{FilesPath}{Path.DirectorySeparatorChar}MatrixA", M, N);
					var matrixB = Matrix.LoadMatrixFromFile($"{FilesPath}{Path.DirectorySeparatorChar}MatrixB", M, N);

					for (var i = 1; i < Size; i++)
					{
						communicator.Send(matrixA, i, 0);
						communicator.Send(matrixB, i, 1);
					}

					var resultMatrix = new Matrix(M, N);
					for (var i = 1; i < Size; i++)
					{
						resultMatrix += communicator.Receive<Matrix>(i, 3);
					}

					Matrix.WriteMatrixToFile($"{FilesPath}{Path.DirectorySeparatorChar}MatrixR", resultMatrix);

					timer.Stop();
					Console.WriteLine($"Elapsed time {timer.ElapsedMilliseconds} ms with {Size} processes.");
				}
				else
				{
					var matrixA = communicator.Receive<Matrix>(0, 0);
					var matrixB = communicator.Receive<Matrix>(0, 1);

					var resultMatrix = matrixA * matrixB;

					communicator.Send(resultMatrix, 0, 3);
				}
			}
		}

		public void SetParams(int v1, int v2)
		{
			M = v1;
			N = v2;
		}
	}
}
