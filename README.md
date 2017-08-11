# ReliableNetcode.NET
A pure managed C# socket agnostic reliability layer inspired by reliable.io and yojimbo.

ReliableNetcode.NET provides a simple and easy-to-use reliability layer designed for use in games built on unreliable UDP connections. It is a great fit for use with [Netcode.IO.NET](https://github.com/KillaMaaki/Netcode.IO.NET), my C# port of Glenn Fiedler's secure UDP communication protocol, but it can also be used with any other system you want.

# Features
* Multiple quality-of-service options (reliable, unreliable, and unreliable-ordered) for different use cases in a single API
* Lightweight packet acking and packet resending
* Supports messages up to about 16kB large using automatic message fragmentation and reassembly.
* Simple congestion control changes the packet send rate according to round-trip-time.
* GC-friendly for maximum performance.

# Usage
All of the API is provided under the `ReliableNetcode` namespace.
Usage is really simple. Create a new instance of `ReliableEndpoint`:

```c#
var reliableEndpoint = new ReliableEndpoint();
reliableEndpoint.ReceiveCallback = ( buffer, size ) =>
{
	// this will be called when the endpoint extracts messages from received packets
	// buffer is byte[] and size is number of bytes in the buffer.
	// do not keep a reference to buffer as it will be pooled after this function returns
};
reliableEndpoint.TransmitCallback = ( buffer, size ) =>
{
	// this will be called when a datagram is ready to be sent across the network.
	// buffer is byte[] and size is number of bytes in the buffer
	// do not keep a reference to the buffer as it will be pooled after this function returns
};
```

To send a message, call `ReliableEndpoint.SendMessage`:

```c#
// Send a message through the qos channel
// messageBytes is a byte array, messageSize is number of bytes in the array, and qos type is either:
// QoSType.Reliable - message is guaranteed to arrive and in order.
// QoSType.Unreliable - message is not guaranteed delivery.
// QoSType.UnreliableOrdered - message is not guaranteed delivery, but received messages will be in order.
reliableEndpoint.SendMessage( messageBytes, messageSize, qosType );
```

When you receive a datagram from your socket implementation, use `ReliableEndpoint.ReceivePacket`:

```c#
// when you receive a datagram, pass the byte array and the number of bytes to ReceivePacket
// this will extract messages from the datagram and call your custom ReceiveCallback with any received messages.
reliableEndpoint.ReceivePacket( packetBytes, packetSize );
```

And, finally, make sure you call `ReliableEndpoint.Update` at regular and frequent intervals to update the internal buffers (you could run it on a separate network thread, from your game loop, or whatever you choose):
```c#
// update internal buffers
reliableEndpoint.Update();
```
