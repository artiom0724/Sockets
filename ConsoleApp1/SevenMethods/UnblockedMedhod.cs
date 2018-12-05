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
	public class UnblockedMedhod : CalculatingEntity
	{
		public void SetParams(int v1, int v2)
		{
			M = v1;
			N = v2;
		}

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

					var sendRequests = new List<Request>();
					for (var i = 1; i < Size; i++)
					{
						sendRequests.Add(communicator.ImmediateSend(matrixA, i, 0));
						sendRequests.Add(communicator.ImmediateSend(matrixB, i, 1));
					}
					sendRequests.ForEach(r => r.Wait());

					var receiveRequests = new List<ReceiveRequest>();
					for (var i = 1; i < Size; i++)
					{
						receiveRequests.Add(communicator.ImmediateReceive<Matrix>(i, 3));
					}

					var resultMatrix = new Matrix(M, N);
					receiveRequests.ForEach(rr =>
					{
						rr.Wait();
						resultMatrix += (Matrix)rr.GetValue();
					});

					Matrix.WriteMatrixToFile($"{FilesPath}{Path.DirectorySeparatorChar}MatrixR", resultMatrix);

					timer.Stop();
					Console.WriteLine($"Elapsed time {timer.ElapsedMilliseconds} ms with {Size} processes.");
				}
				else
				{
					var matrixAReceiveRequest = communicator.ImmediateReceive<Matrix>(0, 0);
					var matrixBReceiveRequest = communicator.ImmediateReceive<Matrix>(0, 1);

					matrixAReceiveRequest.Wait();
					matrixBReceiveRequest.Wait();

					var matrixA = (Matrix)matrixAReceiveRequest.GetValue();
					var matrixB = (Matrix)matrixBReceiveRequest.GetValue();
					var resultMatrix = matrixA * matrixB;

					communicator.ImmediateSend(resultMatrix, 0, 3).Wait();
				}
			}
		}
	}
}
