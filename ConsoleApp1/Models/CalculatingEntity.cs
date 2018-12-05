using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPICalculator.Models
{
	public class CalculatingEntity
	{
		public static int Rank { get; set; }
		public static int Size { get; set; }

		protected static string FilesPath => $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}files";

		protected int M { get; set; }
		protected int N { get; set; }
	}
}
