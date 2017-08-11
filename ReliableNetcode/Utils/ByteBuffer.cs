using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReliableNetcode.Utils
{
	internal class ByteBuffer
	{
		public int Length
		{
			get { return size; }
		}

		public byte[] InternalBuffer
		{
			get { return _buffer; }
		}

		protected byte[] _buffer;
		protected int size;

		public ByteBuffer()
		{
			_buffer = null;
			this.size = 0;
		}

		public ByteBuffer(int size = 0)
		{
			_buffer = new byte[size];
			this.size = size;
		}

		public void SetSize(int newSize)
		{
			if (_buffer == null || _buffer.Length < newSize)
			{
				byte[] newBuffer = new byte[newSize];

				if (_buffer != null)
					System.Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _buffer.Length);

				_buffer = newBuffer;
			}

			size = newSize;
		}

		public void BufferCopy(byte[] source, int src, int dest, int length)
		{
			System.Buffer.BlockCopy(source, src, _buffer, dest, length);
		}

		public void BufferCopy(ByteBuffer source, int src, int dest, int length)
		{
			System.Buffer.BlockCopy(source._buffer, src, _buffer, dest, length);
		}

		public byte this[int index]
		{
			get
			{
				if (index < 0 || index > size) throw new System.IndexOutOfRangeException();
				return _buffer[index];
			}
			set
			{
				if (index < 0 || index > size) throw new System.IndexOutOfRangeException();
				_buffer[index] = value;
			}
		}
	}
}
