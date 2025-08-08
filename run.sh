#!/bin/bash

# Ensure socket directories exist with proper permissions
mkdir -p /data/sockets /data/secure
chmod 755 /data/sockets /data/secure

# Clean up any existing socket files
rm -f /data/sockets/*.sock /data/secure/*.sock

case "$1" in
  "sample")
    echo "Running UmbralSocket.Net.Sample..."
    exec ./samples/UmbralSocket.Net.Sample/UmbralSocket.Net.Sample.csproj "${@:2}"
    ;;
  "pingpong")
    echo "Running PingPong Zero-Copy Demo..."
    exec ./samples/PingPongZeroCopy/PingPongZeroCopy.csproj "${@:2}"
    ;;
  "secure")
    echo "Running Secure Server Demo (Unix Domain Sockets)..."
    exec ./samples/secure/SecureServer "${@:2}"
    ;;
  "server")
    echo "Running UmbralSocket.Net.Sample in server mode..."
    exec ./samples/UmbralSocket.Net.Sample/UmbralSocket.Net.Sample.csproj server "${@:2}"
    ;;
  "client")
    echo "Running UmbralSocket.Net.Sample in client mode..."
    # Add a small delay to ensure server is ready
    sleep 2
    exec ./samples/UmbralSocket.Net.Sample client "${@:2}"
    ;;
  "diagnostic")
    echo "Running UmbralSocket.Net.Sample in diagnostic mode..."
    # Add a small delay to ensure server is ready
    sleep 2
    exec ./samples/UmbralSocket.Net.Sample/UmbralSocket.Net.Sample.csproj diagnostic "${@:2}"
    ;;
  "help"|"--help")
    echo "Available commands:"
    echo "  sample     - Run the main UmbralSocket.Net.Sample (interactive)"
    echo "  pingpong   - Run the PingPong Zero-Copy demonstration"
    echo "  secure     - Run the Secure Server demonstration"
    echo "  server     - Run the main sample in server mode"
    echo "  client     - Run the main sample in client mode"
    echo "  diagnostic - Run socket diagnostic tests"
    echo "  help       - Show this help message"
    ;;
  *)
    echo "Usage: $0 {sample|pingpong|secure|server|client|diagnostic|help} [args...]"
    echo "Use '$0 help' for more information."
    exit 1
    ;;
esac
