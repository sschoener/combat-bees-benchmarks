using Systems;
using UnityEngine;

public class RenderUpdater : MonoBehaviour
{
    public void Update()
    {
        var instance = CustomRenderSystem.Instance;
        if (instance == null || !instance.Inited)
            return;
        instance.OutDependency.Complete();
        instance.Team1Buffer.DataA.EndWrite<CustomRenderSystem.Properties>(instance.WriteCount);
        instance.Team2Buffer.DataA.EndWrite<CustomRenderSystem.Properties>(instance.WriteCount);
        instance.Team1Buffer.Swap();
        instance.Team2Buffer.Swap();
        RenderTeamBuffer(instance, instance.RenderMaterial1, instance.Team2Buffer, Color.yellow);
        RenderTeamBuffer(instance, instance.RenderMaterial2, instance.Team1Buffer, Color.blue);
    }
    
    private static readonly int PropertiesId = Shader.PropertyToID("_Properties");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private void RenderTeamBuffer(CustomRenderSystem s, Material material, in CustomRenderSystem.Buffers buf, Color color)
    {
        material.SetBuffer(PropertiesId, buf.DataA);
        material.SetColor(ColorId, color);
        buf.Args.SetData(s.ShaderArgs);
        Graphics.DrawMeshInstancedIndirect(s.Mesh, 0, material, s.Bounds, buf.Args);
    }
}