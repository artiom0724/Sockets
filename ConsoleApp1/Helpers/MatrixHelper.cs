using MPICalculator.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPICalculator.Helpers
{
	public class MatrixHelper : CalculatingEntity
	{
		public void Start(int _m, int _n)
		{
			M = _m;
			N = _n;

			var matrixA = new Matrix(M, N);
			matrixA.Random();
			Matrix.WriteMatrixToFile($"{FilesPath}{Path.DirectorySeparatorChar}MatrixA", matrixA);

			var matrixB = new Matrix(M, N);
			matrixB.Random();
			Matrix.WriteMatrixToFile($"{FilesPath}{Path.DirectorySeparatorChar}MatrixB", matrixB);
		}
	}
}
