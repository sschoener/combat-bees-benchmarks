using UnityEngine;

namespace Systems
{
    public class RenderSystemBridge : MonoBehaviour
    {
        public Material RenderMaterial;
        public ComputeShader ComputeShader;
        public Mesh Mesh;

        private static bool s_HasSearchedForInstance;
        private static RenderSystemBridge s_Instance;

        public static RenderSystemBridge Instance
        {
            get
            {
                if (!s_HasSearchedForInstance)
                {
                    s_Instance = FindAnyObjectByType<RenderSystemBridge>();
                    s_HasSearchedForInstance = true;
                }

                return s_Instance;
            }
        }
    }
}