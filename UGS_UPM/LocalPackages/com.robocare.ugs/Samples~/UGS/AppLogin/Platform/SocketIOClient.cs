using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BestHTTP.SocketIO3;
using BestHTTP.SocketIO.Events;
using BestHTTP.SocketIO3.Events;
using RoboCare.UGS;

public class SocketIOClient : MonoBehaviour
{
    public static SocketIOClient Instance;
    [SerializeField]private string address = "";
    public SocketManager socketManager = null;
    public Guid uuid;
    private bool isConnected = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
		else if (Instance != this) Destroy(gameObject);

		DontDestroyOnLoad(gameObject); // Optional
    }

    void Start()
    {
        uuid = Guid.NewGuid();

#if UNITY_IOS && !UNITY_EDITOR
        // iOS는 핸드폰 전용 — 로봇 없으므로 Socket.IO 연결 불필요
        return;
#endif

        RefreshSocketConnect();
        SendToServerFrameWork("Match3", "start");
        LogApi.Log("bomphago start socket");
    }

    void Update()
    {
        if(socketManager == null)
        {
            return;
        }
    }

    public void RefreshSocketConnect()
    {
        if (socketManager != null)
        {
            LogApi.Log($"이전 socketManager 존재, Close하고 새로운 연결 시작");
            socketManager.Close();
            socketManager = null;
        }

        address = GetSocketAddress();
        
        SocketIO3Connect(address);
    }
    
    private string GetSocketAddress()
    {
        string socketAddress = $"socket.io://{PlayerPrefs.GetString(PrefKeys.Socket)}";
        if(socketAddress == "" || !PlayerPrefs.HasKey(PrefKeys.Socket))
        {
            LogApi.LogWarning("Socket Address 값이 null 이거나 PlayerPrefs에 socket Key 없음");
        }
        else
            LogApi.Log($"Get Socket Address : {socketAddress}");

        return socketAddress;
    }

    private void SocketIO3Connect(string address)
    {
        SocketOptions options = new SocketOptions();
        options.AutoConnect = true;
        socketManager = new SocketManager(new System.Uri(address), options);
        socketManager.Open();
        socketManager.Socket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
        // socketManager.Socket.On<ConnectResponse>(SocketIOEventTypes.Error, TEST);
        socketManager.Socket.On(SocketIOEventTypes.Disconnect, OnDisConnected);
        socketManager.Socket.On("PlayerConnected", PlayerConnected);
        socketManager.Socket.On("ReceiveSocketIO", ReceiveSocketIO);
    }


    public class CommunicationToServiceFramework
    {
        // Old
        // public string signal_id_;
        // public string signal_type_;
        // public string signal_desc_;
        // public string signal_param_;
        // public string occured_from_;
        // public string occured_at_;

        public string name;
        public string activity;
    }

    public class Robot_Motion_Info
    {
        public string motion_name;
    }

    private void OnConnected(ConnectResponse res)
    {
        isConnected = true;
        RobotMotionCall("welcome");

        LogApi.Log("Connected! Socket.IO");
    }

    private void OnDisConnected()
    {
        isConnected = false;
        LogApi.Log("Disconnected! Socket.IO");
    }

    private void TEST(ConnectResponse res)
    {
        LogApi.Log(res);
    }

    private void PlayerConnected()
    {
        LogApi.Log("Player Connected!");
    }

    private void ReceiveSocketIO()
    {
        LogApi.Log("Callback Func SocketIO");
    }

    // public void SendToServerFrameWork(string signal_id, string signal_type, string signal_desc, string signal_param, string occured_from, string occured_at)
    // {
    //     CommunicationToServiceFramework comm = new CommunicationToServiceFramework();
    //     comm.signal_id_ = signal_id;
    //     comm.signal_type_ = signal_type;
    //     comm.signal_desc_ = signal_desc;
    //     comm.signal_param_ = signal_param;
    //     comm.occured_from_ = occured_from;
    //     comm.occured_at_ = occured_at;

    //     socketManager.Socket.Emit("signal", comm);
    // }

    public void SendToServerFrameWork(string name, string activity)
    {
        if (socketManager == null || !isConnected) return;

        CommunicationToServiceFramework comm = new CommunicationToServiceFramework();
        comm.name = name;
        comm.activity = activity;

        socketManager.Socket.Emit("game", comm);
    }

    public void RobotMotionCall(string motion_name)
    {
        if (socketManager == null || !isConnected) return;

        Robot_Motion_Info rmi = new Robot_Motion_Info();
        rmi.motion_name = motion_name;

        socketManager.Socket.Emit("Robot_Motion_Call", rmi);
        LogApi.Log(motion_name + " motion call!!");
    }

    void OnDestroy()
    {
        if (this.socketManager != null)
        {
            // Leaving this sample, close the socket
            this.socketManager.Close();
            this.socketManager = null;
        }
    }

}
