using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// LAN 호스트 발견(UDP 브로드캐스트). 호스트: 1초마다 포트 47878 로 신호 송출.
/// 클라: 같은 포트 수신 → 호스트 IP 획득. 핫스팟/교실 LAN 시연용(IP 타이핑 제거).
///
/// 호스트는 255.255.255.255 + "모든 활성 인터페이스의 서브넷 브로드캐스트 주소"로 송출 —
/// VR/VPN 가상 어댑터가 있어도 진짜 LAN 으로 신호가 나가게.
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
    private readonly List<IPEndPoint> broadcastTargets = new();

    public void StartHostBroadcast()
    {
        StopAll();
        isHost = true;
        udp = new UdpClient { EnableBroadcast = true };
        BuildBroadcastTargets();
    }

    public void StartClientListen()
    {
        StopAll();
        isHost = false;
        DiscoveredHostIp = null;
        try
        {
            udp = new UdpClient(Port);
            udp.BeginReceive(OnReceive, null);
            Debug.Log($"[LanDiscovery] 클라 수신 대기 시작 (UDP {Port})");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LanDiscovery] 수신 포트 바인딩 실패(다른 인스턴스가 점유?): {e.Message}");
        }
    }

    public void StopAll()
    {
        try { udp?.Close(); } catch { }
        udp = null;
        isHost = false;
    }

    /// HUD 표시/수동 접속 안내용 — 이 기기의 LAN IPv4 (없으면 "?").
    public static string GetLocalIpHint()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        return ua.Address.ToString();
            }
        }
        catch { }
        return "?";
    }

    // 255.255.255.255 + 인터페이스별 서브넷 브로드캐스트(예: 192.168.0.255) 목록 구성.
    private void BuildBroadcastTargets()
    {
        broadcastTargets.Clear();
        broadcastTargets.Add(new IPEndPoint(IPAddress.Broadcast, Port));
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                    byte[] ip = ua.Address.GetAddressBytes();
                    byte[] mask = ua.IPv4Mask.GetAddressBytes();
                    var bc = new byte[4];
                    for (int i = 0; i < 4; i++) bc[i] = (byte)(ip[i] | ~mask[i]);
                    broadcastTargets.Add(new IPEndPoint(new IPAddress(bc), Port));
                }
            }
        }
        catch (Exception e) { Debug.LogWarning($"[LanDiscovery] 인터페이스 조회 실패(전역 브로드캐스트만 사용): {e.Message}"); }
    }

    private bool sendLogged;

    private void Update()
    {
        if (!isHost || udp == null || Time.time - lastBroadcast < 1f) return;
        lastBroadcast = Time.time;
        byte[] data = Encoding.ASCII.GetBytes(Signature);
        int ok = 0;
        foreach (var target in broadcastTargets)
        {
            try { udp.Send(data, data.Length, target); ok++; }
            catch { /* 일부 인터페이스 송출 실패는 무시 */ }
        }
        if (!sendLogged)
        {
            sendLogged = true;
            var sb = new StringBuilder();
            foreach (var t in broadcastTargets) sb.Append(t).Append("  ");
            Debug.Log($"[LanDiscovery] 호스트 송출 시작 — 대상 {broadcastTargets.Count}개(성공 {ok}): {sb}");
        }
    }

    private void OnReceive(IAsyncResult ar)
    {
        try
        {
            var from = new IPEndPoint(IPAddress.Any, Port);
            byte[] data = udp.EndReceive(ar, ref from);
            if (Encoding.ASCII.GetString(data) == Signature)
            {
                if (DiscoveredHostIp == null)
                    Debug.Log($"[LanDiscovery] 호스트 발견: {from.Address}");
                DiscoveredHostIp = from.Address.ToString();
            }
            udp.BeginReceive(OnReceive, null);
        }
        catch { /* 소켓 닫힘 — 정상 종료 경로 */ }
    }

    private void OnDestroy() => StopAll();
}
