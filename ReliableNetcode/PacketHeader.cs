using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ReliableNetcode.Utils;

namespace ReliableNetcode
{
    internal class SentPacketData
    {
        public double time;
        public bool acked;
        public uint packetBytes;
    }

    internal class ReceivedPacketData
    {
        public double time;
        public uint packetBytes;
    }

    internal class FragmentReassemblyData
    {
        public ushort Sequence;
        public ushort Ack;
        public uint AckBits;
        public int NumFragmentsReceived;
        public int NumFragmentsTotal;
        public ByteBuffer PacketDataBuffer = new ByteBuffer();
        public int PacketBytes;
        public int HeaderOffset;
        public bool[] FragmentReceived = new bool[256];

        public void StoreFragmentData(byte channelID, ushort sequence, ushort ack, uint ackBits, int fragmentID, int fragmentSize, byte[] fragmentData, int fragmentBytes)
        {
            int copyOffset = 0;

            if (fragmentID == 0) {
                byte[] packetHeader = BufferPool.GetBuffer(Defines.MAX_PACKET_HEADER_BYTES);
                int headerBytes = PacketIO.WritePacketHeader(packetHeader, channelID, sequence, ack, ackBits);
                this.HeaderOffset = Defines.MAX_PACKET_HEADER_BYTES - headerBytes;

                if (this.PacketDataBuffer.Length < (Defines.MAX_PACKET_HEADER_BYTES + fragmentSize))
                    this.PacketDataBuffer.SetSize(Defines.MAX_PACKET_HEADER_BYTES + fragmentSize);

                this.PacketDataBuffer.BufferCopy(packetHeader, 0, this.HeaderOffset, headerBytes);
                copyOffset = headerBytes;

                fragmentBytes -= headerBytes;

                BufferPool.ReturnBuffer(packetHeader);
            }

            int writePos = Defines.MAX_PACKET_HEADER_BYTES + fragmentID * fragmentSize;
            int end = writePos + fragmentBytes;

            if (this.PacketDataBuffer.Length < end)
                this.PacketDataBuffer.SetSize(end);

            if (fragmentID == NumFragmentsTotal - 1) {
                this.PacketBytes = (this.NumFragmentsTotal - 1) * fragmentSize + fragmentBytes;
            }

            this.PacketDataBuffer.BufferCopy(fragmentData, copyOffset, Defines.MAX_PACKET_HEADER_BYTES + fragmentID * fragmentSize, fragmentBytes);
        }
    }
}
