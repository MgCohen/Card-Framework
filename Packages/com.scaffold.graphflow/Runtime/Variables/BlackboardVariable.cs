using System;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class BlackboardVariable<T> : VariableDefault<T> { }

    [Serializable] public sealed class BlackboardInt    : BlackboardVariable<int> { }
    [Serializable] public sealed class BlackboardFloat  : BlackboardVariable<float> { }
    [Serializable] public sealed class BlackboardBool   : BlackboardVariable<bool> { }
    [Serializable] public sealed class BlackboardString : BlackboardVariable<string> { }
    [Serializable] public sealed class BlackboardObject : BlackboardVariable<UnityEngine.Object> { }
}
