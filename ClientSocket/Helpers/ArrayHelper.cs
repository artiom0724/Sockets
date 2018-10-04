using System;

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
    }
}