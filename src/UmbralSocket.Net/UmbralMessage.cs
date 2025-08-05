using System;

namespace UmbralSocket.Net;

/// <summary>
/// Represents a message in the Umbral protocol containing an opcode and payload.
/// </summary>
/// <param name="Opcode">The operation code that identifies the message type.</param>
/// <param name="Payload">The message payload data.</param>
public readonly record struct UmbralMessage(byte Opcode, ReadOnlyMemory<byte> Payload);
