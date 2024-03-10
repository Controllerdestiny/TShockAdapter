using System.Net;
using System.Net.Sockets;
using System.Text;
using TShockAPI;

namespace MorMorAdapter.SocketReceive;

internal class SocketClient
{
    public class State
    {
        public byte[] Buffer = new byte[2048];

        public Socket Client { get; }

        public State(Socket client)
        {
            Client = client;
        }
    }
    private Socket Client { get; set; }

    private IPAddress IPAddress { get; set; }

    private int Port { get; set; }

    private int ReConnectCount = 0;

    public event Action<string> OnMessage;

    public event Action OnConnect;

    public SocketClient(IPAddress address, int prot)
    {
        IPAddress = address;
        Port = prot;
    }

    public void Start()
    {
        try
        {
            Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Client.BeginConnect(IPAddress, Port, ConnectCallBack, Client);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError(ex.Message);
        }
    }

    public async void SendMessgae(string msg)
    {
        if (Client.Connected)
            await Client.SendAsync(Encoding.UTF8.GetBytes(msg), SocketFlags.None);
    }

    private async void ConnectCallBack(IAsyncResult ar)
    {
        if (ar.AsyncState is Socket clint && clint.Connected)
        {
            OnConnect?.Invoke();
            TShock.Log.ConsoleError("成功连接到MorMorBOT...");
            await Task.Delay(Plugin.Config.SocketConfig.ReConnectTimer);
            State state = new(clint);
            Client.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ReceiveCallBack, state);
            clint.EndConnect(ar);
            ReConnectCount = 0;
        }
        else
        {
            ReConnectCount++;
            TShock.Log.ConsoleError($"[{ReConnectCount}]连接已断开，{Plugin.Config.SocketConfig.ReConnectTimer / 1000}秒后尝试重新连接...");
            await Task.Delay(Plugin.Config.SocketConfig.ReConnectTimer);
            Client.BeginConnect(IPAddress, Port, ConnectCallBack, Client);
        }
    }


    private void ReceiveCallBack(IAsyncResult ar)
    {
        if (ar.AsyncState is State state)
        {
            try
            {
                state.Client.EndReceive(ar);
                var NewState = new State(state.Client);
                state.Client.BeginReceive(NewState.Buffer, 0, NewState.Buffer.Length, SocketFlags.None, ReceiveCallBack, NewState);
                OnMessage?.Invoke(Encoding.UTF8.GetString(state.Buffer));
            }
            catch (SocketException ex)
            {
                TShock.Log.ConsoleError(ex.ToString());
                Client = null;
                Start();
            }
        }
    }
}
