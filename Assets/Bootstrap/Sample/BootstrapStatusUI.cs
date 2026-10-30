using Scaffold.AppFlow;
using UnityEngine;

namespace Framework.Bootstrap.Sample
{
    [RequireComponent(typeof(AppFlowRoot))]
    public sealed class BootstrapStatusUI : MonoBehaviour
    {
        [SerializeField] string _side = "Engine";
        [SerializeField] int _row = 0;

        AppFlowRoot _root;

        void Awake() => _root = GetComponent<AppFlowRoot>();

        void OnGUI()
        {
            var rect = new Rect(20, 20 + _row * 60, 260, 50);
            var status = _root.ReadyTask.IsCompletedSuccessfully ? "Engine bootstrapped"
                       : _root.ReadyTask.IsCompleted ? "Bootstrap failed"
                       : "Booting...";
            GUI.Box(rect, $"{_side}\n{status}");
        }
    }
}
