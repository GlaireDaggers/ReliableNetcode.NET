using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReliableNetcode.Utils
{
	internal static class ObjPool<T> where T : new()
	{
		private static Queue<T> pool = new Queue<T>();

		public static T Get()
		{
            lock (pool) {
                if (pool.Count > 0)
                    return pool.Dequeue();
            }

			return new T();
		}

		public static void Return(T val)
		{
            lock (pool) {
                pool.Enqueue(val);
            }
		}
	}
}
