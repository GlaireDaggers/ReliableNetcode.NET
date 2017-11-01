using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ReliableNetcode.Utils;

namespace ReliableNetcode
{
    internal class ReliableConfig
    {
        public string Name;
        public int MaxPacketSize;
        public int FragmentThreshold;
        public int MaxFragments;
        public int FragmentSize;
        public int SentPacketBufferSize;
        public int ReceivedPacketBufferSize;
        public int FragmentReassemblyBufferSize;
        public float RTTSmoothFactor;
        public float PacketLossSmoothingFactor;
        public float BandwidthSmoothingFactor;
        public int PacketHeaderSize;

        public Action<byte[], int> TransmitPacketCallback;
        public Action<ushort, byte[], int> ProcessPacketCallback;
        public Action<ushort> AckPacketCallback;

        public static ReliableConfig DefaultConfig()
        {
            var config = new ReliableConfig();
            config.Name = "endpoint";
            config.MaxPacketSize = 16 * 1024;
            config.FragmentThreshold = 1024;
            config.MaxFragments = 16;
            config.FragmentSize = 1024;
            config.SentPacketBufferSize = 256;
            config.ReceivedPacketBufferSize = 256;
            config.FragmentReassemblyBufferSize = 64;
            config.RTTSmoothFactor = 0.25f;
            config.PacketLossSmoothingFactor = 0.1f;
            config.BandwidthSmoothingFactor = 0.1f;
            config.PacketHeaderSize = 28;

            return config;
        }
    }

    internal class ReliablePacketController
    {
        public ReliableConfig config;

        public float RTT
        {
            get { return rtt; }
        }

        public float PacketLoss
        {
            get { return packetLoss; }
        }

        public float SentBandwidthKBPS
        {
            get { return sentBandwidthKBPS; }
        }

        public float ReceivedBandwidthKBPS
        {
            get { return receivedBandwidthKBPS; }
        }

        public float AckedBandwidthKBPS
        {
            get { return ackedBandwidthKBPS; }
        }

        private double time;
        private float rtt;
        private float packetLoss;
        private float sentBandwidthKBPS;
        private float receivedBandwidthKBPS;
        private float ackedBandwidthKBPS;
        private ushort sequence;
        private SequenceBuffer<SentPacketData> sentPackets;
        private SequenceBuffer<ReceivedPacketData> receivedPackets;
        private SequenceBuffer<FragmentReassemblyData> fragmentReassembly;

        public ReliablePacketController(ReliableConfig config, double time)
        {
            this.config = config;
            this.time = time;

            this.sentPackets = new SequenceBuffer<SentPacketData>(config.SentPacketBufferSize);
            this.receivedPackets = new SequenceBuffer<ReceivedPacketData>(config.ReceivedPacketBufferSize);
            this.fragmentReassembly = new SequenceBuffer<FragmentReassemblyData>(config.FragmentReassemblyBufferSize);
        }

        public ushort NextPacketSequence()
        {
            return sequence;
        }

        public void Reset()
        {
            this.sequence = 0;

            for (int i = 0; i < config.FragmentReassemblyBufferSize; i++) {
                FragmentReassemblyData reassemblyData = fragmentReassembly.AtIndex(i);
                if (reassemblyData != null) {
                    reassemblyData.PacketDataBuffer.SetSize(0);
                }
            }

            sentPackets.Reset();
            receivedPackets.Reset();
            fragmentReassembly.Reset();
        }

        public void Update(double newTime)
        {
            this.time = newTime;

            // calculate packet loss
            {
                uint baseSequence = (uint)((sentPackets.sequence - config.SentPacketBufferSize + 1) + 0xFFFF);

                int numDropped = 0;
                int numSamples = config.SentPacketBufferSize / 2;
                for (int i = 0; i < numSamples; i++) {
                    ushort sequence = (ushort)(baseSequence + i);
                    var sentPacketData = sentPackets.Find(sequence);
                    if (sentPacketData != null && !sentPacketData.acked)
                        numDropped++;
                }

                float packetLoss = (float)numDropped / (float)numSamples;
                if (Math.Abs(this.packetLoss - packetLoss) > 0.00001f) {
                    this.packetLoss += (packetLoss - this.packetLoss) * config.PacketLossSmoothingFactor;
                }
                else {
                    this.packetLoss = packetLoss;
                }
            }

            // calculate sent bandwidth
            {
                uint baseSequence = (uint)((sentPackets.sequence - config.SentPacketBufferSize + 1) + 0xFFFF);

                int bytesSent = 0;
                double startTime = double.MaxValue;
                double finishTime = 0.0;
                int numSamples = config.SentPacketBufferSize / 2;
                for (int i = 0; i < numSamples; i++) {
                    ushort sequence = (ushort)(baseSequence + i);
                    var sentPacketData = sentPackets.Find(sequence);
                    if (sentPacketData == null) continue;

                    bytesSent += (int)sentPacketData.packetBytes;
                    startTime = Math.Min(startTime, sentPacketData.time);
                    finishTime = Math.Max(finishTime, sentPacketData.time);
                }

                if (startTime != double.MaxValue && finishTime != 0.0) {
                    float sentBandwidth = (float)bytesSent / (float)(finishTime - startTime) * 8f / 1000f;
                    if (Math.Abs(this.sentBandwidthKBPS - sentBandwidth) > 0.00001f) {
                        this.sentBandwidthKBPS += (sentBandwidth - this.sentBandwidthKBPS) * config.BandwidthSmoothingFactor;
                    }
                    else {
                        this.sentBandwidthKBPS = sentBandwidth;
                    }
                }
            }

            // calculate received bandwidth
            lock (receivedPackets)
            {
                uint baseSequence = (uint)((receivedPackets.sequence - config.ReceivedPacketBufferSize + 1) + 0xFFFF);

                int bytesReceived = 0;
                double startTime = double.MaxValue;
                double finishTime = 0.0;
                int numSamples = config.ReceivedPacketBufferSize / 2;
                for (int i = 0; i < numSamples; i++) {
                    ushort sequence = (ushort)(baseSequence + i);
                    var receivedPacketData = receivedPackets.Find(sequence);
                    if (receivedPacketData == null) continue;

                    bytesReceived += (int)receivedPacketData.packetBytes;
                    startTime = Math.Min(startTime, receivedPacketData.time);
                    finishTime = Math.Max(finishTime, receivedPacketData.time);
                }

                if (startTime != double.MaxValue && finishTime != 0.0) {
                    float receivedBandwidth = (float)bytesReceived / (float)(finishTime - startTime) * 8f / 1000f;
                    if (Math.Abs(this.receivedBandwidthKBPS - receivedBandwidth) > 0.00001f) {
                        this.receivedBandwidthKBPS += (receivedBandwidth - this.receivedBandwidthKBPS) * config.BandwidthSmoothingFactor;
                    }
                    else {
                        this.receivedBandwidthKBPS = receivedBandwidth;
                    }
                }
            }

            // calculate acked bandwidth
            {
                uint baseSequence = (uint)((sentPackets.sequence - config.SentPacketBufferSize + 1) + 0xFFFF);

                int bytesSent = 0;
                double startTime = double.MaxValue;
                double finishTime = 0.0;
                int numSamples = config.SentPacketBufferSize / 2;
                for (int i = 0; i < numSamples; i++) {
                    ushort sequence = (ushort)(baseSequence + i);
                    var sentPacketData = sentPackets.Find(sequence);
                    if (sentPacketData == null || sentPacketData.acked == false) continue;

                    bytesSent += (int)sentPacketData.packetBytes;
                    startTime = Math.Min(startTime, sentPacketData.time);
                    finishTime = Math.Max(finishTime, sentPacketData.time);
                }

                if (startTime != double.MaxValue && finishTime != 0.0) {
                    float ackedBandwidth = (float)bytesSent / (float)(finishTime - startTime) * 8f / 1000f;
                    if (Math.Abs(this.ackedBandwidthKBPS - ackedBandwidth) > 0.00001f) {
                        this.ackedBandwidthKBPS += (ackedBandwidth - this.ackedBandwidthKBPS) * config.BandwidthSmoothingFactor;
                    }
                    else {
                        this.ackedBandwidthKBPS = ackedBandwidth;
                    }
                }
            }
        }

        public void SendAck(byte channelID)
        {
            ushort ack;
            uint ackBits;

            lock( receivedPackets )
                receivedPackets.GenerateAckBits(out ack, out ackBits);

            byte[] transmitData = BufferPool.GetBuffer(16);
            int headerBytes = PacketIO.WriteAckPacket(transmitData, channelID, ack, ackBits);

            config.TransmitPacketCallback(transmitData, headerBytes);

            BufferPool.ReturnBuffer(transmitData);
        }

        public ushort SendPacket(byte[] packetData, int length, byte channelID)
        {
            if (length > config.MaxPacketSize)
                throw new ArgumentOutOfRangeException("Packet is too large to send, max packet size is " + config.MaxPacketSize + " bytes");

            ushort sequence = this.sequence++;
            ushort ack;
            uint ackBits;

            lock( receivedPackets )
                receivedPackets.GenerateAckBits(out ack, out ackBits);

            SentPacketData sentPacketData = sentPackets.Insert(sequence);
            sentPacketData.time = this.time;
            sentPacketData.packetBytes = (uint)(config.PacketHeaderSize + length);
            sentPacketData.acked = false;

            if (length <= config.FragmentThreshold) {
                // regular packet

                byte[] transmitData = BufferPool.GetBuffer(2048);
                int headerBytes = PacketIO.WritePacketHeader(transmitData, channelID, sequence, ack, ackBits);
                int transmitBufferLength = length + headerBytes;

                Buffer.BlockCopy(packetData, 0, transmitData, headerBytes, length);

                config.TransmitPacketCallback(transmitData, transmitBufferLength);

                BufferPool.ReturnBuffer(transmitData);
            }
            else {
                // fragmented packet

                byte[] packetHeader = BufferPool.GetBuffer(Defines.MAX_PACKET_HEADER_BYTES);

                int packetHeaderBytes = 0;

                try {
                    packetHeaderBytes = PacketIO.WritePacketHeader(packetHeader, channelID, sequence, ack, ackBits);
                }
                catch {
                    throw;
                }

                int numFragments = (length / config.FragmentSize) + ((length % config.FragmentSize) != 0 ? 1 : 0);
                //int fragmentBufferSize = Defines.FRAGMENT_HEADER_BYTES + Defines.MAX_PACKET_HEADER_BYTES + config.FragmentSize;

                byte[] fragmentPacketData = BufferPool.GetBuffer(2048);
                int qpos = 0;

                byte prefixByte = 1;
                prefixByte |= (byte)((channelID & 0x03) << 6);

                for (int fragmentID = 0; fragmentID < numFragments; fragmentID++) {
                    using (var writer = ByteArrayReaderWriter.Get(fragmentPacketData)) {
                        writer.Write(prefixByte);
                        writer.Write(channelID);
                        writer.Write(sequence);
                        writer.Write((byte)fragmentID);
                        writer.Write((byte)(numFragments - 1));

                        if (fragmentID == 0) {
                            writer.WriteBuffer(packetHeader, packetHeaderBytes);
                        }

                        int bytesToCopy = config.FragmentSize;
                        if (qpos + bytesToCopy > length)
                            bytesToCopy = length - qpos;

                        for (int i = 0; i < bytesToCopy; i++)
                            writer.Write(packetData[qpos++]);

                        int fragmentPacketBytes = (int)writer.WritePosition;
                        config.TransmitPacketCallback(fragmentPacketData, fragmentPacketBytes);
                    }
                }

                BufferPool.ReturnBuffer(packetHeader);
                BufferPool.ReturnBuffer(fragmentPacketData);
            }

            return sequence;
        }

        public void ReceivePacket(byte[] packetData, int bufferLength)
        {
            if (bufferLength > config.MaxPacketSize)
                throw new ArgumentOutOfRangeException("Packet is larger than max packet size");

            if (packetData == null)
                throw new InvalidOperationException("Tried to receive null packet!");

            if (bufferLength > packetData.Length)
                throw new InvalidOperationException("Buffer length exceeds actual packet length!");

            byte prefixByte = packetData[0];

            if ((prefixByte & 1) == 0) {
                // regular packet

                ushort sequence;
                ushort ack;
                uint ackBits;

                byte channelID;

                int packetHeaderBytes = PacketIO.ReadPacketHeader(packetData, 0, bufferLength, out channelID, out sequence, out ack, out ackBits);

                bool isStale;
                lock( receivedPackets )
                    isStale = !receivedPackets.TestInsert(sequence);

                if (!isStale && (prefixByte & 0x80) == 0) {
                    if (packetHeaderBytes >= bufferLength)
                        throw new FormatException("Buffer too small for packet data!");

                    ByteBuffer tempBuffer = ObjPool<ByteBuffer>.Get();
                    tempBuffer.SetSize(bufferLength - packetHeaderBytes);
                    tempBuffer.BufferCopy(packetData, packetHeaderBytes, 0, tempBuffer.Length);

                    // process packet
                    config.ProcessPacketCallback(sequence, tempBuffer.InternalBuffer, tempBuffer.Length);

                    // add to received buffer
                    lock (receivedPackets) {
                        ReceivedPacketData receivedPacketData = receivedPackets.Insert(sequence);

                        if (receivedPacketData == null)
                            throw new InvalidOperationException("Failed to insert received packet!");

                        receivedPacketData.time = this.time;
                        receivedPacketData.packetBytes = (uint)(config.PacketHeaderSize + bufferLength);
                    }

                    ObjPool<ByteBuffer>.Return(tempBuffer);
                }

                if (!isStale || (prefixByte & 0x80) != 0) {
                    for (int i = 0; i < 32; i++) {
                        if ((ackBits & 1) != 0) {
                            ushort ack_sequence = (ushort)(ack - i);
                            SentPacketData sentPacketData = sentPackets.Find(ack_sequence);

                            if (sentPacketData != null && !sentPacketData.acked) {
                                sentPacketData.acked = true;

                                if (config.AckPacketCallback != null)
                                    config.AckPacketCallback(ack_sequence);

                                float rtt = (float)(this.time - sentPacketData.time) * 1000.0f;
                                if ((this.rtt == 0f && rtt > 0f) || Math.Abs(this.rtt - rtt) < 0.00001f) {
                                    this.rtt = rtt;
                                }
                                else {
                                    this.rtt += (rtt - this.rtt) * config.RTTSmoothFactor;
                                }
                            }
                        }

                        ackBits >>= 1;
                    }
                }
            }
            else {
                // fragment packet

                int fragmentID;
                int numFragments;
                int fragmentBytes;

                ushort sequence;
                ushort ack;
                uint ackBits;

                byte fragmentChannelID;

                int fragmentHeaderBytes = PacketIO.ReadFragmentHeader(packetData, 0, bufferLength, config.MaxFragments, config.FragmentSize,
                    out fragmentID, out numFragments, out fragmentBytes, out sequence, out ack, out ackBits, out fragmentChannelID);

                FragmentReassemblyData reassemblyData = fragmentReassembly.Find(sequence);
                if (reassemblyData == null) {
                    reassemblyData = fragmentReassembly.Insert(sequence);

                    // failed to insert into buffer (stale)
                    if (reassemblyData == null)
                        return;

                    reassemblyData.Sequence = sequence;
                    reassemblyData.Ack = 0;
                    reassemblyData.AckBits = 0;
                    reassemblyData.NumFragmentsReceived = 0;
                    reassemblyData.NumFragmentsTotal = numFragments;
                    reassemblyData.PacketBytes = 0;
                    Array.Clear(reassemblyData.FragmentReceived, 0, reassemblyData.FragmentReceived.Length);
                }

                if (numFragments != reassemblyData.NumFragmentsTotal)
                    return;

                if (reassemblyData.FragmentReceived[fragmentID])
                    return;

                reassemblyData.NumFragmentsReceived++;
                reassemblyData.FragmentReceived[fragmentID] = true;

                byte[] tempFragmentData = BufferPool.GetBuffer(2048);
                Buffer.BlockCopy(packetData, fragmentHeaderBytes, tempFragmentData, 0, bufferLength - fragmentHeaderBytes);
                
                reassemblyData.StoreFragmentData(fragmentChannelID, sequence, ack, ackBits, fragmentID, config.FragmentSize, tempFragmentData, bufferLength - fragmentHeaderBytes);
                BufferPool.ReturnBuffer(tempFragmentData);

                if (reassemblyData.NumFragmentsReceived == reassemblyData.NumFragmentsTotal) {
                    // grab internal buffer and pass it to ReceivePacket. Internal buffer will be packet marked as normal packet, so it will go through normal packet path

                    // copy into new buffer to remove preceding offset (used to simplify variable length header handling)
                    ByteBuffer temp = ObjPool<ByteBuffer>.Get();
                    temp.SetSize(reassemblyData.PacketDataBuffer.Length - reassemblyData.HeaderOffset);
                    Buffer.BlockCopy(reassemblyData.PacketDataBuffer.InternalBuffer, reassemblyData.HeaderOffset, temp.InternalBuffer, 0, temp.Length);

                    // receive packet
                    this.ReceivePacket(temp.InternalBuffer, temp.Length);

                    // return temp buffer
                    ObjPool<ByteBuffer>.Return(temp);

                    // clear reassembly
                    reassemblyData.PacketDataBuffer.SetSize(0);
                    fragmentReassembly.Remove(sequence);
                }
            }
        }
    }
}
