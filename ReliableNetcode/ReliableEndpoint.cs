using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ReliableNetcode.Utils;

namespace ReliableNetcode
{
	/// <summary>
	/// Quality-of-service type for a message
	/// </summary>
	public enum QosType : byte
	{
		/// <summary>
		/// Message is guaranteed to arrive and in order
		/// </summary>
		Reliable = 0,

		/// <summary>
		/// Message is not guaranteed delivery nor order
		/// </summary>
		Unreliable = 1,

		/// <summary>
		/// Message is not guaranteed delivery, but will be in order
		/// </summary>
		UnreliableOrdered = 2
	}

	/// <summary>
	/// Main class for routing messages through QoS channels
	/// </summary>
	public class ReliableEndpoint
	{
		/// <summary>
		/// Method which will be called to transmit raw datagrams over the network
		/// </summary>
		public Action<byte[], int> TransmitCallback;

		/// <summary>
		/// Method which will be called when messages are received
		/// </summary>
		public Action<byte[], int> ReceiveCallback;

		// Index, buffer, bufferLength
		public Action<uint, byte[], int> TransmitExtendedCallback;
		public Action<uint, byte[], int> ReceiveExtendedCallback;
		public uint Index = uint.MaxValue;

		/// <summary>
		/// Approximate round-trip-time
		/// </summary>
		public float RTT => _reliableChannel.RTT;

		/// <summary>
		/// Approximate packet loss
		/// </summary>
		public float PacketLoss => _reliableChannel.PacketLoss;

		/// <summary>
		/// Approximate send bandwidth
		/// </summary>
		public float SentBandwidthKBPS => _reliableChannel.SentBandwidthKBPS;

		/// <summary>
		/// Approximate received bandwidth
		/// </summary>
		public float ReceivedBandwidthKBPS => _reliableChannel.ReceivedBandwidthKBPS;

		private MessageChannel[] messageChannels;
		private double time = 0.0;

		// the reliable channel
		private ReliableMessageChannel _reliableChannel;

		public ReliableEndpoint()
		{
			time = DateTime.Now.GetTotalSeconds();

			_reliableChannel = new ReliableMessageChannel() { TransmitCallback = this.transmitMessage, ReceiveCallback = this.receiveMessage };

			messageChannels = new MessageChannel[]
			{
				_reliableChannel,
				new UnreliableMessageChannel() { TransmitCallback = this.transmitMessage, ReceiveCallback = this.receiveMessage },
				new UnreliableOrderedMessageChannel() { TransmitCallback = this.transmitMessage, ReceiveCallback = this.receiveMessage },
			};
		}

		public ReliableEndpoint(uint index) : this()
		{
			Index = index;
		}

		/// <summary>
		/// Reset the endpoint
		/// </summary>
		public void Reset()
		{
			for (int i = 0; i < messageChannels.Length; i++)
				messageChannels[i].Reset();
		}

		/// <summary>
		/// Update the endpoint with the current time
		/// </summary>
		public void Update()
		{
			Update(DateTime.Now.GetTotalSeconds());
		}

		/// <summary>
		/// Manually step the endpoint forward by increment in seconds
		/// </summary>
		public void UpdateFastForward(double increment)
		{
			this.time += increment;
			Update(this.time);
		}

		/// <summary>
		/// Update the endpoint with a specific time value
		/// </summary>
		public void Update(double time)
		{
			this.time = time;

			for (int i = 0; i < messageChannels.Length; i++)
				messageChannels[i].Update(this.time);
		}

		/// <summary>
		/// Call this when a datagram has been received over the network
		/// </summary>
		public void ReceivePacket(byte[] buffer, int bufferLength)
		{
			int channel = buffer[1];
			messageChannels[channel].ReceivePacket(buffer, bufferLength);
		}

		/// <summary>
		/// Send a message with the given QoS level
		/// </summary>
		public void SendMessage(byte[] buffer, int bufferLength, QosType qos)
		{
			messageChannels[(int)qos].SendMessage(buffer, bufferLength);
		}

		protected void receiveMessage(byte[] buffer, int length)
		{
			if (ReceiveCallback != null)
				ReceiveCallback(buffer, length);

			if (ReceiveExtendedCallback != null)
				ReceiveExtendedCallback(Index, buffer, length);
		}

		protected void transmitMessage(byte[] buffer, int length)
		{
			if (TransmitCallback != null)
				TransmitCallback(buffer, length);

			if (TransmitExtendedCallback != null)
				TransmitExtendedCallback(Index, buffer, length);
		}
	}
}
