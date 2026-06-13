using System;
using UnityEngine;

namespace NowUI.Internal
{
    public class NowBootstrap : MonoBehaviour
    {
        public event Action onPreUpdate;

        public event Action onUpdate;

        public event Action onPostUpdate;

        private void Update()
        {
            onPreUpdate?.Invoke();
            onUpdate?.Invoke();
            onPostUpdate?.Invoke();
        }
    }
}