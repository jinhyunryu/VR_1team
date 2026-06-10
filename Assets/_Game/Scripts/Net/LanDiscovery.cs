using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// LAN 호스트 발견(UDP 브로드캐스트). 호스트: 1초마다 포트 47878 로 신호 송출.
/// 클라: 같은 포트 수신 → 호스트 IP 획득. 핫스팟 시연용(IP 타이핑 제거).
///
/// 붙이는 법: "Multiplayer" GameObject 에 추가 + SessionConnector 의 lanDiscovery 에 연결.
/// </summary>
public class LanDiscovery : MonoBehaviour
{
    private const int Port = 47878;
    private const string Signature = "VRBOAT";

    public string DiscoveredHostIp { get; private set; }

    private UdpClient udp;
    private float lastBroadcast;
    private bool isHost;

    public void StartHostBroadcast()
    {
        StopAll();
        isHost = true;
        udp = new UdpClient { EnableBroadcast = true };
    }

    public void StartClientListen()
    {
        StopAll();
        isHost = false;
        DiscoveredHostIp = null;
        udp = new UdpClient(Port);
        udp.BeginReceive(OnReceive, null);
    }

    public void StopAll()
    {
        try { udp?.Close(); } catch { }
        udp = null;
        isHost = false;
    }

    private void Update()
    {
        if (!isHost || udp == null || Time.time - lastBroadcast < 1f) return;
        lastBroadcast = Time.time;
        byte[] data = Encoding.ASCII.GetBytes(Signature);
        try { udp.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, Port)); }
        catch (Exception e) { Debug.LogWarning($"[LanDiscovery] 송출 실패: {e.Message}"); }
    }

    private void OnReceive(IAsyncResult ar)
    {
        try
        {
            var from = new IPEndPoint(IPAddress.Any, Port);
            byte[] data = udp.EndReceive(ar, ref from);
            if (Encoding.ASCII.GetString(data) == Signature)
                DiscoveredHostIp = from.Address.ToString();
            udp.BeginReceive(OnReceive, null);
        }
        catch { /* 소켓 닫힘 — 정상 종료 경로 */ }
    }

    private void OnDestroy() => StopAll();
}
