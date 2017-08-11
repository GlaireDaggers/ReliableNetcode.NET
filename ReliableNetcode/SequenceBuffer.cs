using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ReliableNetcode.Utils;

namespace ReliableNetcode
{
	internal class SequenceBuffer<T> where T : class, new()
	{
		private const uint NULL_SEQUENCE = 0xFFFFFFFF;

		public int Size
		{
			get { return numEntries; }
		}

		public ushort sequence;
		int numEntries;
		uint[] entrySequence;
		T[] entryData;

		public SequenceBuffer(int bufferSize)
		{
			this.sequence = 0;
			this.numEntries = bufferSize;

			this.entrySequence = new uint[bufferSize];
			for (int i = 0; i < bufferSize; i++)
				this.entrySequence[i] = NULL_SEQUENCE;

			this.entryData = new T[bufferSize];
			for (int i = 0; i < bufferSize; i++)
				this.entryData[i] = new T();
		}

		public void Reset()
		{
			this.sequence = 0;
			for (int i = 0; i < numEntries; i++)
				this.entrySequence[i] = NULL_SEQUENCE;
		}

		public void RemoveEntries(int startSequence, int finishSequence)
		{
			if (finishSequence < startSequence)
				finishSequence += 65536;

			if (finishSequence - startSequence < numEntries)
			{
				for (int sequence = startSequence; sequence <= finishSequence; sequence++)
				{
					entrySequence[sequence % numEntries] = NULL_SEQUENCE;
				}
			}
			else
			{
				for (int i = 0; i < numEntries; i++)
				{
					entrySequence[i] = NULL_SEQUENCE;
				}
			}
		}

		public bool TestInsert(ushort sequence)
		{
			return !PacketIO.SequenceLessThan(sequence, (ushort)(this.sequence - numEntries));
		}

		public T Insert(ushort sequence)
		{
			if (PacketIO.SequenceLessThan(sequence, (ushort)(this.sequence - numEntries)))
				return null;

			if (PacketIO.SequenceGreaterThan((ushort)(sequence + 1), this.sequence))
			{
				RemoveEntries(this.sequence, sequence);
				this.sequence = (ushort)(sequence + 1);
			}

			int index = sequence % numEntries;
			this.entrySequence[index] = sequence;
			return this.entryData[index];
		}

		public void Remove(ushort sequence)
		{
			this.entrySequence[sequence % numEntries] = NULL_SEQUENCE;
		}

		public bool Available(ushort sequence)
		{
			return this.entrySequence[sequence % numEntries] == NULL_SEQUENCE;
		}

		public bool Exists(ushort sequence)
		{
			return this.entrySequence[sequence % numEntries] == sequence;
		}

		public T Find(ushort sequence)
		{
			int index = sequence % numEntries;
			uint sequenceNum = this.entrySequence[index];
			if (sequenceNum == sequence)
				return this.entryData[index];
			else
				return null;
		}

		public T AtIndex(int index)
		{
			uint sequenceNum = this.entrySequence[index];
			if (sequenceNum == NULL_SEQUENCE)
				return null;

			return this.entryData[index];
		}

		public void GenerateAckBits(out ushort ack, out uint ackBits)
		{
			ack = (ushort)(this.sequence - 1);
			ackBits = 0;

			uint mask = 1;
			for (int i = 0; i < 32; i++)
			{
				ushort sequence = (ushort)(ack - i);
				if (Exists(sequence))
					ackBits |= mask;

				mask <<= 1;
			}
		}
	}
}
