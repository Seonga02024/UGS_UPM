using System;

namespace RoboCare.UGS
{
    [Serializable]
    public class GetOrInitPlayerMoneyResponse
    {
        public bool success;
        public string playerId;
        public string key;
        public long money;
        public bool updated;
        public string message;
    }

}
