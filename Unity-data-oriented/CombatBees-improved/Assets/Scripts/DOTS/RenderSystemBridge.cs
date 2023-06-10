using UnityEngine;

namespace Systems
{
    public class RenderSystemBridge : MonoBehaviour
    {
        public Material RenderMaterial;
        public ComputeShader ComputeShader;
        public static RenderSystemBridge Instance;

        private void Awake()
        {
            Instance = this;
        }
    }
}