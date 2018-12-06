namespace Ambrosia
{
    public static class RpcTypes
    {
        public enum RpcType : byte
        {
            ReturnValue = 0,
            FireAndForget = 1,
            Impulse = 2,
        }

        public static bool IsFireAndForget(this RpcType rpcType)
        {
            return rpcType == RpcType.FireAndForget || rpcType == RpcType.Impulse;
        }
    }
}