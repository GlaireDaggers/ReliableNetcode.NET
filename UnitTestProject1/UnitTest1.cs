using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ReliableNetcode;

namespace UnitTestProject1
{
	[TestClass]
	public class UnitTest1
	{
		// tests with randomness are run this many times to ensure tests are unlikely to randomly pass
		const int RANDOM_RUNS = 100;

		/// <summary>
		/// All messages are sent through the Reliable channel with no packet loss. All messages should be received.
		/// </summary>
		[TestMethod]
		public void TestBasicSending()
		{
			List<byte> sentPackets = new List<byte>();
			List<byte> receivedPackets = new List<byte>();

			ReliableEndpoint endpoint1 = new ReliableEndpoint();
			endpoint1.ReceiveCallback = (buffer, size) =>
			{
				if (buffer[0] == 0)
					receivedPackets.Add(buffer[1]);
			};
			endpoint1.TransmitCallback = (buffer, size) =>
			{
				endpoint1.ReceivePacket(buffer, size);
			};

			for (int i = 0; i < 10; i++)
			{
				sentPackets.Add((byte)i);
				byte[] test = new byte[256];
				test[0] = 0;
				test[1] = (byte)i;
				endpoint1.SendMessage(test, 256, QosType.Reliable);
			}

			int iterations = 0;
			for (int i = 0; i < 5000; i++)
			{
				endpoint1.UpdateFastForward(1.0);
				iterations++;

				if (receivedPackets.Count == sentPackets.Count) break;
			}

			if (receivedPackets.Count == sentPackets.Count)
			{
				bool compare = true;
				for (int i = 0; i < receivedPackets.Count; i++)
				{
					if (receivedPackets[i] != sentPackets[i])
					{
						compare = false;
						break;
					}
				}

				if (!compare)
				{
					throw new System.Exception("Received packet contents differ!");
				}
			}
			else
			{
				throw new System.Exception((sentPackets.Count - receivedPackets.Count) + " packets not received");
			}
		}

		/// <summary>
		/// Test ushort message ID wrapping
		/// </summary>
		[TestMethod]
		public void TestIDWrapping()
		{
			List<byte> sentPackets = new List<byte>();
			List<byte> receivedPackets = new List<byte>();

			ReliableEndpoint endpoint1 = new ReliableEndpoint();
			endpoint1.ReceiveCallback = (buffer, size) =>
			{
				if (buffer[0] == 0)
					receivedPackets.Add(buffer[1]);
			};
			endpoint1.TransmitCallback = (buffer, size) =>
			{
				endpoint1.ReceivePacket(buffer, size);
			};

			for (int i = 0; i < 68000; i++)
			{
				sentPackets.Add((byte)i);
				byte[] test = new byte[256];
				test[0] = 0;
				test[1] = (byte)i;
				endpoint1.SendMessage(test, 256, QosType.Reliable);

				endpoint1.UpdateFastForward(0.1);
			}

			while (receivedPackets.Count < sentPackets.Count)
			{
				endpoint1.UpdateFastForward(1.0);
			}

			if (receivedPackets.Count == sentPackets.Count)
			{
				bool compare = true;
				for (int i = 0; i < receivedPackets.Count; i++)
				{
					if (receivedPackets[i] != sentPackets[i])
					{
						compare = false;
						break;
					}
				}

				if (!compare)
				{
					throw new System.Exception("Received packet contents differ!");
				}
			}
			else
			{
				throw new System.Exception((sentPackets.Count - receivedPackets.Count) + " packets not received");
			}
		}

		/// <summary>
		/// Packets are sent through the Reliable channel with random packet loss. All packets should be received
		/// </summary>
		[TestMethod]
		public void TestReliability()
		{
			Random rand = new Random();

			for (int run = 0; run < RANDOM_RUNS; run++)
			{
				Console.WriteLine("RUN: " + run);

				List<byte> sentPackets = new List<byte>();
				List<byte> receivedPackets = new List<byte>();

				int droppedPackets = 0;

				ReliableEndpoint endpoint1 = new ReliableEndpoint();
				endpoint1.ReceiveCallback = (buffer, size) =>
				{
					if (buffer[0] == 0)
						receivedPackets.Add(buffer[1]);
				};
				endpoint1.TransmitCallback = (buffer, size) =>
				{
					if (rand.Next(100) > 50)
						endpoint1.ReceivePacket(buffer, size);
					else
						droppedPackets++;
				};

				for (int i = 0; i < 100; i++)
				{
					sentPackets.Add((byte)i);
					byte[] test = new byte[256];
					test[0] = 0;
					test[1] = (byte)i;
					endpoint1.SendMessage(test, 256, QosType.Reliable);

					endpoint1.UpdateFastForward(0.1);
				}

				int iterations = 0;
				while (receivedPackets.Count < sentPackets.Count)
				{
					endpoint1.UpdateFastForward(1.0);
					iterations++;
				}

				Console.WriteLine("Dropped packets: " + droppedPackets);

				if (receivedPackets.Count == sentPackets.Count)
				{
					bool compare = true;
					for (int i = 0; i < receivedPackets.Count; i++)
					{
						if (receivedPackets[i] != sentPackets[i])
						{
							compare = false;
							break;
						}
					}

					if (!compare)
					{
						throw new System.Exception("Received packet contents differ!");
					}
				}
				else
				{
					throw new System.Exception((sentPackets.Count - receivedPackets.Count) + " packets not received");
				}
			}
		}

		/// <summary>
		/// All packets are sent through the Unreliable channel with no packet loss. All packets should be received.
		/// </summary>
		[TestMethod]
		public void TestBasicUnreliable()
		{
			List<byte> sentPackets = new List<byte>();
			List<byte> receivedPackets = new List<byte>();

			ReliableEndpoint endpoint1 = new ReliableEndpoint();
			endpoint1.ReceiveCallback = (buffer, size) =>
			{
				if (buffer[0] == 0)
					receivedPackets.Add(buffer[1]);
			};
			endpoint1.TransmitCallback = (buffer, size) =>
			{
				endpoint1.ReceivePacket(buffer, size);
			};

			for (int i = 0; i < 100; i++)
			{
				sentPackets.Add((byte)i);
				byte[] test = new byte[256];
				test[0] = 0;
				test[1] = (byte)i;
				endpoint1.SendMessage(test, 256, QosType.Unreliable);
			}

			if (receivedPackets.Count == sentPackets.Count)
			{
				bool compare = true;
				for (int i = 0; i < receivedPackets.Count; i++)
				{
					if (receivedPackets[i] != sentPackets[i])
					{
						compare = false;
						break;
					}
				}

				if (!compare)
				{
					throw new System.Exception("Received packet contents differ!");
				}
			}
			else
			{
				throw new System.Exception((sentPackets.Count - receivedPackets.Count) + " packets not received");
			}
		}

		/// <summary>
		/// All packets are sent through the UnreliableOrdered channel with no packet loss. All packets should be received.
		/// </summary>
		[TestMethod]
		public void TestBasicUnreliableOrdered()
		{
			List<byte> sentPackets = new List<byte>();
			List<byte> receivedPackets = new List<byte>();

			ReliableEndpoint endpoint1 = new ReliableEndpoint();
			endpoint1.ReceiveCallback = (buffer, size) =>
			{
				if (buffer[0] == 0)
					receivedPackets.Add(buffer[1]);
			};
			endpoint1.TransmitCallback = (buffer, size) =>
			{
				endpoint1.ReceivePacket(buffer, size);
			};

			for (int i = 0; i < 100; i++)
			{
				sentPackets.Add((byte)i);
				byte[] test = new byte[256];
				test[0] = 0;
				test[1] = (byte)i;
				endpoint1.SendMessage(test, 256, QosType.UnreliableOrdered);
			}

			if (receivedPackets.Count == sentPackets.Count)
			{
				bool compare = true;
				for (int i = 0; i < receivedPackets.Count; i++)
				{
					if (receivedPackets[i] != sentPackets[i])
					{
						compare = false;
						break;
					}
				}

				if (!compare)
				{
					throw new System.Exception("Received packet contents differ!");
				}
			}
			else
			{
				throw new System.Exception((sentPackets.Count - receivedPackets.Count) + " packets not received");
			}
		}

		/// <summary>
		/// All packets are sent through the UnreliableOrdered channel with random reordering. Received packets should be in order.
		/// </summary>
		[TestMethod]
		public void TestUnreliableOrderedSequence()
		{
			Random rand = new Random();

			for (int run = 0; run < RANDOM_RUNS; run++)
			{
				Console.WriteLine("RUN: " + run);

				List<byte> sentPackets = new List<byte>();
				List<byte> receivedPackets = new List<byte>();

				List<byte[]> testQueue = new List<byte[]>();

				ReliableEndpoint endpoint1 = new ReliableEndpoint();
				endpoint1.ReceiveCallback = (buffer, size) =>
				{
					if (buffer[0] == 0)
						receivedPackets.Add(buffer[1]);
				};
				endpoint1.TransmitCallback = (buffer, size) =>
				{
					int index = testQueue.Count;
					if (rand.Next(100) >= 50)
						index = rand.Next(testQueue.Count);

					byte[] item = new byte[size];
					Buffer.BlockCopy(buffer, 0, item, 0, size);

					testQueue.Insert(index, item);
				};

				// semi-randomly enqueue packets
				for (int i = 0; i < 10; i++)
				{
					sentPackets.Add((byte)i);
					byte[] test = new byte[256];
					test[0] = 0;
					test[1] = (byte)i;
					endpoint1.SendMessage(test, 256, QosType.UnreliableOrdered);
				}

				// now dequeue all packets
				while (testQueue.Count > 0)
				{
					var item = testQueue[0];
					testQueue.RemoveAt(0);

					endpoint1.ReceivePacket(item, item.Length);
				}

				// and verify that packets aren't out of order or duplicated
				List<int> processed = new List<int>();
				int sequence = 0;
				for (int i = 0; i < receivedPackets.Count; i++)
				{
					if (receivedPackets[i] < sequence)
					{
						throw new System.Exception("Found out-of-order packet!");
					}

					if (processed.Contains(receivedPackets[i]))
						throw new System.Exception("Found duplicate packet!");

					processed.Add(receivedPackets[i]);
					sequence = receivedPackets[i];
				}

				Console.WriteLine("Dropped packets: " + (sentPackets.Count - receivedPackets.Count));
			}
		}
	}
}
