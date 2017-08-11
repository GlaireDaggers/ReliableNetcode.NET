using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReliableNetcode.Utils
{
	internal static class PacketIO
	{
		public static bool SequenceGreaterThan(ushort s1, ushort s2)
		{
			return ((s1 > s2) && (s1 - s2 <= 32768)) ||
				((s1 < s2) && (s2 - s1 > 32768));
		}

		public static bool SequenceLessThan(ushort s1, ushort s2)
		{
			return SequenceGreaterThan(s2, s1);
		}

		public static int ReadPacketHeader(byte[] packetBuffer, int offset, int bufferLength, out ushort sequence, out ushort ack, out uint ackBits)
		{
			if (bufferLength < 3)
				throw new FormatException("Buffer too small for packet header");

			using (var reader = ByteArrayReaderWriter.Get(packetBuffer))
			{
				reader.SeekRead(offset);

				byte prefixByte = reader.ReadByte();
				if ((prefixByte & 1) != 0)
				{
					throw new InvalidOperationException("Header does not indicate regular packet");
				}

				sequence = reader.ReadUInt16();

				if ((prefixByte & (1 << 5)) != 0)
				{
					if (bufferLength < 3 + 1)
					{
						throw new FormatException("Buffer too small for packet header");
					}

					byte sequenceDiff = reader.ReadByte();
					ack = (ushort)(sequence - sequenceDiff);
				}
				else
				{
					if (bufferLength < 3 + 2)
					{
						throw new FormatException("Buffer too small for packet header");
					}

					ack = reader.ReadUInt16();
				}

				int expectedBytes = 0;
				for (int i = 0; i <= 4; i++)
				{
					if ((prefixByte & (1 << i)) != 0)
						expectedBytes++;
				}

				if (bufferLength < (bufferLength - reader.ReadPosition) + expectedBytes)
					throw new FormatException("Buffer too small for packet header");

				ackBits = 0xFFFFFFFF;

				if ((prefixByte & (1 << 1)) != 0)
				{
					ackBits &= 0xFFFFFF00;
					ackBits |= reader.ReadByte();
				}

				if ((prefixByte & (1 << 2)) != 0)
				{
					ackBits &= 0xFFFF00FF;
					ackBits |= (uint)(reader.ReadByte() << 8);
				}

				if ((prefixByte & (1 << 3)) != 0)
				{
					ackBits &= 0xFF00FFFF;
					ackBits |= (uint)(reader.ReadByte() << 16);
				}

				if ((prefixByte & (1 << 4)) != 0)
				{
					ackBits &= 0x00FFFFFF;
					ackBits |= (uint)(reader.ReadByte() << 24);
				}

				return (int)reader.ReadPosition - offset;
			}
		}

		public static int ReadFragmentHeader(byte[] packetBuffer, int offset, int bufferLength, int maxFragments, int fragmentSize, out int fragmentID, out int numFragments, out int fragmentBytes,
			out ushort sequence, out ushort ack, out uint ackBits, out byte channelID)
		{
			if (bufferLength < Defines.FRAGMENT_HEADER_BYTES)
				throw new FormatException("Buffer too small for packet header");

			using (var reader = ByteArrayReaderWriter.Get(packetBuffer))
			{
				byte prefixByte = reader.ReadByte();

				if (prefixByte != 1)
					throw new FormatException("Packet header indicates non-fragment packet");

				channelID = (byte)(prefixByte >> 6);

				sequence = reader.ReadUInt16();

				fragmentID = reader.ReadByte();
				numFragments = reader.ReadByte() + 1;

				if (numFragments > maxFragments)
					throw new FormatException("Packet header indicates fragments outside of max range");

				if (fragmentID >= numFragments)
					throw new FormatException("Packet header indicates fragment ID outside of fragment count");

				fragmentBytes = bufferLength - Defines.FRAGMENT_HEADER_BYTES;

				ushort packetSequence = 0;
				ushort packetAck = 0;
				uint packetAckBits = 0;

				if (fragmentID == 0)
				{
					int packetHeaderBytes = ReadPacketHeader(packetBuffer, Defines.FRAGMENT_HEADER_BYTES, bufferLength, out packetSequence, out packetAck, out packetAckBits);
					if (packetSequence != sequence)
						throw new FormatException("Bad packet sequence in fragment");

					fragmentBytes = bufferLength - packetHeaderBytes - Defines.FRAGMENT_HEADER_BYTES;
				}

				ack = packetAck;
				ackBits = packetAckBits;

				if (fragmentBytes > fragmentSize)
					throw new FormatException("Fragment bytes remaining > indicated fragment size");

				if (fragmentID != numFragments - 1 && fragmentBytes != fragmentSize)
					throw new FormatException("Fragment size invalid");

				return (int)reader.ReadPosition - offset;
			}
		}

		public static int WritePacketHeader(byte[] packetBuffer, byte channelID, ushort sequence, ushort ack, uint ackBits)
		{
			using (var writer = ByteArrayReaderWriter.Get(packetBuffer))
			{
				byte prefixByte = 0;

				if ((ackBits & 0x000000FF) != 0x000000FF)
					prefixByte |= (1 << 1);

				if ((ackBits & 0x0000FF00) != 0x0000FF00)
					prefixByte |= (1 << 2);

				if ((ackBits & 0x00FF0000) != 0x00FF0000)
					prefixByte |= (1 << 3);

				if ((ackBits & 0xFF000000) != 0xFF000000)
					prefixByte |= (1 << 4);

				int sequenceDiff = sequence - ack;
				if (sequenceDiff < 0)
					sequenceDiff += 65536;
				if (sequenceDiff <= 255)
					prefixByte |= (1 << 5);

				prefixByte |= (byte)( (channelID & 0x03) << 6 );

				writer.Write(prefixByte);
				writer.Write(sequence);

				if (sequenceDiff <= 255)
					writer.Write((byte)sequenceDiff);
				else
					writer.Write(ack);

				if ((ackBits & 0x000000FF) != 0x000000FF)
					writer.Write((byte)((ackBits & 0x000000FF)));

				if ((ackBits & 0x0000FF00) != 0x0000FF00)
					writer.Write((byte)((ackBits & 0x0000FF00) >> 8));

				if ((ackBits & 0x00FF0000) != 0x00FF0000)
					writer.Write((byte)((ackBits & 0x00FF0000) >> 16));

				if ((ackBits & 0xFF000000) != 0xFF000000)
					writer.Write((byte)((ackBits & 0xFF000000) >> 24));

				return (int)writer.WritePosition;
			}
		}
	}
}
