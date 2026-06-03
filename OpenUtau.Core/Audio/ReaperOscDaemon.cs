using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.Core {
    public static class ReaperOSC {
        private const string ReaperIp = "127.0.0.1";
        private const int SendPort = 8000;
        private const int ReceivePort = 8001;
        
        private static UdpClient sender = new UdpClient();
        private static UdpClient receiver;
        private static Thread listenThread;
        public static bool ReaperIsPlaying { get; private set; } = false;
        public static bool IsReceivingOsc { get; private set; } = false;

        public static void Init() {
            try {
                receiver = new UdpClient(ReceivePort);
                listenThread = new Thread(ListenLoop) { IsBackground = true };
                listenThread.Start();
                Log.Information("Reaper OSC Listener started on port " + ReceivePort);
            } catch (Exception e) {
                Log.Error(e, "Failed to start OSC Listener for Reaper");
            }
        }

        private static void ListenLoop() {
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, ReceivePort);
            while (true) {
                try {
                    byte[] bytes = receiver.Receive(ref ep);
                    var messages = ParseOscPacket(bytes);
                    bool hasRelevantMessage = false;
                    foreach (var msg in messages) {
                        if (msg.Address == "/play") {
                            ReaperIsPlaying = msg.Value > 0.5f;
                            hasRelevantMessage = true;
                            Log.Information($"[OSC] Reaper Play: {ReaperIsPlaying}");
                        } 
                        else if (msg.Address == "/stop" || msg.Address == "/pause") {
                            if (msg.Value > 0.5f) ReaperIsPlaying = false;
                            hasRelevantMessage = true;
                            Log.Information($"[OSC] Reaper Stopped/Paused");
                        } 
                        else if (msg.Address == "/time") {
                            hasRelevantMessage = true;             
                        }
                    }
                    if (hasRelevantMessage) {
                        DocManager.Inst.PostOnUIThread(() => {
                            IsReceivingOsc = true; 
                            var project = DocManager.Inst.Project;

                            foreach (var msg in messages) {
                                string address = msg.Address;
                                float val = msg.Value;

                                if (address == "/play") {
                                    if (val > 0.5f) {
                                        if (!PlaybackManager.Inst.PlayingMaster) {
                                            PlaybackManager.Inst.Play(project, DocManager.Inst.playPosTick);
                                        }
                                    } else {
                                        if (PlaybackManager.Inst.PlayingMaster) {
                                            PlaybackManager.Inst.PausePlayback();
                                        }
                                    }
                                } 
                                else if (address == "/stop" || address == "/pause") {
                                    if (val > 0.5f && PlaybackManager.Inst.PlayingMaster) {
                                        PlaybackManager.Inst.PausePlayback();
                                    }
                                }
                                else if (address == "/time") {
                                    if (!PlaybackManager.Inst.PlayingMaster) {
                                        int tick = project.timeAxis.MsPosToTickPos(val * 1000.0);
                                        DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification(tick));
                                    }
                                }
                            }
                            IsReceivingOsc = false; 
                        });
                    }
                } catch (SocketException) {
                    break;
                } catch (Exception ex) {
                    Log.Error(ex, "[OSC] Error parsing incoming packet");
                }
            }
        }

        private static List<(string Address, float Value)> ParseOscPacket(byte[] bytes) {
            var results = new List<(string, float)>();
            if (bytes.Length == 0) return results;

            string header = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 8));
            if (header.StartsWith("#bundle")) {
                int i = 16;
                while (i + 4 <= bytes.Length) {
                    byte[] sizeBytes = new byte[4];
                    Array.Copy(bytes, i, sizeBytes, 0, 4);
                    if (BitConverter.IsLittleEndian) Array.Reverse(sizeBytes);
                    int size = BitConverter.ToInt32(sizeBytes, 0);
                    i += 4;
                    if (i + size > bytes.Length) break;
                    ParseOscMessage(bytes, i, size, results);
                    i += size;
                }
            } else {
                ParseOscMessage(bytes, 0, bytes.Length, results);
            }
            return results;
        }

        private static void ParseOscMessage(byte[] bytes, int offset, int length, List<(string Address, float Value)> results) {
            try {
                int i = offset;
                while (i < offset + length && bytes[i] != 0) i++;
                string address = Encoding.ASCII.GetString(bytes, offset, i - offset);
                
                i = (i + 4) & ~3;
                if (i >= offset + length) return;

                float value = 1.0f;
                if (bytes[i] == ',') {
                    int typeStart = i;
                    while (i < offset + length && bytes[i] != 0) i++;
                    string types = Encoding.ASCII.GetString(bytes, typeStart, i - typeStart);
                    
                    i = (i + 4) & ~3;
                    if (i >= offset + length) return;

                    if (types.Contains("f")) { 
                        byte[] valBytes = new byte[4];
                        Array.Copy(bytes, i, valBytes, 0, 4);
                        if (BitConverter.IsLittleEndian) Array.Reverse(valBytes);
                        value = BitConverter.ToSingle(valBytes, 0);
                    } 
                    else if (types.Contains("i")) { 
                        byte[] valBytes = new byte[4];
                        Array.Copy(bytes, i, valBytes, 0, 4);
                        if (BitConverter.IsLittleEndian) Array.Reverse(valBytes);
                        value = BitConverter.ToInt32(valBytes, 0);
                    }
                }
                results.Add((address, value));
            } catch {}
        }
        
        public static void SetTempo(double bpm) {
            SendOscMessage("/tempo/raw", (float)bpm);
        }
        public static void Play() {
            if (ReaperIsPlaying) return; 
            
            ReaperIsPlaying = true;
            SendOscMessage("/play", 1.0f);
        }

        public static void Stop() {
            if (!ReaperIsPlaying) return; 
            
            ReaperIsPlaying = false; 
            SendOscMessage("/stop", 1.0f);
        }

        private static double lastSentTime = -100.0;
        public static void Seek(double seconds) {
            if (IsReceivingOsc) return; 
            if (Math.Abs(seconds - lastSentTime) < 0.1) {
                lastSentTime = seconds;
                return;
            }
            SendOscMessage("/time", (float)seconds);
            lastSentTime = seconds;
        }

        private static void SendOscMessage(string address, float value) {
            try {
                List<byte> packet = new List<byte>();
                packet.AddRange(Encoding.ASCII.GetBytes(address));
                packet.Add(0);
                while (packet.Count % 4 != 0) packet.Add(0);

                packet.AddRange(Encoding.ASCII.GetBytes(",f"));
                packet.Add(0);
                while (packet.Count % 4 != 0) packet.Add(0);

                byte[] valBytes = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian) Array.Reverse(valBytes);
                packet.AddRange(valBytes);

                byte[] data = packet.ToArray();
                sender.Send(data, data.Length, ReaperIp, SendPort);
            } catch {}
        }
    }
}