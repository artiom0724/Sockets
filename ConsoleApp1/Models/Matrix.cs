using ClientSocket.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MPICalculator.Models
{
	[Serializable]
	public class Matrix
	{
		private readonly List<List<int>> _matrix;
		private int M { get; }
		private int N { get; }

		public Matrix(int i, int j)
		{
			_matrix = Enumerable.Range(0, i).Select(_ => Enumerable.Range(0, j).Select(__ => 0).ToList()).ToList();
			M = i;
			N = j;
		}

		public static Matrix operator *(Matrix m1, Matrix m2)
		{
			if (m1.M != m2.M || m1.N != m2.N)
			{
				throw new Exception("Can't multiply matrix with different dimensions.");
			}

			var resultMatrix = new Matrix(m1.M, m1.N);

			for (var i = CalculatingEntity.Rank - 1; i < m1.M; i += CalculatingEntity.Size - 1)
			{
				for (var j = 0; j < m1.N; j++)
				{
					for (var k = 0; k < m1.N; k++)
					{
						resultMatrix[i][j] += m1[i][k] * m2[k][j];
					}
				}
			}

			return resultMatrix;
		}

		public static Matrix operator +(Matrix m1, Matrix m2)
		{
			if (m1.M != m2.M || m1.N != m2.N)
			{
				throw new Exception("Can't multiply matrix with different dimensions.");
			}

			var resultMatrix = new Matrix(m1.M, m1.N);

			for (var i = 0; i < m1.M; i++)
			{
				for (var j = 0; j < m1.N; j++)
				{
					resultMatrix[i][j] = m1[i][j] + m2[i][j];
				}
			}

			return resultMatrix;
		}

		private List<int> this[int i] => _matrix[i];

		private List<int> Flatten => _matrix.SelectMany(_ => _).ToList();

		public void Random()
		{
			var random = new Random();
			foreach (var row in _matrix)
			{
				for (var j = 0; j < N; j++)
				{
					row[j] = random.Next(1, 99);
				}
			}
		}

		public static Matrix LoadMatrixFromFile(string filePath, int i, int j)
		{
			CheckPath(filePath);

			var matrix = new Matrix(i, j);
			using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				var buffer = new byte[file.Length];
				file.Read(buffer, 0, (int)file.Length);
				var numbers = buffer.ByteArrayToString().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(int.Parse).ToList();

				for (var x = 0; x < i; x++)
				{
					for (var y = 0; y < j; y++)
					{
						matrix[x][y] = numbers[x * j + y];
					}
				}
			}

			return matrix;
		}

		public static void WriteMatrixToFile(string filePath, Matrix matrix)
		{
			CheckPath(filePath);
			using (var file = new FileStream(filePath, FileMode.Create))
			{
				var buffer = string.Join(";", matrix.Flatten).StringToByteArray();
				file.Write(buffer, 0, buffer.Length);
			}
		}

		private static void CheckPath(string path)
		{
			Directory.CreateDirectory(new Regex($".*\\{Path.DirectorySeparatorChar}").Match(path).Value);
		}
	}
}
