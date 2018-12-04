using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ClientSocket.Helpers
{
    static public class ArrayHelper
    {
        public static T[] SubArray<T>(this T[] data, int index, long length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static T[] InsertInStartArray<T>(this T[] data, T[] inserting)
        {
            for(int i = 0; i < inserting.Length; i++)
            {
                data[i] = inserting[i];
            }
            return data;
        }

        public static T[] InsertInArray<T>(this T[] data, int startPosition, T[] inserting)
        {
            for (int i = 0; i < inserting.Length; i++)
            {
                data[startPosition + i] = inserting[i];
            }
            return data;
        }

        public static Stream AddToStream<T>(this Stream stream, T s)
        {
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

		public static byte[] StringToByteArray(this string str) => Encoding.ASCII.GetBytes(str);

		public static string ByteArrayToString(this byte[] bytes) => Encoding.ASCII.GetString(bytes, 0, bytes.Length);

		public static void OpenFile(string filePath)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				Process.Start(new ProcessStartInfo("cmd", $"/c {filePath}"));
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				Process.Start("gedit", filePath);
			}
		}

		public static void CheckFolder(string folderName)
		{
			Directory.CreateDirectory(folderName);
		}
	}
}