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
	public class GroupMethod: CalculatingEntity
	{
		private int GroupsCount;
		public void SetParams(int v1, int v2, int groupCount)
		{
			M = v1;
			N = v2;
			GroupsCount = groupCount;
		}

		public void Execute(string[] parameters)
		{
			using (new Environment(ref parameters))
			{
				var communicator = Communicator.world;

				var groupSize = communicator.Size / GroupsCount;

				var groupCommunicator = (Intracommunicator)Enumerable.Range(0, GroupsCount).Aggregate<int, Communicator>(null,
					(current, groupId) => communicator.Create(communicator.Group.IncludeOnly(
											  Enumerable.Range(0, communicator.Size).Skip(groupId * groupSize)
												  .Take(groupSize + (GroupsCount - 1 == groupId
															? communicator.Size % GroupsCount
															: 0)).ToArray())) ?? current);

				Rank = groupCommunicator.Rank;
				Size = groupCommunicator.Size;

				Matrix matrixA = null;
				Matrix matrixB = null;

				var timer = new Stopwatch();
				if (Rank == 0)
				{
					timer.Restart();
					matrixA = Matrix.LoadMatrixFromFile($"{FilesPath}{Path.DirectorySeparatorChar}MatrixA", M, N);
					matrixB = Matrix.LoadMatrixFromFile($"{FilesPath}{Path.DirectorySeparatorChar}MatrixB", M, N);
				}

				groupCommunicator.Broadcast(ref matrixA, 0);
				groupCommunicator.Broadcast(ref matrixB, 0);

				var matrixR = new Matrix(M, N);
				if (Rank != 0)
				{
					matrixR = matrixA * matrixB;
				}

				var partialMatrix = groupCommunicator.Gather(matrixR, 0);

				if (Rank == 0)
				{
					var groupId = Guid.NewGuid().ToString("N").Substring(0, 8);
					Matrix.WriteMatrixToFile($"{FilesPath}{Path.DirectorySeparatorChar}MatrixR-{groupId}",
						partialMatrix.Aggregate(new Matrix(M, N), (current, matrix) => current + matrix));

					timer.Stop();

					Console.WriteLine($"Group ({groupId}) of {groupCommunicator.Size} process(es) finished at {timer.ElapsedMilliseconds} ms ");
				}
			}
		}
	}
}
