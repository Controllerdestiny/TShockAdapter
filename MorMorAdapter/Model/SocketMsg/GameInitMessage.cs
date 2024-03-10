using MorMorAdapter.Enumerates;

namespace MorMorAdapter.Model.SocketMsg;

public class GameInitMessage : BaseMessage
{
    public GameInitMessage()
    {
        MessageType = MessageType.GamePostInit;
    }
}
